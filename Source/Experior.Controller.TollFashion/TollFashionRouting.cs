using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Dematic.DATCOMAUS;
using Experior.Catalog.Dematic.Case.Components;
using Experior.Catalog.Dematic.DatcomAUS.Assemblies;
using Experior.Core.Loads;
using Experior.Core.Routes;
using Experior.Dematic.Base;

namespace Experior.Controller.TollFashion
{
    public partial class TollFashionRouting : Catalog.ControllerExtended
    {
        private readonly MHEControllerAUS_Case plc51, plc52, plc53, plc54, plc61, plc62, plc63;
        private readonly List<PickPutStation> rapidPickstations;
        private List<Catalog.Dematic.Case.Devices.CommunicationPoint> activePoints;
        private readonly List<EquipmentStatus> equipmentStatuses = new List<EquipmentStatus>();
        public IReadOnlyList<EquipmentStatus> EquipmentStatuses { get; private set; }
        public IReadOnlyList<string> PossibleStatuses { get; private set; } = new List<string>() { "00", "01", "10", "11" };
        private EmulationController emulationController;
        private readonly StraightAccumulationConveyor emptyToteLine;
        private int noBarcodesCount;

        public TollFashionRouting() : base("TollFashionRouting")
        {
            StandardConstructor();

            emulationController = new EmulationController();

            emptyToteLine = Core.Assemblies.Assembly.Get("P1963") as StraightAccumulationConveyor;

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
            }
            if (plc52 != null)
            {
                plc52.OnRequestAllDataTelegramReceived += OnRequestAllDataTelegramReceived;
                plc52.OnSetSystemStatusTelegramReceived += OnSetSystemStatusTelegramReceived;
            }
            if (plc53 != null)
            {
                plc53.OnRequestAllDataTelegramReceived += OnRequestAllDataTelegramReceived;
                plc53.OnSetSystemStatusTelegramReceived += OnSetSystemStatusTelegramReceived;
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
            CreateEquipmentStatuses();
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

            equipmentStatuses.Add(new EquipmentStatus(plc51, "CC51CARTONA1", "11")); //CC11? 11 Both small and large cartons are available.
            equipmentStatuses.Add(new EquipmentStatus(plc52, "CC52CARTONA1", "11")); //CC11? 11 Both small and large cartons are available.
            equipmentStatuses.Add(new EquipmentStatus(plc53, "CC53CARTONA1", "11")); //CC11? 11 Both small and large cartons are available.

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
            }
        }

        private void AddBarcodeAndData(Load load)
        {
            string nextBarcode;

            if (emulationController.ValidCartonErectionBarcodes.Any())
            {
                nextBarcode = emulationController.ValidCartonErectionBarcodes.Dequeue();
            }
            else
            {
                nextBarcode = $"noBarcodes {++noBarcodesCount}";
            }

            load.Identification = nextBarcode;

            var caseLoad = load as Case_Load;
            if (caseLoad == null)
                return;

            var caseData = caseLoad.Case_Data as CaseData;
            if (caseData == null)
                return;

            caseData.Barcode2 = nextBarcode;
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
                Core.Timer.Action(() => 
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
            var sBefore = eqStatus.GroupStatus;
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
            if (eqStatus.GroupStatus != sBefore)
            {
                plc63.SendEquipmentStatus(eqStatus.FunctionGroup, eqStatus.GroupStatus);
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
            Core.Environment.UI.Toolbar.Remove(Speed1);
            Core.Environment.UI.Toolbar.Remove(Speed2);
            Core.Environment.UI.Toolbar.Remove(Speed5);
            Core.Environment.UI.Toolbar.Remove(Speed10);
            Core.Environment.UI.Toolbar.Remove(Speed20);

            Core.Environment.UI.Toolbar.Remove(Reset);
            Core.Environment.UI.Toolbar.Remove(fps1);
            Core.Environment.UI.Toolbar.Remove(localProp);
            Core.Environment.UI.Toolbar.Remove(connectButt);
            Core.Environment.UI.Toolbar.Remove(disconnectButt);
            if (plc51 != null)
            {
                plc51.OnRequestAllDataTelegramReceived -= OnRequestAllDataTelegramReceived;
                plc51.OnSetSystemStatusTelegramReceived -= OnSetSystemStatusTelegramReceived;
            }
            if (plc52 != null)
            {
                plc52.OnRequestAllDataTelegramReceived -= OnRequestAllDataTelegramReceived;
                plc52.OnSetSystemStatusTelegramReceived -= OnSetSystemStatusTelegramReceived;
            }
            if (plc53 != null)
            {
                plc53.OnRequestAllDataTelegramReceived -= OnRequestAllDataTelegramReceived;
                plc53.OnSetSystemStatusTelegramReceived -= OnSetSystemStatusTelegramReceived;
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
            base.Dispose();
        }
    }

    public class EquipmentStatus
    {
        public MHEControllerAUS_Case Plc { get; private set; }
        public string FunctionGroup { get; private set; }
        public string GroupStatus { get; set; }

        public EquipmentStatus(MHEControllerAUS_Case plc, string functionGroup, string groupStatus = "00")
        {
            Plc = plc;
            FunctionGroup = functionGroup;
            GroupStatus = groupStatus;
        }
    }
}