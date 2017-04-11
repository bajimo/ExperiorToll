using System.Collections.Generic;
using System.Linq;
using Experior.Catalog.Dematic.Case.Components;
using Experior.Catalog.Dematic.DatcomAUS.Assemblies;
using Experior.Core.Loads;
using Experior.Core.Routes;
using Experior.Dematic.Base;
using Experior.Dematic.Base.Devices;

namespace Experior.Controller.TollFashion
{
    public partial class TollFashionRouting : Catalog.ControllerExtended
    {
        private MHEControllerAUS_Case plc51, plc52, plc53;
        private List<PickPutStation> rapidPickstations;
        private List<Catalog.Dematic.Case.Devices.CommunicationPoint> activePoints;

        public TollFashionRouting() : base("TollFashionRouting")
        {
            StandardConstructor();

            plc51 = Core.Assemblies.Assembly.Get("PLC 51") as MHEControllerAUS_Case;
            plc52 = Core.Assemblies.Assembly.Get("PLC 52") as MHEControllerAUS_Case;
            plc53 = Core.Assemblies.Assembly.Get("PLC 53") as MHEControllerAUS_Case;
            rapidPickstations = Core.Assemblies.Assembly.Items.Values.OfType<PickPutStation>().ToList();
  
            if (plc51 != null)
            {
                plc51.OnRequestAllDataTelegramReceived += OnRequestAllDataTelegramReceived;
            }
            if (plc52 != null)
            {
                plc52.OnRequestAllDataTelegramReceived += OnRequestAllDataTelegramReceived; ;
            }
            if (plc53 != null)
            {
                plc53.OnRequestAllDataTelegramReceived += OnRequestAllDataTelegramReceived; ;
            }

            Core.Environment.Time.ContinuouslyRunning = true;
            Core.Environment.Scene.OnResetCompleted += Scene_OnResetCompleted;
            Core.Environment.Scene.OnLoaded += Scene_OnLoaded;
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

            //if (node.Name.EndsWith("A1", false, CultureInfo.InvariantCulture))
            if (node.Name.Contains("CARTONA1"))
            {
                //Active locations
                var caseLoad = load as Case_Load;
                if (caseLoad != null)
                {
                    caseLoad.LoadWaitingForWCS = true;
                    caseLoad.StopLoad();
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
            }
            if (plc52 != null)
            {
                plc52.OnRequestAllDataTelegramReceived -= OnRequestAllDataTelegramReceived; ;
            }
            if (plc53 != null)
            {
                plc53.OnRequestAllDataTelegramReceived -= OnRequestAllDataTelegramReceived; ;
            }
            base.Dispose();
        }
    }
}