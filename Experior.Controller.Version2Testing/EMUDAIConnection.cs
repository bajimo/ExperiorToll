using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Experior.Dematic.Base;
using System.Drawing;
using Experior.Catalog.Assemblies.BK10;

namespace Experior.Controller
{
    public partial class Version2Testing : Experior.Catalog.ControllerExtended
    {
        private List<string[]> dataMissionID = new List<string[]>();
        int cycleNumber = 0;
        private Core.Timer dataCheckTicker;

        private void EMUDAIConnectionConstructor()
        {
            //Data Check Ticker
            dataCheckTicker = new Core.Timer(1);
            dataCheckTicker.OnElapsed += new Core.Timer.ElapsedEvent(dataCheckTicker_Elapsed);
            dataCheckTicker.AutoReset = true;
            dataCheckTicker.Start();
        }

        #region Receive Messages
        private void EMUTelegramReceived(string[] telegramFields)
        {
            string header = telegramFields[0];
            string type = header.Substring(0, 4);
            string subtype = header.Substring(4, 4);
            string cycleno = header.Substring(8, 6);

            switch (type)
            {
                case "FEED":
                    FeedTelegramRecieved(telegramFields, subtype);
                    break;
                case "WIPE":
                    DeleteTelegramRecieved(telegramFields, subtype);
                    break;
                case "AUTO":
                    AutoFeedTelegramRecieved(telegramFields, subtype);
                    break;
                case "DATA":
                    DataTelegramRecieved(telegramFields, subtype);
                    break;
                case "RELE":
                    ReleaseTelegramReceived(telegramFields, subtype);
                    break;
                default:
                    Core.Environment.Log.Write("Recieved unknown type on EMU connection: " + type);
                    break;
            }
        }

        private void AutoFeedTelegramRecieved(string[] telegramFields, string subtype)
        {

        }

        private void DataTelegramRecieved(string[] telegramFields, string subtype)
        {
            if (subtype == "D001")
            {
                string location = telegramFields[1];
                if (location == "ETBLSTART")
                {
                    for (int i = 2; i < telegramFields.Length; i++)
                    {
                        if (telegramFields[i] != "")
                            etlbStartBarcodes.Add(telegramFields[i]);
                    }
                }
            }
            else if (subtype == "D002" || subtype == "D003")
            {
                //WMS sends data after data request.
                //Find caseload and update the data (SSCC barcode)
                string missionID = telegramFields[1];
                string ssccbarcode = telegramFields[2];
                string tmType = telegramFields[3];
                Case_Load caseload = Case_Load.GetCaseFromULID(missionID);

                if (caseload == null)
                {
                    dataMissionID.Add(telegramFields);
                    Core.Environment.Log.Write("Error: No caseload found with mission ID " + missionID + " when recieving data 05 subtype D002/3 on emulation communication");
                    return;
                }

                caseload.SSCCBarcode = ssccbarcode;

                try
                {
                    switch (tmType)
                    {
                        case "TOTE": caseload.Color = Color.Blue; break;
                        case "PICKTOTE": caseload.Color = Color.LightBlue; break;
                        case "CARTON": caseload.Color = Color.Peru; break;
                    }
                }
                catch
                {
                }
            }
        }

        //BG: I've added this because sometimes the data message is not being processed for "D002" or "D003" messages. If it does I have
        //added to a list and on a ticker (every 5 seconds) check if the data can be updated.
        void dataCheckTicker_Elapsed(Core.Timer sender)
        {
            //ExperiorOutputMessage("Ticker", MessageSeverity.Test);

            if (dataMissionID.Count != 0)
            {
                List<string[]> foundDataMissions = new List<string[]>();
                foreach (string[] telegramFields in dataMissionID)
                {
                    //WMS sends data after data request.
                    //Find caseload and update the data (SSCC barcode)
                    string missionID = telegramFields[1];
                    string ssccbarcode = telegramFields[2];
                    Case_Load caseload = Case_Load.GetCaseFromULID(missionID);

                    if (caseload != null)
                    {
                        caseload.SSCCBarcode = ssccbarcode;
                        ExperiorOutputMessage(string.Format("Error Recovery: Updated mission ID {0} after initial failure", missionID), MessageSeverity.Information);

                        if (telegramFields.Length >= 5)
                        {
                            //Inner tray barcodes (Tesco specific)
                            caseload.Case_Data.UserData = telegramFields[3] + "," + telegramFields[4];
                        }

                        foundDataMissions.Add(telegramFields);
                    }
                }

                foreach (string[] telegramFields in foundDataMissions)
                {
                    dataMissionID.Remove(telegramFields);
                }
            }
        }

        private void DeleteTelegramRecieved(string[] telegramFields, string subtype)
        {
            if (subtype == "W001" || subtype == "W002" || subtype == "W004")
            {
                //Delete tote message from wcs
                string location = telegramFields[2];
                string totebarcode = telegramFields[1];

                Case_Load caseload = Case_Load.GetCaseFromSSCCBarcode(totebarcode);

                if (caseload != null && caseload.Deletable && caseload.UserDeletable)
                {
                    caseload.Dispose();
                    //Core.Environment.Log.Write("Case load deleted, Barcode: " + totebarcode);
                }
                else
                {
                    Core.Environment.Log.Write("Could not delete case load barcode: " + totebarcode);
                    if (caseload == null)
                        Core.Environment.Log.Write("Case load not found, barcode: " + totebarcode);
                    else
                        Core.Environment.Log.Write("Case load not ready to be deleted, barcode: " + totebarcode);
                }
            }
        }

