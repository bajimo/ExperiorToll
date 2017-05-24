using System;
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
using Experior.Dematic.Base.Devices;
using Environment = Experior.Core.Environment;

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
        private readonly ZplLabeler carton51Labeler, carton52Labeler, carton53Labeler, carton61Labeler1, carton61Labeler2;

        private ActionPoint lidnp2;
        private Load lidnp2Load;
        private readonly Timer lidnp2Resend;
        private readonly Timer cartonErectorTimer, cartonErectorResendTimer;
        private readonly Timer onlinePackingStationTimer;
        private EquipmentStatus cc51Cartona1, cc52Cartona1, cc53Cartona1;
        private DematicCommunicationPoint cc51Cartona1Comm, cc52Cartona1Comm, cc53Cartona1Comm;
        private readonly Dictionary<ActionPoint, double> onlinePackingStations = new Dictionary<ActionPoint, double>();
        private double onlinePackingTime = 20; //20 seconds from arrival to done
        private readonly StraightConveyor emptyCartonOnlineTakeAway;
        private readonly HashSet<StraightBeltConveyor> dispatchLanes = new HashSet<StraightBeltConveyor>();

        public TollFashionRouting() : base("TollFashionRouting")
        {
            carton51Labeler = new ZplLabeler("CAR51");
            carton52Labeler = new ZplLabeler("CAR52");
            carton53Labeler = new ZplLabeler("CAR53");

            carton61Labeler1 = new ZplLabeler("LPA1");
            carton61Labeler2 = new ZplLabeler("LPA2");

            emulationController = new EmulationController();
            emulationController.FeedReceived += EmulationController_FeedReceived;

            emptyToteLine = Core.Assemblies.Assembly.Get("P1963") as StraightAccumulationConveyor;
            cartonErector1 = Core.Assemblies.Assembly.Get("P1051") as StraightConveyor;
            cartonErector2 = Core.Assemblies.Assembly.Get("P1052") as StraightConveyor;
            cartonErector3 = Core.Assemblies.Assembly.Get("P1053") as StraightConveyor;

            emptyCartonOnlineTakeAway = Core.Assemblies.Assembly.Get("P7071") as StraightConveyor;

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
                plc61.OnTransportOrderTelegramReceived += OnTransportOrderTelegramReceived61;
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

            Environment.Time.ContinuouslyRunning = true;
            Environment.Scene.OnLoaded += Scene_OnLoaded;
            Environment.Scene.OnStarting += Scene_OnStarting;
            
            lidnp2Resend = new Timer(2.5f);
            lidnp2Resend.OnElapsed += Lidnp2Resend_Elapsed;

            cartonErectorTimer = new Timer(1);
            cartonErectorTimer.AutoReset = true;
            cartonErectorTimer.OnElapsed += CartonErectorTimer_OnElapsed;

            cartonErectorResendTimer = new Timer(10);
            cartonErectorResendTimer.AutoReset = true;
            cartonErectorResendTimer.OnElapsed += CartonErectorResendTimer_OnElapsed;

            //Init online packing stations (Actionpoint, arrival timestamp)
            for (int i = 1; i <= 18; i++)
            {
                onlinePackingStations.Add(ActionPoint.Get("ONLINE" + i), -1f);
            }

            onlinePackingStationTimer = new Timer(1);
            onlinePackingStationTimer.AutoReset = true;
            onlinePackingStationTimer.OnElapsed += OnlinePackingStationTimer_OnElapsed;

            dispatchLanes.Add(Core.Assemblies.Assembly.Items["P5162-4"] as StraightBeltConveyor);
            dispatchLanes.Add(Core.Assemblies.Assembly.Items["P6162-4"] as StraightBeltConveyor);
            dispatchLanes.Add(Core.Assemblies.Assembly.Items["P7162-4"] as StraightBeltConveyor);
            dispatchLanes.Add(Core.Assemblies.Assembly.Items["P8162-4"] as StraightBeltConveyor);

            dispatchLanes.Add(Core.Assemblies.Assembly.Items["P11071"] as StraightBeltConveyor);
            dispatchLanes.Add(Core.Assemblies.Assembly.Items["P11121"] as StraightBeltConveyor);
            dispatchLanes.Add(Core.Assemblies.Assembly.Items["P11141"] as StraightBeltConveyor);
            dispatchLanes.Add(Core.Assemblies.Assembly.Items["P11161"] as StraightBeltConveyor);
            dispatchLanes.Add(Core.Assemblies.Assembly.Items["P11181"] as StraightBeltConveyor);
            dispatchLanes.Add(Core.Assemblies.Assembly.Items["P11201"] as StraightBeltConveyor);

            CreateEquipmentStatuses();
            StandardConstructor();
        }

        private void CartonErectorResendTimer_OnElapsed(Timer sender)
        {
            if (cc51Cartona1Comm.apCommPoint.Active)
                plc51.SendArrivalMessage(cc51Cartona1Comm.Name, cc51Cartona1Comm.apCommPoint.ActiveLoad as Case_Load, "00", true, false);

            if (cc52Cartona1Comm.apCommPoint.Active)
                plc52.SendArrivalMessage(cc52Cartona1Comm.Name, cc52Cartona1Comm.apCommPoint.ActiveLoad as Case_Load, "00", true, false);

            if (cc53Cartona1Comm.apCommPoint.Active)
                plc53.SendArrivalMessage(cc53Cartona1Comm.Name, cc53Cartona1Comm.apCommPoint.ActiveLoad as Case_Load, "00", true, false);
        }

        private void EmulationController_FeedReceived(object sender, string[] telegramFields)
        {
            var location = telegramFields[1];
            var barcode = telegramFields[2];
            if (location.StartsWith("DC"))
            {
                //Decant feeding. Delete a tote on the empty tote line (if any)
                if (emptyToteLine.LoadCount > 0)
                {
                    //Search for the tote on the empty tote line
                    var exists = emptyToteLine.TransportSection.Route.Loads.FirstOrDefault(l => l.Identification.Substring(0, Math.Min(7, l.Identification.Length)) == barcode.Substring(0, Math.Min(7, barcode.Length)));
                    if (exists != null)
                    {
                        exists.Dispose();
                    }
                    else
                    {
                        //If no match then just dispose the front load
                        emptyToteLine.TransportSection.Route.Loads.Last.Value.Dispose();
                    }
                }
            }
        }

        private void Dispatch_LineReleasePhotocell_OnPhotocellStatusChanged(object sender, Dematic.Base.Devices.PhotocellStatusChangedEventArgs e)
        {
            if (e._PhotocellStatus == PhotocellState.Blocked)
            {
                //Remove (dispose) the load at dispatch
                Timer.Action(() =>
                {
                    e._Load.Dispose();
                    Log.Write($"Carton {e._Load.Identification} removed from dispatch lane");
                }, 5);
            }
        }

        private void OnlinePackingStationTimer_OnElapsed(Timer sender)
        {
            //Check we have space on empty line
            if (emptyCartonOnlineTakeAway.ThisRouteStatus.Available == RouteStatuses.Blocked)
                return;

            if (emptyCartonOnlineTakeAway.LoadCount > 50)
                return;

            //Check all online packing stations
            foreach (var p in onlinePackingStations)
            {
                if (p.Value > 0 && Environment.Time.Simulated > p.Value + onlinePackingTime)
                {
                    if (p.Key.Active)
                    {
                        //Switch to empty line
                        var carton = p.Key.ActiveLoad;
                        carton.Yaw = 0;
                        carton.Switch(emptyCartonOnlineTakeAway.TransportSection.Route);
                        carton.Release();
                    }
                }
            }
        }

        private void OnTransportOrderTelegramReceived61(object sender, MessageEventArgs e)
        {
            if (e.Destination.StartsWith("CC61LID") && e.Destination.EndsWith("A1"))
            {
                //sent to lidder
                //Update carrier size
                var caseData = (e.Load as Case_Load)?.Case_Data as CaseData;
                if (caseData != null)
                {
                    caseData.CarrierSize = e.Telegram.GetFieldValue(sender as IDATCOMAUSTelegrams, TelegramFields.CarrierSize).Trim();
                }
            }
        }

        private void OnTransportOrderTelegramReceived(object sender, MessageEventArgs e)
        {
            if (e.Location == "CC51CARTONA1" || e.Location == "CC52CARTONA1" || e.Location == "CC53CARTONA1")
            {
                var size = e.Telegram.GetFieldValue(sender as IDATCOMAUSTelegrams, TelegramFields.CarrierSize);
                size = size.Replace("CA", "");
                if (size == "02" || size == "10" || size == "01")
                {
                    //Update carton size to produce at carton erector

                    if (size == "02" || size == "10")
                        cartonErectorSize[e.Location] = "10";
                    if (size == "01")
                        cartonErectorSize[e.Location] = "01";
                }
                else
                {
                    Log.Write($"Unknown carton size in transport order at location {e.Location}. Carton size requested: {size}. Request is ignored!");
                }

                //Clear carrier size (field is updated at carton induction, after scanner)
                var caseData = (e.Load as Case_Load)?.Case_Data as CaseData;
                if (caseData != null)
                {
                    caseData.CarrierSize = "";
                }
            }
        }

        private void Scene_OnStarting()
        {
            cartonErectorTimer.Start();
            //onlinePackingStationTimer.Start(); //MRP disabled. WCS sends delete and feed messages for EMPTYCARTONRETURN instead
            cartonErectorResendTimer.Start();
        }

        private void CartonErectorTimer_OnElapsed(Timer sender)
        {
            var size1 = cartonErectorSize[cc51Cartona1.FunctionGroup]; //10 large, 01 small.
            var size2 = cartonErectorSize[cc52Cartona1.FunctionGroup]; //10 large, 01 small.
            var size3 = cartonErectorSize[cc53Cartona1.FunctionGroup]; //10 large, 01 small.
            //Group status CA00: no cartons available
            //Group status CA01: Only small cartons are available. Large carton magazine may be empty or in fault, etc.
            //Group status CA10: Only large cartons are available. Small carton magazine may be empty or in fault, etc.
            //Group status CA11: Both small and large carton available

            if (cartonErector1.LoadCount <= 1 && cc51Cartona1.GroupStatus != "00")
            {
                if (cc51Cartona1.GroupStatus == "11" || cc51Cartona1.GroupStatus == size1)
                    FeedCarton(cartonErector1, size1);
            }
            if (cartonErector2.LoadCount <= 1 && cc52Cartona1.GroupStatus != "00")
            {
                if (cc52Cartona1.GroupStatus == "11" || cc52Cartona1.GroupStatus == size2)
                    FeedCarton(cartonErector2, size2);
            }
            if (cartonErector3.LoadCount <= 1 && cc53Cartona1.GroupStatus != "00")
            {
                if (cc53Cartona1.GroupStatus == "11" || cc53Cartona1.GroupStatus == size3)
                    FeedCarton(cartonErector3, size3);
            }
        }

        private void FeedCarton(StraightConveyor cartonErector, string size)
        {
            var caseData = new CaseData { Length = 0.580f, Width = 0.480f, Height = 0.365f, colour = Color.Peru, Weight = 0 };

            if (size == "10")
            {
                //large
                caseData.CarrierSize = "CA00";
            }
            else if (size == "01")
            {
                //small
                caseData.CarrierSize = "CA01";
                caseData.Length = 0.490f;
                caseData.Width = 0.360f;
            }
            else
            {
                Log.Write($"Unknown carton size at carton erector ({cartonErector.Name}): {size}");
                return;
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

            equipmentStatuses.Add(cc51Cartona1 = new EquipmentStatus(plc51, "CC51CARTONA1", "11")); //CC11? 11 Both small and large cartons are available.
            cartonErectorSize["CC51CARTONA1"] = "10"; //Large          
            equipmentStatuses.Add(cc52Cartona1 = new EquipmentStatus(plc52, "CC52CARTONA1", "11")); //CC11? 11 Both small and large cartons are available.
            cartonErectorSize["CC52CARTONA1"] = "10"; //Large
            equipmentStatuses.Add(cc53Cartona1 = new EquipmentStatus(plc53, "CC53CARTONA1", "11")); //CC11? 11 Both small and large cartons are available.
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
            Environment.Scene.OnLoaded -= Scene_OnLoaded;
            activePoints = Core.Assemblies.Assembly.Items.Values.Where(a => a.Assemblies != null).SelectMany(a => a.Assemblies)
                    .OfType<Catalog.Dematic.Case.Devices.CommunicationPoint>()
                    .Where(c => c.Name.EndsWith("A1"))
                    .ToList();

            //Subscribe to photocell status change on all dispatch lanes and use the event to dispose the load
            foreach (var d in dispatchLanes)
            {
                if (d == null)
                    continue;

                d.beltControl.LineReleasePhotocell.OnPhotocellStatusChanged += Dispatch_LineReleasePhotocell_OnPhotocellStatusChanged;
            }

            var carton51 = Core.Assemblies.Assembly.Get("CARTON51");
            var carton52 = Core.Assemblies.Assembly.Get("CARTON52");
            var carton53 = Core.Assemblies.Assembly.Get("CARTON53");

            cc51Cartona1Comm = carton51.Assemblies.FirstOrDefault(a => a.Name == "CC51CARTONA1") as DematicCommunicationPoint;
            cc52Cartona1Comm = carton52.Assemblies.FirstOrDefault(a => a.Name == "CC52CARTONA1") as DematicCommunicationPoint;
            cc53Cartona1Comm = carton53.Assemblies.FirstOrDefault(a => a.Name == "CC53CARTONA1") as DematicCommunicationPoint;
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

            //Reset packing arrival
            foreach (var onlinePackingStation in onlinePackingStations.Keys.ToList())
            {
                onlinePackingStations[onlinePackingStation] = -1;
            }

            carton51Labeler.Reset();
            carton52Labeler.Reset();
            carton53Labeler.Reset();

            carton61Labeler1.Reset();
            carton61Labeler2.Reset();

            base.Reset();
        }

        protected override void Arriving(INode node, Load load)
        {
            if (node.Name.StartsWith("ROUTETO:"))
            {
                CheckProductBarcodeSuffix(load);
                var dest = node.Name.Replace("ROUTETO:", "");
                if (dest.StartsWith("PICK"))
                {
                    var picknumber = int.Parse(dest.Substring(4, 2));
                    var plc = plc51;
                    if (picknumber > 8 && picknumber <= 16)
                        plc = plc52;
                    if (picknumber > 17)
                        plc = plc53;

                    //Set the destination so the case load will cross the main line
                    plc.RoutingTable[load.Identification] = dest;

                }
                return;
            }
            if (node.Name == "CC63ETLNP1")
            {
                CheckProductBarcodeSuffix(load);
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
            if (node.Name == "CC61ECFA1")
            {
                HandleFailedCartons(plc61, node.Name, load);
                return;
            }
            if (node.Name == "CC61ECFA2")
            {
                HandleFailedCartons(plc61, node.Name, load);
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
            if (node.Name == "CC51CARTONSWAP" || node.Name == "CC52CARTONSWAP" || node.Name == "CC53CARTONSWAP")
            {
                //Apply barcode, set profile and carrier size
                AddBarcodeAndData(node.Name, load);
                AddBarcode2(load);
                SetCarrierSizeAfterCartonErector(load);
                AddProfile(load);
                return;
            }
            if (node.Name == "CC61LPA1")
            {
                AddDispatchLabel(load, carton61Labeler1);
                return;
            }
            if (node.Name == "CC61LPA2")
            {
                AddDispatchLabel(load, carton61Labeler2);
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
                return;
            }
            var ap = node as ActionPoint;
            if (ap != null && onlinePackingStations.ContainsKey(ap))
            {
                //Arrived at online pack station. Set arrival timestamp 
                onlinePackingStations[ap] = Environment.Time.Simulated;
            }
        }

        private void AddDispatchLabel(Load load, ZplLabeler labeler)
        {
            //var barcode2 = labeler.GetNextValidBarcode();
            var barcode2 = load.Identification; 
            var caseload = load as Case_Load;
            var casedata = caseload?.Case_Data as CaseData;
            if (casedata != null)
            {
                casedata.Barcode2 = barcode2;
            }
        }

        private void CheckProductBarcodeSuffix(Load load)
        {
            //Check barcode suffix for totes coming out of MS
            var barcode = load.Identification;
            if (barcode.StartsWith("9") && barcode.Length == 7)
            {
                //Add suffix 1 or 2
                if (Environment.Random.Next(0, 2) == 0)
                {
                    load.Identification = barcode + "1";
                }
                else
                {
                    load.Identification = barcode + "2";
                }
            }
        }

        private void RequestValidBarcodes()
        {
            //MRP: Changed to use ZPL script

            //if (emulationController.CartonBarcodesNeeded)
            //{
            //    emulationController.SendCartonBarcodesRequest("CARTONERECTION", 50);
            //}
        }

        private static void AddLid(Load load)
        {
            var part = load.Part;
            var box = new BoxPart(part.Length, part.Height, part.Width, part.Density, part.Color, Core.Loads.Load.Rigids.Cube)
            {
                Position = part.Position,
                Orientation = part.Orientation
            };
            load.Part = box;
            part.Dispose();
        }

        private static void AddBarcode2(Load load)
        {
            var caseLoad = load as Case_Load;

            var caseData = caseLoad?.Case_Data as CaseData;
            if (caseData == null)
                return;

            var barcode = load.Identification;

            if (barcode.StartsWith("9") && barcode.Length == 8)
            {
                //Product tote. Add suffix 1 or 2
                if (barcode.EndsWith("1"))
                {
                    caseData.Barcode2 = barcode.Substring(0, 7) + "2";
                }
                else
                {
                    caseData.Barcode2 = barcode.Substring(0, 7) + "1";
                }
            }
            else
            {
                //Cartons have the same barcode 1 and 2
                caseData.Barcode2 = barcode;
            }
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

        private static void SetCarrierSizeAfterCartonErector(Load load)
        {
            var caseLoad = load as Case_Load;

            var caseData = caseLoad?.Case_Data as CaseData;
            if (caseData == null)
                return;

            //Carton
            if (load.Length >= 0.58f)
            {
                //Large
                caseData.CarrierSize = "CA02";
            }
            else
            {
                //Small (medium?)
                caseData.CarrierSize = "CA01";
            }
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

        private void AddBarcodeAndData(string location, Load load)
        {
            if (location.StartsWith("CC51"))
                load.Identification = carton51Labeler.GetNextValidBarcode();
            else if (location.StartsWith("CC52"))
                load.Identification = carton52Labeler.GetNextValidBarcode();
            else
                load.Identification = carton53Labeler.GetNextValidBarcode();

            var caseLoad = load as Case_Load;

            var caseData = caseLoad?.Case_Data as CaseData;
            if (caseData == null)
                return;

            if (caseData.CarrierSize == "CA00") //CA00 is reported at CARTONA1 for large carton
                caseData.CarrierSize = "CA02"; //this is the carton size for large cartons
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
            Environment.Scene.Reset();

            foreach (var connection in Core.Communication.Connection.Items.Values)
            {
                connection.Disconnect();
            }
        }

        public override void Dispose()
        {
            Environment.UI.Toolbar.Remove(speed1);
            Environment.UI.Toolbar.Remove(speed2);
            Environment.UI.Toolbar.Remove(speed5);
            Environment.UI.Toolbar.Remove(speed10);
            Environment.UI.Toolbar.Remove(speed20);

            Environment.UI.Toolbar.Remove(reset);
            Environment.UI.Toolbar.Remove(fps1);
            Environment.UI.Toolbar.Remove(localProp);
            Environment.UI.Toolbar.Remove(connectButt);
            Environment.UI.Toolbar.Remove(disconnectButt);
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
                plc61.OnTransportOrderTelegramReceived -= OnTransportOrderTelegramReceived61;
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
            cartonErectorResendTimer.OnElapsed -= CartonErectorResendTimer_OnElapsed;
            cartonErectorResendTimer.Dispose();
            lidnp2Resend.OnElapsed -= Lidnp2Resend_Elapsed;
            lidnp2Resend.Dispose();
            onlinePackingStationTimer.OnElapsed -= OnlinePackingStationTimer_OnElapsed;
            onlinePackingStationTimer.Dispose();
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