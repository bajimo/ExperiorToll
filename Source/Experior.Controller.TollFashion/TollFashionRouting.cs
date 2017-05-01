using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using Dematic.DATCOMAUS;
using Experior.Catalog.Dematic.Case.Components;
using Experior.Catalog.Dematic.DatcomAUS.Assemblies;
using Experior.Core.Loads;
using Experior.Core.Routes;
using Experior.Dematic.Base;
using Experior.Core;
using System;

namespace Experior.Controller.TollFashion
{
    public partial class TollFashionRouting : Catalog.ControllerExtended
    {
        private readonly MHEControllerAUS_Case plc51, plc52, plc53, plc54, plc61, plc62, plc63;
        private readonly List<PickPutStation> rapidPickstations;
        private List<Catalog.Dematic.Case.Devices.CommunicationPoint> activePoints;
        private readonly List<EquipmentStatus> equipmentStatuses = new List<EquipmentStatus>();
        public IReadOnlyList<EquipmentStatus> EquipmentStatuses { get; private set; }
        private readonly EmulationController emulationController;
        private readonly StraightAccumulationConveyor emptyToteLine;
        private readonly StraightConveyor cartonErector1, cartonErector2, cartonErector3;
        private readonly Dictionary<string, string> cartonErectorSize = new Dictionary<string, string>();

        private ActionPoint lidnp2;
        private Load lidnp2Load;
        private readonly Timer lidnp2Resend;
        private readonly Timer cartonErectorTimer;
        private EquipmentStatus cc51cartona1, cc52cartona1, cc53cartona1;

        public TollFashionRouting() : base("TollFashionRouting")
        {
            StandardConstructor();

            emulationController = new EmulationController();

            emptyToteLine = Core.Assemblies.Assembly.Get("P1963") as StraightAccumulationConveyor;
            cartonErector1 = Core.Assemblies.Assembly.Get("P1051") as StraightConveyor;
            cartonErector2 = Core.Assemblies.Assembly.Get("P1052") as StraightConveyor;
            cartonErector3 = Core.Assemblies.Assembly.Get("P1053") as StraightConveyor;

            plc51 = Core.Assemblies.Assembly.Get("PLC 51") as MHEControllerAUS_Case;
            plc52 = Core.Assemblies.Assembly.Get("PLC 52") as MHEControllerAUS_Case;
            plc53 = Core.Assemblies.Assembly.Get("PLC 53") as MHEControllerAUS_Case;
            plc54 = Core.Assemblies.Assembly.Get("PLC 54") as MHEControllerAUS_Case;
            plc61 = Core.Assemblies.Assembly.Get("PLC 61") as MHEControllerAUS_Case;
            plc62 = Core.Assemblies.Assembly.Get("PLC 62") as MHEControllerAUS_Case;
            plc63 = Core.Assemblies.Assembly.Get("PLC 63") as MHEControllerAUS_Case;
            rapidPickstations = Core.Assemblies.Assembly.Items.Values.OfType<PickPutStation>().ToList();

            if (plc51 != null)
            {
                plc51.OnRequestAllDataTelegramReceived += OnRequestAllDataTelegramReceived;
                plc51.OnSetSystemStatusTelegramReceived += OnSetSystemStatusTelegramReceived;
                plc51.OnTransportOrderTelegramReceived += OnTransportOrderTelegramReceived;
            }
            if (plc52 != null)
            {
                plc52.OnRequestAllDataTelegramReceived += OnRequestAllDataTelegramReceived;
                plc52.OnSetSystemStatusTelegramReceived += OnSetSystemStatusTelegramReceived;
                plc52.OnTransportOrderTelegramReceived += OnTransportOrderTelegramReceived;
            }
            if (plc53 != null)
            {
                plc53.OnRequestAllDataTelegramReceived += OnRequestAllDataTelegramReceived;
                plc53.OnSetSystemStatusTelegramReceived += OnSetSystemStatusTelegramReceived;
                plc53.OnTransportOrderTelegramReceived += OnTransportOrderTelegramReceived;
            }
            if (plc54 != null)
            {
                plc54.OnRequestAllDataTelegramReceived += OnRequestAllDataTelegramReceived;
                plc54.OnSetSystemStatusTelegramReceived += OnSetSystemStatusTelegramReceived;
            }
            if (plc61 != null)
            {
                plc61.OnRequestAllDataTelegramReceived += OnRequestAllDataTelegramReceived;
                plc61.OnSetSystemStatusTelegramReceived += OnSetSystemStatusTelegramReceived;
            }
            if (plc62 != null)
            {
                plc62.OnRequestAllDataTelegramReceived += OnRequestAllDataTelegramReceived;
                plc62.OnSetSystemStatusTelegramReceived += OnSetSystemStatusTelegramReceived;
            }
            if (plc63 != null)
            {
                plc63.OnRequestAllDataTelegramReceived += OnRequestAllDataTelegramReceived;
                plc63.OnSetSystemStatusTelegramReceived += OnSetSystemStatusTelegramReceived;
            }

            Core.Environment.Time.ContinuouslyRunning = true;
            Core.Environment.Scene.OnLoaded += Scene_OnLoaded;
            Core.Environment.Scene.OnStarting += Scene_OnStarting;
            CreateEquipmentStatuses();
            lidnp2Resend = new Timer(2.5f);    
            lidnp2Resend.OnElapsed += Lidnp2Resend_Elapsed;

            cartonErectorTimer = new Timer(1);
            cartonErectorTimer.AutoReset = true;
            cartonErectorTimer.OnElapsed += CartonErectorTimer_OnElapsed;
        }

