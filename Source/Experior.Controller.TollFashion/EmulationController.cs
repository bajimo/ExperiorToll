using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using Experior.Catalog.Dematic.Case;
using Experior.Catalog.Dematic.Case.Components;
using Experior.Catalog.Dematic.DatcomAUS.Assemblies;
using Experior.Dematic.Base;

namespace Experior.Controller.TollFashion
{
    public class EmulationController
    {
        private int cycleNumber;
        //private readonly Queue<string> validCartonErectionBarcodes = new Queue<string>();
        private readonly Dictionary<string, float> loadWeight = new Dictionary<string, float>();
        private readonly Core.Communication.TCPIP.Connection emuconnection;

        public event EventHandler<string[]> FeedReceived;
        //private int noBarcodesCount;

        //public bool CartonBarcodesNeeded => validCartonErectionBarcodes.Count < 20;

        public EmulationController()
        {
            emuconnection = Core.Communication.Connection.Get(99) as Core.Communication.TCPIP.Connection;
            if (emuconnection != null)
            {
                emuconnection.OnTelegramReceived += Emuconnection_OnTelegramReceived;
                emuconnection.OnConnected += Emuconnection_OnConnected;
            }
        }

        private void Emuconnection_OnConnected(Core.Communication.Connection connection)
        {
            Core.Environment.Invoke(CheckResetMessage);
        }

        private void CheckResetMessage()
        {
            //var loads = Experior.Core.Loads.Load.Items.Where(l => l.Route?.Parent?.Parent?.Name == )
            var loadCount = Core.Loads.Load.Items.Count(l => !l.Embedded);
            if (loadCount == 0)
                SendReset();

            if (loadCount > 6)
                return; //Carton erectors hold up to 6 cartons. If > 6 then system has been running

            var cartonErectorConveyrs = new List<string>() {"P1051", "P1052", "P1053"};
            var loadsNotOnCartonErector = Core.Loads.Load.Items.Count(l => !l.Embedded && !cartonErectorConveyrs.Contains(l.Route?.Parent?.Parent?.Name));
            if (loadsNotOnCartonErector == 0)
                SendReset();
        }

        private void Emuconnection_OnTelegramReceived(Core.Communication.TCPIP.Connection sender, string telegram)
        {
            if (telegram == null)
                return;

            if (Core.Environment.InvokeRequired)
            {
                Core.Environment.Invoke(() => Emuconnection_OnTelegramReceived(sender, telegram));
                return;
            }

            Log.Write(DateTime.Now.ToString(CultureInfo.InvariantCulture) + " MFH>EMU: " + emuconnection.Id + " " + telegram);
            var telegramFields = telegram.Split(',');
            HandleTelegram(telegramFields);
        }

