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
        private readonly Queue<string> validCartonErectionBarcodes = new Queue<string>();
        private readonly Dictionary<string, float> loadWeight = new Dictionary<string, float>();
        private readonly Core.Communication.TCPIP.Connection emuconnection;
        private int noBarcodesCount;

        public EmulationController()
        {
            emuconnection = Core.Communication.Connection.Get(99) as Core.Communication.TCPIP.Connection;
            if (emuconnection != null)
            {
                emuconnection.OnTelegramReceived += Emuconnection_OnTelegramReceived;
            }
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
                if (location == "CARTONERECTION")
                {
                    for (int i = 2; i < telegramFields.Length; i++)
                    {
                        if (telegramFields[i] != "")
                            validCartonErectionBarcodes.Enqueue(telegramFields[i]);
                    }
                }
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
                            colour = Color.Peru;
                        }

                        var caseData = new CaseData { Length = l, Width = w, Height = h, colour = colour, Weight = we };
                        var caseLoad = FeedLoad.FeedCaseLoad(conv.TransportSection, l / 2, l, w, h, we, colour, 10, caseData);
                        caseLoad.SSCCBarcode = barcode;
                        caseLoad.Identification = barcode;

                        //if (conv.Width > 0.5)
                        //{
                        //    caseLoad.Yaw = (float)(Math.PI / 2f);
                        //}
                        if (conv.CaseOrientation == CaseOrientation.LengthLeading && l > w)
                        {
                            caseLoad.Yaw = (float)(Math.PI / 2f);
                        }

                        loadWeight[barcode] = we;
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

                //if (caseload != null && (location == "ZP01" || location == "ZP02" || location == "ZP03" || location == "ZP04" || location == "ZP05" || location == "ZP06"))
                //{
                //    if (caseload.Route.Parent.Parent is MergeDivertConveyor &&
                //        ((MergeDivertConveyor)caseload.Route.Parent.Parent).Name.Substring(0, 3) == "ZP0" &&
                //        ((MergeDivertConveyor)caseload.Route.Parent.Parent).Name.Substring(4, 5) == "_PUSH" &&
                //        ((MergeDivertConveyor)caseload.Route.Parent.Parent).LoadOnAP(caseload, Direction.Left))
                //    {
                //        var mergeDivert = caseload.Route.Parent.Parent as MergeDivertConveyor;
                //        caseload.Release();
                //        mergeDivert.RouteLoad(caseload, new List<Direction>() { Direction.Left }, false);
                //    }
                //    else
                //    {
                //        lock (zonePickRelease)
                //        {
                //            if (zonePickRelease.ContainsKey(caseload.SSCCBarcode))
                //            {
                //                zonePickRelease.Remove(caseload.SSCCBarcode);
                //            }
                //            zonePickRelease.Add(caseload.SSCCBarcode, location);
                //        }
                //    }
                //}
                //else if (caseload != null && (location == "PTW1" || location == "PTW2" || location == "PTW3" || location == "PTW4" ||
                //    location == "PTW5" || location == "PTW6" || location == "PTW7"))
                //{
                //    if (caseload.Route.Parent.Parent is MergeDivertConveyor &&
                //        ((MergeDivertConveyor)caseload.Route.Parent.Parent).Name.Substring(0, 3) == "PTW" &&
                //        ((MergeDivertConveyor)caseload.Route.Parent.Parent).Name.Substring(4, 5) == "_PUSH" &&
                //        ((MergeDivertConveyor)caseload.Route.Parent.Parent).LoadOnAP(caseload, Direction.Right))
                //    {
                //        var mergeDivert = caseload.Route.Parent.Parent as MergeDivertConveyor;
                //        caseload.Release();
                //        mergeDivert.RouteLoad(caseload, new List<Direction>() { Direction.Right }, false);
                //    }
                //    else
                //    {
                //        lock (putwallRelease)
                //        {
                //            if (putwallRelease.ContainsKey(caseload.SSCCBarcode))
                //            {
                //                putwallRelease.Remove(caseload.SSCCBarcode);
                //            }
                //            putwallRelease.Add(caseload.SSCCBarcode, location);
                //        }
                //    }
                //}
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

        public void SendTrayBarcodesRequest(string location, int quantity)
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

        public string GetBarcode2(string identification)
        {
            //todo how to do barcode2?
            return identification;
        }

        public string GetNextValidBarcode()
        {
            var nextBarcode = validCartonErectionBarcodes.Any() ? validCartonErectionBarcodes.Dequeue() : $"noBarcodes {++noBarcodesCount}";
            return nextBarcode;
        }

        public float GetWeight(string barcode1)
        {
            if (loadWeight.ContainsKey(barcode1))
                return loadWeight[barcode1];

            return 0;
        }
    }
}