        private void OnTransportOrderTelegramReceived(object sender, MessageEventArgs e)
        {
            if (e.Location == "CC51CARTONA1" || e.Location == "CC52CARTONA1" || e.Location == "CC53CARTONA1")
            {
                var size = e.Telegram.GetFieldValue(sender as IDATCOMAUSTelegrams, TelegramFields.CarrierSize);
                if (size == "10" || size == "01")
                {
                    //Update carton size to produce at carton erector
                    cartonErectorSize[e.Location] = size;
                }
                else
                {
                    Log.Write($"Unknown carton size in transport order at location {e.Location}. Carton size requested: {size}. Request is ignored!");
                }
            }
        }

        private void Scene_OnStarting()
        {
            cartonErectorTimer.Start();
        }

        private void CartonErectorTimer_OnElapsed(Timer sender)
        {           
            var size1 = cartonErectorSize[cc51cartona1.FunctionGroup]; //10 large, 01 small.
            var size2 = cartonErectorSize[cc52cartona1.FunctionGroup]; //10 large, 01 small.
            var size3 = cartonErectorSize[cc53cartona1.FunctionGroup]; //10 large, 01 small.
            //Group status CA00: no cartons available
            //Group status CA01: Only small cartons are available. Large carton magazine may be empty or in fault, etc.
            //Group status CA10: Only large cartons are available. Small carton magazine may be empty or in fault, etc.
            //Group status CA11: Both small and large carton available

            if (cartonErector1.LoadCount <= 1 && cc51cartona1.GroupStatus != "00")
            {
                if (cc51cartona1.GroupStatus == "11" || cc51cartona1.GroupStatus == size1)
                    FeedCarton(cartonErector1, size1);
            }
            if (cartonErector2.LoadCount <= 1 && cc52cartona1.GroupStatus != "00")
            {
                if (cc52cartona1.GroupStatus == "11" || cc52cartona1.GroupStatus == size2)
                    FeedCarton(cartonErector2, size2);
            }
            if (cartonErector3.LoadCount <= 1 && cc53cartona1.GroupStatus != "00")
            {
                if (cc53cartona1.GroupStatus == "11" || cc53cartona1.GroupStatus == size3)
                    FeedCarton(cartonErector3, size3);
            }
        }

