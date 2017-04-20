using System.Collections.Generic;
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
        public IReadOnlyList<string> PossibleStatuses { get; private set; } = new List<string>() {"00", "01", "10", "11"};

        public TollFashionRouting() : base("TollFashionRouting")
        {
            StandardConstructor();

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
            Core.Environment.Scene.OnResetCompleted += Scene_OnResetCompleted;
            Core.Environment.Scene.OnLoaded += Scene_OnLoaded;
            CreateEquipmentStatuses();
        }

        private void CreateEquipmentStatuses()
        {
            equipmentStatuses.Add(new EquipmentStatus(plc54, "CC54LOOP", "11"));
            equipmentStatuses.Add(new EquipmentStatus(plc51, "CC51CARTON", "11")); //TODO check name and status
            equipmentStatuses.Add(new EquipmentStatus(plc52, "CC52CARTON", "11"));//TODO check name and status
            equipmentStatuses.Add(new EquipmentStatus(plc53, "CC53CARTON", "11"));//TODO check name and status
            equipmentStatuses.Add(new EquipmentStatus(plc51, "CC51CBUFFI", "11"));//TODO check name and status
            equipmentStatuses.Add(new EquipmentStatus(plc52, "CC52CBUFFI", "11"));//TODO check name and status
            equipmentStatuses.Add(new EquipmentStatus(plc53, "CC53CBUFFI", "11"));//TODO check name and status

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

        void Scene_OnResetCompleted()
        {

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
            Core.Environment.Scene.OnResetCompleted -= Scene_OnResetCompleted;
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

        public EquipmentStatus(MHEControllerAUS_Case plc, string functionGroup, string groupStatus)
        {
            Plc = plc;
            FunctionGroup = functionGroup;
            GroupStatus = groupStatus;
        }
    } 
}