        private void HandleTelegram(string[] telegramFields)
        {
            var header = telegramFields[0];
            var type = header.Substring(0, 4);
            var subtype = header.Substring(4, 4);
            //var cycleno = header.Substring(8, 6);

            switch (type)
            {
                case "FEED":
                    FeedTelegramRecieved(telegramFields, subtype);
                    break;
                case "WIPE":
                    DeleteTelegramRecieved(telegramFields, subtype);
                    break;
                //case "AUTO":
                //    AutoFeedTelegramRecieved(telegramFields, subtype);
                //    break;
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

        //private void AutoFeedTelegramRecieved(string[] telegramFields, string subtype)
        //{

        //}

        private void DataTelegramRecieved(string[] telegramFields, string subtype)
        {
            if (subtype == "D001")
            {
                string location = telegramFields[1];
                //if (location == "CARTONERECTION")  //We extract from ZPL script instead
                //{
                //    for (int i = 2; i < telegramFields.Length; i++)
                //    {
                //        if (telegramFields[i] != "")
                //            validCartonErectionBarcodes.Enqueue(telegramFields[i]);
                //    }
                //}
            }
            else if (subtype == "D002" || subtype == "D003")
            {
                //WMS sends data after data request.
                //Find caseload and update the data (SSCC barcode)
                //var ssccBarcode = telegramFields[1];
                //var labellerBarcode = telegramFields[2];
                //var tmType = telegramFields[3];
                //Case_Load caseload = Case_Load.GetCaseFromSSCCBarcode(ssccBarcode);

                //if (caseload != null)
                //{
                //    DespatchLabels.Add(ssccBarcode, labellerBarcode);
                //}
            }
        }

        private void DeleteTelegramRecieved(string[] telegramFields, string subtype)
        {
            if (subtype == "W001" || subtype == "W002" || subtype == "W004")
            {
                //Delete tote message from wcs
                //var location = telegramFields[2];
                var totebarcode = telegramFields[1];

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
            if (subtype == "F001" || subtype == "F004" || subtype == "F002")  //F001 Tote (Stock Tote), F004 Pick Tote, F002 Carton 
            {
                //Feed tote to system
                var location = telegramFields[1];
                var barcode = telegramFields[2];
                var length = telegramFields[3];
                var width = telegramFields[4];
                var height = telegramFields[5];
                var weight = telegramFields[6];

                if (location == "P1051" || location == "P1052" || location == "P1053")
                {
                    //Carton erector feeding
                    if (!string.IsNullOrWhiteSpace(barcode))
                    {
                        //Store the barcode for the label applicator. //MRP changed to use ZPL script
                        //validCartonErectionBarcodes.Enqueue(barcode);
                        barcode = "";
                    }
                    subtype = "F002"; //only cartons at these locations. 
                    //Todo: should I do this? And should I use the size from equipment status (and not the size from the feeding message)?
                }

                if (Core.Assemblies.Assembly.Items.ContainsKey(location))
                {
                    var conv = Core.Assemblies.Assembly.Items[location] as StraightConveyor;

                    if (conv != null)
                    {
                        var l = float.Parse(length) / 1000f;//mm
                        var h = float.Parse(height) / 1000f; //mm
                        var w = float.Parse(width) / 1000f; //mm
                        var we = float.Parse(weight) / 1000f;

                        var colour = Color.Blue;
                        if (subtype == "F002")
                        {
                            //Carton
                            colour = Color.Peru;
                        }

                        var caseData = new CaseData { Length = l, Width = w, Height = h, colour = colour, Weight = 0 };
                        var caseLoad = FeedLoad.FeedCaseLoad(conv.TransportSection, l / 2, l, w, h, we, colour, 10, caseData);
                        caseData.Weight = 0;
                        if (subtype == "F002")
                        {
                            //Carton
                            if (l >= 0.58f)
                            {
                                //Large
                                caseData.CarrierSize = "CA00";
                            }
                            else
                            {
                                //Small (medium?)
                                caseData.CarrierSize = "CA01";
                            }
                        }
                        if (subtype == "F001")
                        {
                            //Product tote
                            caseData.CarrierSize = "BX01";
                            if (barcode.StartsWith("9") && barcode.Length == 7)
                            {
                                //Add product tote barcode suffix
                                if (Core.Environment.Random.Next(0, 2) == 0)
                                {
                                    barcode = barcode + "1";
                                }
                                else
                                {
                                    barcode = barcode + "2";
                                }
                            }
                        }

                        caseLoad.SSCCBarcode = barcode;
                        caseLoad.Identification = barcode;

                        if (conv.CaseOrientation == CaseOrientation.LengthLeading && l > w)
                        {
                            caseLoad.Yaw = (float)(Math.PI / 2f);
                        }

                        //Store the weight for weigh stations
                        if (!string.IsNullOrWhiteSpace(barcode))
                        {
                            loadWeight[barcode] = we;
                        }
                        caseLoad.OnDisposed += CaseLoad_OnDisposed;
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
                }
            }

            OnFeedReceived(telegramFields);
        }

        private void CaseLoad_OnDisposed(Core.Loads.Load load)
        {
            load.OnDisposed -= CaseLoad_OnDisposed;
            loadWeight.Remove(load.Identification);
        }

        private void ReleaseTelegramReceived(string[] telegramFields, string subtype)
        {
            if (subtype == "S001")
            {
                //Delete tote message from wcs
                //var location = telegramFields[2];
                var totebarcode = telegramFields[1];
                var caseload = Case_Load.GetCaseFromSSCCBarcode(totebarcode);
                caseload.ReleaseLoad();
            }
        }

        public void SendRequestTelegram(string subType, string tmType, string location, string barcode, string quantity = null)
        {
            string telegram = null;

            if (subType == "R004" || subType == "R005")
            {
                telegram = $"RQST{subType}{EmuCycleNumber()},{barcode},{location},{tmType}";
                emuconnection.Send(telegram);
            }
            //Add further request types here
            if (telegram != null)
                Log.Write(DateTime.Now.ToString(CultureInfo.InvariantCulture) + " EMU>MFH: " + emuconnection.Id + " " + telegram);
        }

        private string EmuCycleNumber()
        {
            cycleNumber++;
            if (cycleNumber > 999999)
                cycleNumber = 1;

            return cycleNumber.ToString("000000");
        }

        public void SendCartonBarcodesRequest(string location, int quantity)
        {
            var header = "RQSTR001" + EmuCycleNumber();
            var telegram = header + "," + location + ",CARTON," + quantity;
            Core.Environment.Log.Write(DateTime.Now.ToString(CultureInfo.InvariantCulture) + " MFH<EMU: " + emuconnection.Id + " " + telegram);
            emuconnection.Send(telegram);
        }

        public string GetProfile(string identification)
        {
            //todo
            return "@@@@";
        }

        //public string GetNextValidBarcode()
        //{
        //    var nextBarcode = validCartonErectionBarcodes.Any() ? validCartonErectionBarcodes.Dequeue() : $"noBarcodes {++noBarcodesCount}";
        //    return nextBarcode;
        //}

        public float GetWeight(string barcode1)
        {
            if (loadWeight.ContainsKey(barcode1))
                return loadWeight[barcode1];

            return 0;
        }

        protected virtual void OnFeedReceived(string[] telegramFields)
        {
            FeedReceived?.Invoke(this, telegramFields);
        }

        public void SendReset()
        {
            SendReset("ALL");
        }

        public void SendReset(string area)
        {
            //Send reset message to WCS
            var header = "MODEL001" + EmuCycleNumber();
            var telegram = header + "," + area;
            Core.Environment.Log.Write(DateTime.Now.ToString(CultureInfo.InvariantCulture) + " MFH<EMU: " + emuconnection.Id + " " + telegram);
            emuconnection.Send(telegram);
        }
    }
}