        private void FeedCarton(StraightConveyor cartonErector, string size)
        {
            var caseData = new CaseData { Length = 0.580f, Width = 0.480f, Height = 0.365f, colour = Color.Peru, Weight = 0 };

            if (size == "10")
            {
                //large
                caseData.CarrierSize = "01";
            }
            else if (size == "01")
            {
                //small
                caseData.CarrierSize = "00";
                caseData.Length = 0.490f;
                caseData.Width = 0.360f;
            }
            else
            {
                //what todo?
            }

            var carton = FeedLoad.FeedCaseLoad(cartonErector.TransportSection, caseData.Length / 2, caseData.Length, caseData.Width, caseData.Height, 0, Color.Peru, 8, caseData);
            caseData.Weight = 0;
            carton.Identification = "";       
        }

        private void Lidnp2Resend_Elapsed(Timer sender)
        {
            if (lidnp2 == null)
                return;

            if (lidnp2Load == null)
                return;

            if (lidnp2.Active && lidnp2Load == lidnp2.ActiveLoad && lidnp2Load.Stopped)
            {
                plc61.SendArrivalMessage(lidnp2.Name, lidnp2.ActiveLoad as Case_Load);
                lidnp2Resend.Start();
            }
        }

        private void CreateEquipmentStatuses()
        {
            //00 is ok, 01 is fault. Some function groups use different statuses.

            //Table 50 Functional Groups – Order Fulfilment     
            equipmentStatuses.Add(new EquipmentStatus(plc51, "CC51CBUFFIN", "11")); //11 induction running
            equipmentStatuses.Add(new EquipmentStatus(plc52, "CC52CBUFFIN", "11")); //11 induction running
            equipmentStatuses.Add(new EquipmentStatus(plc53, "CC53CBUFFIN", "11")); //11 induction running
            equipmentStatuses.Add(new EquipmentStatus(plc51, "CC51OBUFFIN"));
            equipmentStatuses.Add(new EquipmentStatus(plc52, "CC52OBUFFIN"));
            equipmentStatuses.Add(new EquipmentStatus(plc53, "CC53OBUFFIN"));
            equipmentStatuses.Add(new EquipmentStatus(plc51, "CC51BUFF"));
            equipmentStatuses.Add(new EquipmentStatus(plc52, "CC52BUFF"));
            equipmentStatuses.Add(new EquipmentStatus(plc53, "CC53BUFF"));
            equipmentStatuses.Add(new EquipmentStatus(plc51, "CC51OBUFFOUT"));
            equipmentStatuses.Add(new EquipmentStatus(plc52, "CC52OBUFFOUT"));
            equipmentStatuses.Add(new EquipmentStatus(plc53, "CC53OBUFFOUT"));

            var plc = plc51;
            for (int i = 1; i <= 24; i++)
            {
                if (i == 9)
                    plc = plc52;
                if (i == 17)
                    plc = plc53;

                equipmentStatuses.Add(new EquipmentStatus(plc, $"CC{plc.SenderIdentifier}RP{i:00}IN"));
                equipmentStatuses.Add(new EquipmentStatus(plc, $"RP{i:00}"));
            }

            equipmentStatuses.Add(new EquipmentStatus(plc53, "CC53QA"));

            //Table 59 Functional Groups – Finishing
            equipmentStatuses.Add(new EquipmentStatus(plc54, "CC54LOOP", "11")); //11 loop is running
            equipmentStatuses.Add(new EquipmentStatus(plc54, "CC54FINISHLINE"));
            equipmentStatuses.Add(new EquipmentStatus(plc54, "CC54RECYCLE"));
            equipmentStatuses.Add(new EquipmentStatus(plc54, "CC54GOH"));

            //Table 62 Functional Groups – Documentation & Lidding
            equipmentStatuses.Add(new EquipmentStatus(plc61, "CC61COMMON"));
            equipmentStatuses.Add(new EquipmentStatus(plc61, "CC61DOCLID"));
            equipmentStatuses.Add(new EquipmentStatus(plc61, "CC61DOC1"));
            //equipmentStatuses.Add(new EquipmentStatus(plc61, "CC61DOC2")); //future
            equipmentStatuses.Add(new EquipmentStatus(plc61, "CC61QA"));
            equipmentStatuses.Add(new EquipmentStatus(plc61, "CC61LID1"));
            equipmentStatuses.Add(new EquipmentStatus(plc61, "CC61LID2"));
            equipmentStatuses.Add(new EquipmentStatus(plc61, "CC61LID3"));
            //equipmentStatuses.Add(new EquipmentStatus(plc61, "CC61LID4")); //future
            equipmentStatuses.Add(new EquipmentStatus(plc61, "CC61ONLINE"));
            equipmentStatuses.Add(new EquipmentStatus(plc61, "CC61RECYCLE"));
            equipmentStatuses.Add(new EquipmentStatus(plc61, "CC61LPA1"));
            equipmentStatuses.Add(new EquipmentStatus(plc61, "CC61LPA2"));

            //Table 65 Functional Groups – Sorter
            equipmentStatuses.Add(new EquipmentStatus(plc62, "CC62TOSORT"));
            equipmentStatuses.Add(new EquipmentStatus(plc62, "CC62DBIN"));
            equipmentStatuses.Add(new EquipmentStatus(plc62, "CC62LOOP", "11")); //11 loop is running
            for (int i = 1; i <= 9; i++)
            {
                equipmentStatuses.Add(new EquipmentStatus(plc62, $"CC62LANE{i}"));
            }
            equipmentStatuses.Add(new EquipmentStatus(plc62, "CC62REJECT"));

            //Table 71 Functional Groups – Decant
            equipmentStatuses.Add(new EquipmentStatus(plc63, "CC63ETL", "10"));  //10 is empty
            equipmentStatuses.Add(new EquipmentStatus(plc63, "CC63LOOP", "11")); //11 loop is running
            equipmentStatuses.Add(new EquipmentStatus(plc63, "CC63QA1"));
            equipmentStatuses.Add(new EquipmentStatus(plc63, "CC63QA2"));

            //todo Where are these mentioned?:

            equipmentStatuses.Add(cc51cartona1 = new EquipmentStatus(plc51, "CC51CARTONA1", "11")); //CC11? 11 Both small and large cartons are available.
            cartonErectorSize["CC51CARTONA1"] = "10"; //Large          
            equipmentStatuses.Add(cc52cartona1 = new EquipmentStatus(plc52, "CC52CARTONA1", "11")); //CC11? 11 Both small and large cartons are available.
            cartonErectorSize["CC52CARTONA1"] = "10"; //Large
            equipmentStatuses.Add(cc53cartona1 = new EquipmentStatus(plc53, "CC53CARTONA1", "11")); //CC11? 11 Both small and large cartons are available.
            cartonErectorSize["CC53CARTONA1"] = "10"; //Large

            EquipmentStatuses = new List<EquipmentStatus>(equipmentStatuses);
        }

