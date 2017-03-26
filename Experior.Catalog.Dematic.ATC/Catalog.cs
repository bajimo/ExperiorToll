using System.Drawing;

namespace Experior.Catalog.Dematic.ATC
{
    internal class Common
    {
        public static Experior.Core.Resources.Meshes Meshes;
        public static Experior.Core.Resources.Icons Icons;
    }

    public class Controllers : Experior.Core.Catalog
    {
        public Controllers() : base("ATC Controllers")
        {

            Simulation = Experior.Core.Environment.Simulation.Events | Experior.Core.Environment.Simulation.Physics;

            Common.Meshes = new Experior.Core.Resources.Meshes(System.Reflection.Assembly.GetExecutingAssembly());
            Common.Icons = new Experior.Core.Resources.Icons(System.Reflection.Assembly.GetExecutingAssembly());

            Add(Common.Icons.Get("PLC"), "ATC Sorter PLC", "", Simulation, Create.ATCSorterPLC);
            Add(Common.Icons.Get("PLC"), "ATC Case PLC", "", Simulation, Create.ATCCasePLC);
            Add(Common.Icons.Get("PLC"), "ATC Emulation", "", Simulation, Create.ATCEmulation);
            Add(Common.Icons.Get("PLC"), "ATC MultiShuttle PLC", "", Simulation, Create.ATCMultiShuttlePLC);
            Add(Common.Icons.Get("PLC"), "ATC Miniload PLC", "", Simulation, Create.ATCMiniloadPLC);
            Add(Common.Icons.Get("PLC"), "ATC Pallet Crane PLC", "", Simulation, Create.ATCPalletCranePLC);
            Add(Common.Icons.Get("PLC"), "ATC Pallet PLC", "", Simulation, Create.ATCPalletPLC);

        }
        public override Image Logo
        {
            get{ return Common.Icons.Get("dematic"); }
        }
    }
}
