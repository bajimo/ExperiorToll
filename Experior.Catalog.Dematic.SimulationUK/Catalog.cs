using System.Drawing;
using Experior.Core;

namespace Experior.Catalog.Dematic.SimulationUK
{
    internal class Common
    {
        public static Experior.Core.Resources.Meshes Meshes;
        public static Experior.Core.Resources.Icons Icons;
    }

    public class Controllers : Experior.Core.Catalog
    {
        public Controllers() : base("Simulation UK Controllers")
        {

            Simulation = Experior.Core.Environment.Simulation.Events | Experior.Core.Environment.Simulation.Physics;

            Common.Meshes = new Experior.Core.Resources.Meshes(System.Reflection.Assembly.GetExecutingAssembly());
            Common.Icons = new Experior.Core.Resources.Icons(System.Reflection.Assembly.GetExecutingAssembly());

            Add(Common.Icons.Get("Globe"), "Simulation UK", "", Simulation);
        }
        public override Image Logo
        {
            get{ return Common.Icons.Get("dematic"); }
        }

        public override Experior.Core.Assemblies.Assembly Construct(string title, string subtitle, object properties)
        {         
            return ConstructAssembly.CreateAssembly(title, subtitle);   
        }

    }
}