        private void OnSetSystemStatusTelegramReceived(object sender, MessageEventArgs e)
        {
            var plc = sender as MHEControllerAUS_Case;
            string status = e.Telegram.GetFieldValue(sender as IDATCOMAUSTelegrams, TelegramFields.SystemStatus);
            if (status == "03")
            {
                //Send equipment status from sender plc
                var equipmentList = equipmentStatuses.FindAll(p => p.Plc == plc);
                foreach (var equipment in equipmentList)
                {
                    equipment.Plc.SendEquipmentStatus(equipment.FunctionGroup, equipment.GroupStatus);
                }
            }
        }

        private void Scene_OnLoaded()
        {
            Core.Environment.Scene.OnLoaded -= Scene_OnLoaded;
            activePoints = Core.Assemblies.Assembly.Items.Values.Where(a => a.Assemblies != null).SelectMany(a => a.Assemblies)
                    .OfType<Catalog.Dematic.Case.Devices.CommunicationPoint>()
                    .Where(c => c.Name.EndsWith("A1"))
                    .ToList();
        }

        private void OnRequestAllDataTelegramReceived(object sender, MessageEventArgs e)
        {
            var plc = sender as MHEControllerAUS_Case;
            if (plc == null)
                return;
            var rapidPicks = rapidPickstations.Where(r => r.Controller == plc);
            foreach (var rapidPick in rapidPicks)
            {
                if (rapidPick.LeftLoad != null)
                    plc.SendRemapUlData(rapidPick.LeftLoad);
                if (rapidPick.RightLoad != null)
                    plc.SendRemapUlData(rapidPick.RightLoad);
            }

            foreach (var point in activePoints)
            {
                var caseload = point.apCommPoint.ActiveLoad as Case_Load;
                if (caseload != null)
                {
                    if (plc == point.Controller)
                    {
                        plc.SendRemapUlData(caseload);
                    }
                }
            }
        }