        private void FeedTelegramRecieved(string[] telegramFields, string subtype)
        {
            if (subtype == "F001" || subtype == "F004")  //F001 Tote (Stock Tote), F004 Pick Tote 
            {
                //Feed tote to system
                string location = telegramFields[1];
                string totebarcode = telegramFields[2];
                //string quantity = telegramFields[3];
                string length = telegramFields[3];
                string width = telegramFields[4];
                string height = telegramFields[5];
                string weight = telegramFields[6];

                if (Experior.Core.Assemblies.Assembly.Items.ContainsKey(location))
                {
                    StraightConveyor conv = Experior.Core.Assemblies.Assembly.Items[location] as StraightConveyor;

                    if (conv.PLC != null)
                    {
                        float l = float.Parse(length) / 1000f;//mm
                        float h = float.Parse(height) / 1000f; //mm
                        float w = float.Parse(width) / 1000f; //mm

                        Case_Load caseload = conv.PLC.CreateCaseLoad(l, w, h);
                        caseload.SSCCBarcode = totebarcode;

                        if (subtype == "F001")
                        {
                            caseload.Color = Color.Blue;
                            caseload.UserData = "TOTE";
                        }
                        else //F004
                        {
                            caseload.Color = Color.LightBlue;
                            caseload.UserData = "PICKTOTE";
                        }

                        //Turn totes to correct orientation when added to conveyors
                        if (conv.Width > 0.5)
                        {
                            caseload.Yaw = (float)(Math.PI / 2f);
                        }

                        float weightfloat = float.Parse(weight);
                        weightfloat /= 1000f; // to get kg
                        caseload.Case_Data.Weight = weightfloat;
                        conv.TransportSection.Route.Add(caseload);
                    }
                    else
                    {
                        Core.Environment.Log.Write("Location not configured with plc for feed telegram location: " + location);
                        Core.Environment.Scene.Pause();
                    }
                }
                else
                {
                    Core.Environment.Log.Write("Location not found for feed telegram: " + location);
                    //Core.Environment.Scene.Pause();
                }
            }
            else if (subtype == "F002") //CARTON 
            {
                //Feed carton to system. 
                string location = telegramFields[1];
                string cartonIdentifier = telegramFields[2];
                string length = telegramFields[3];
                string width = telegramFields[4];
                string height = telegramFields[5];
                string weight = telegramFields[6];

                StraightConveyor conv = Core.Assemblies.Assembly.Items[location] as StraightConveyor;

                if (conv == null)
                {
                    Core.Environment.Log.Write("FEED error, location: " + location + " not found");
                    Core.Environment.Scene.Pause();
                    return;
                }
                if (conv.PLC != null)
                {
                    float l = float.Parse(length) / 1000f;//mm
                    float h = float.Parse(height) / 1000f; //mm
                    float w = float.Parse(width) / 1000f; //mm

                    Case_Load caseload = conv.PLC.CreateCaseLoad(l, w, h);
                    caseload.SSCCBarcode = cartonIdentifier;
                    caseload.UserData = "CARTON";
                    caseload.Color = Color.Peru;

                    //Turn totes to correct orientation when added to conveyors
                    if (conv.Width > 0.5)
                    {
                        caseload.Yaw = (float)(Math.PI / 2f);
                    }

                    float weightfloat = float.Parse(weight);
                    weightfloat /= 1000f; // to get kg
                    caseload.Case_Data.Weight = weightfloat;

                    conv.TransportSection.Route.Add(caseload);
                }
                else
                {
                    Core.Environment.Log.Write("Location not configured with plc for feed telegram location: " + location);
                    //Core.Environment.Scene.Pause();
                }
            }
        }

        private void ReleaseTelegramReceived(string[] telegramFields, string subtype)
        {
            if (subtype == "S001")
            {
                //Delete tote message from wcs
                string location = telegramFields[2];
                string totebarcode = telegramFields[1];

                Case_Load caseload = Case_Load.GetCaseFromSSCCBarcode(totebarcode);

                if (caseload != null)
                {
                    caseload.Release();
                    //Core.Environment.Log.Write("Case load deleted, Barcode: " + totebarcode);
                }
                else
                {
                    Core.Environment.Log.Write("Case load not found and could not be released, barcode: " + totebarcode);
                }
            }
        }
        #endregion

        #region Send Messages
        private void SendRequestTelegram(string subType, string TMType, string location, string barcode, string quantity = null)
        {
            string telegram = null;

            if (subType == "R004" || subType == "R005")
            {
                telegram = string.Format("RQST{0}{1},{2},{3},{4}", subType, EMUCycleNumber(),barcode, location, TMType);
                emuconnection.Send(telegram);
            }
            //Add further request types here

            if (telegram != null)
                ExperiorOutputMessage("EMU>MFH: " + emuconnection.Id.ToString() + " " + telegram, MessageSeverity.Information);

        }

        private string EMUCycleNumber()
        {
            cycleNumber++;
            if (cycleNumber > 999999)
                cycleNumber = 1;

            return cycleNumber.ToString("000000");

        }



        #endregion
    }
}
