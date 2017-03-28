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
        public Controllers() : base("DCI Controllers")
        {

            Simulation = Experior.Core.Environment.Simulation.Events | Experior.Core.Environment.Simulation.Physics;

            Common.Meshes = new Experior.Core.Resources.Meshes(System.Reflection.Assembly.GetExecutingAssembly());
            Common.Icons = new Experior.Core.Resources.Icons(System.Reflection.Assembly.GetExecutingAssembly());

            Add(Common.Icons.Get("PLC"), "DCI MultiShuttle PLC", "", Simulation, Create.DCIMultiShuttlePLC);

        }
        public override Image Logo
        {
            get{ return Common.Icons.Get("dematic"); }
        }
    }
}