        protected override void Reset()
        {
            lidnp2Load = null;
            base.Reset();
        }

        protected override void Arriving(INode node, Load load)
        {
            if (node.Name.StartsWith("ROUTETO:"))
            {
                var dest = node.Name.Replace("ROUTETO:", "");
                if (dest.StartsWith("PICK"))
                {
                    var picknumber = int.Parse(dest.Substring(4, 2));
                    var plc = plc51;
                    if (picknumber > 8 && picknumber <= 16)
                        plc = plc52;
                    if (picknumber > 17)
                        plc = plc53;

                    var caseLoad = load as Case_Load;
                    if (caseLoad == null)
                    {
                        Log.Write($"Error: Load exiting MS and going to pick {picknumber} is no caseLoad");
                        return;
                    }

                    //Set the destination so the case load will cross the main line
                    if (!plc.RoutingTable.ContainsKey(caseLoad.SSCCBarcode))
                        plc.RoutingTable[caseLoad.SSCCBarcode] = dest;

                }
                return;
            }
            if (node.Name == "ETLENTRY")
            {
                load.OnDisposed += EmptyToteDisposed;
                SendEmptyToteLineFillLevel();
                return;
            }
            if (node.Name == "CC51ECFA0")
            {
                HandleFailedCartons(plc51, node.Name, load);
                return;
            }
            if (node.Name == "CC52ECFA0")
            {
                HandleFailedCartons(plc52, node.Name, load);
                return;
            }
            if (node.Name == "CC53ECFA0")
            {
                HandleFailedCartons(plc53, node.Name, load);
                return;
            }
            if (node.Name == "CC51ECINP1" || node.Name == "CC52ECINP1" || node.Name == "CC53ECINP1")
            {
                //Apply barcode
                AddBarcodeAndData(load);
                return;
            }
            if (node.Name == "SORTERWEIGHT")
            {
                AddWeight(load);
                AddBarcode2(load);
                AddProfile(load);
                return;
            }
            if (node.Name == "DECANTWEIGHT")
            {
                AddWeight(load);
                AddProfile(load);
                AddBarcode2(load);
                return;
            }
            if (node.Name == "CC54PROFILE")
            {
                AddProfile(load);
                AddBarcode2(load);
                return;
            }
            if (node.Name == "CC61SWAP")
            {
                AddWeight(load);
                AddProfile(load);
                AddBarcode2(load);
                return;
            }
            if (node.Name.StartsWith("CLEARSWAP"))
            {
                ClearSwap(load);
                return;
            }
            if (node.Name.StartsWith("CC61LID") && node.Name.EndsWith("A1"))
            {
                //Add lid
                AddLid(load);
                return;
            }
            if (node.Name == "CC61LIDNP2")
            {
                //Resend arrival after 2,5 sec if no transport order received
                if (lidnp2 == null)
                {
                    lidnp2 = node as ActionPoint;
                }
                lidnp2Load = load;
                lidnp2Resend.Start();
            }
            if (node.Name.EndsWith("CARTONA1"))
            {
                RequestValidBarcodes();
            }
        }

        private void RequestValidBarcodes()
        {
            if (emulationController.CartonBarcodesNeeded)
            {
                emulationController.SendCartonBarcodesRequest("CARTONERECTION", 50);
            }
        }

        private static void AddLid(Load load)
        {
            var part = load.Part;
            var box = new BoxPart(part.Length, part.Height, part.Width, part.Density, part.Color, Core.Loads.Load.Rigids.Cube);
            box.Position = part.Position;
            box.Orientation = part.Orientation;
            load.Part = box;
            part.Dispose();
        }

        private void AddBarcode2(Load load)
        {
            var caseLoad = load as Case_Load;

            var caseData = caseLoad?.Case_Data as CaseData;
            if (caseData == null)
                return;

            caseData.Barcode2 = emulationController.GetBarcode2(load.Identification);
        }

        private static void ClearSwap(Load load)
        {
            var caseLoad = load as Case_Load;

            var caseData = caseLoad?.Case_Data as CaseData;
            if (caseData == null)
                return;

            //Reset to default values
            caseData.Profile = "@@@@";
            caseData.Weight = 0;
            caseData.Barcode2 = "";
        }

        private void AddProfile(Load load)
        {
            var caseLoad = load as Case_Load;

            var caseData = caseLoad?.Case_Data as CaseData;
            if (caseData == null)
                return;

            caseData.Profile = emulationController.GetProfile(load.Identification);
        }

        private void AddWeight(Load load)
        {
            var caseLoad = load as Case_Load;

            var caseData = caseLoad?.Case_Data as CaseData;
            if (caseData == null)
                return;

            caseData.Weight = emulationController.GetWeight(load.Identification);
        }

        private void AddBarcodeAndData(Load load)
        {
            load.Identification = emulationController.GetNextValidBarcode();

            var caseLoad = load as Case_Load;

            var caseData = caseLoad?.Case_Data as CaseData;
            if (caseData == null)
                return;

            //TODO how to handle barcode2?
            //caseData.Barcode2 = nextBarcode;
            //TODO add more here?           
        }

        private void HandleFailedCartons(MHEControllerAUS_Case plc, string location, Load load)
        {
            //Check if carton is failed 
            if (!plc.RoutingTable.ContainsKey(load.Identification))
            {
                //Load unknown... this should not happen
                return;
            }

            var destination = plc.RoutingTable[load.Identification];
            if (destination == location)
            {
                //Load failed. 
                load.Stop();
                load.Color = Color.Red;
                Timer.Action(() =>
                {
                    load.Dispose();
                    Log.Write($"Failed carton ({load.Identification}) manually removed from {location}");
                }, 5);
            }
        }

        private void EmptyToteDisposed(Load load)
        {
            load.OnDisposed -= EmptyToteDisposed;
            SendEmptyToteLineFillLevel();
        }

        private void SendEmptyToteLineFillLevel()
        {
            //’10’ Empty Tote Line – Empty
            //’11’ Empty Tote Line – 1 / 6 Full
            //’12’ Empty Tote Line – 1 / 3 Full
            //’13’ Empty Tote Line – 1 / 2 Full
            //’14’ Empty Tote Line – 2 / 3 Full
            //’15’ Empty Tote Line – 5 / 6 Full
            //’16’ Empty Tote Line – Full
            var eqStatus = equipmentStatuses.First(e => e.FunctionGroup == "CC63ETL");
            var fillLevel = emptyToteLine.LoadCount / (float)emptyToteLine.Positions;
            if (fillLevel <= 0)
            {
                eqStatus.GroupStatus = "10";
            }
            else if (fillLevel < 1 / 6f)
            {
                eqStatus.GroupStatus = "11";
            }
            else if (fillLevel < 1 / 3f)
            {
                eqStatus.GroupStatus = "12";
            }
            else if (fillLevel < 2 / 3f)
            {
                eqStatus.GroupStatus = "13";
            }
            else if (fillLevel < 5 / 6f)
            {
                eqStatus.GroupStatus = "14";
            }
            else
            {
                eqStatus.GroupStatus = "15";
            }
        }

        private void ResetStandard()
        {
            Core.Environment.Scene.Reset();

            foreach (Core.Communication.Connection connection in Core.Communication.Connection.Items.Values)
            {
                connection.Disconnect();
            }
        }

        public override void Dispose()
        {
            Core.Environment.UI.Toolbar.Remove(speed1);
            Core.Environment.UI.Toolbar.Remove(speed2);
            Core.Environment.UI.Toolbar.Remove(speed5);
            Core.Environment.UI.Toolbar.Remove(speed10);
            Core.Environment.UI.Toolbar.Remove(speed20);

            Core.Environment.UI.Toolbar.Remove(reset);
            Core.Environment.UI.Toolbar.Remove(fps1);
            Core.Environment.UI.Toolbar.Remove(localProp);
            Core.Environment.UI.Toolbar.Remove(connectButt);
            Core.Environment.UI.Toolbar.Remove(disconnectButt);
            if (plc51 != null)
            {
                plc51.OnRequestAllDataTelegramReceived -= OnRequestAllDataTelegramReceived;
                plc51.OnSetSystemStatusTelegramReceived -= OnSetSystemStatusTelegramReceived;
                plc51.OnTransportOrderTelegramReceived -= OnTransportOrderTelegramReceived;
            }
            if (plc52 != null)
            {
                plc52.OnRequestAllDataTelegramReceived -= OnRequestAllDataTelegramReceived;
                plc52.OnSetSystemStatusTelegramReceived -= OnSetSystemStatusTelegramReceived;
                plc52.OnTransportOrderTelegramReceived -= OnTransportOrderTelegramReceived;
            }
            if (plc53 != null)
            {
                plc53.OnRequestAllDataTelegramReceived -= OnRequestAllDataTelegramReceived;
                plc53.OnSetSystemStatusTelegramReceived -= OnSetSystemStatusTelegramReceived;
                plc53.OnTransportOrderTelegramReceived -= OnTransportOrderTelegramReceived;
            }
            if (plc54 != null)
            {
                plc54.OnRequestAllDataTelegramReceived -= OnRequestAllDataTelegramReceived;
                plc54.OnSetSystemStatusTelegramReceived -= OnSetSystemStatusTelegramReceived;
            }
            if (plc61 != null)
            {
                plc61.OnRequestAllDataTelegramReceived -= OnRequestAllDataTelegramReceived;
                plc61.OnSetSystemStatusTelegramReceived -= OnSetSystemStatusTelegramReceived;
            }
            if (plc62 != null)
            {
                plc62.OnRequestAllDataTelegramReceived -= OnRequestAllDataTelegramReceived;
                plc62.OnSetSystemStatusTelegramReceived -= OnSetSystemStatusTelegramReceived;
            }
            if (plc63 != null)
            {
                plc63.OnRequestAllDataTelegramReceived -= OnRequestAllDataTelegramReceived;
                plc63.OnSetSystemStatusTelegramReceived -= OnSetSystemStatusTelegramReceived;
            }

            cartonErectorTimer.OnElapsed -= CartonErectorTimer_OnElapsed;
            cartonErectorTimer.Dispose();
            lidnp2Resend.OnElapsed -= Lidnp2Resend_Elapsed;
            lidnp2Resend.Dispose();
            base.Dispose();
        }
    }

    public class EquipmentStatus
    {
        private string groupStatus;
        [Browsable(false)]
        public MHEControllerAUS_Case Plc { get; }
        [DisplayName("Function Group")]
        public string FunctionGroup { get; }
        [DisplayName("Group Status")]
        public string GroupStatus
        {
            get { return groupStatus; }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    return;
                if (value.Length != 2)
                    return;

                if (value != groupStatus)
                {
                    groupStatus = value;
                    Plc.SendEquipmentStatus(FunctionGroup, groupStatus);
                }
            }
        }

        public EquipmentStatus(MHEControllerAUS_Case plc, string functionGroup, string groupStatus = "00")
        {
            Plc = plc;
            FunctionGroup = functionGroup;
            this.groupStatus = groupStatus;
        }
    }
}