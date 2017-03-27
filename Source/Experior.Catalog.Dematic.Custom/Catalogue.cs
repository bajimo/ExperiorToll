using System.Drawing;
using Experior.Core;
using Experior.Core.Assemblies;
using Experior.Core.Resources;
using Experior.Dematic.Base;


namespace Experior.Catalog.Dematic.Custom {

    internal class Common
    {
        public static Experior.Core.Resources.Meshes Meshes;
        public static Experior.Core.Resources.Icons Icons;
    }

    public class Conveyors : Core.Catalog {

        public Conveyors()  : base("Custom")
        {            
            Environment.Engine.RoutingGraph = false;

            Simulation = Environment.Simulation.Events;

            Common.Meshes = new Meshes(System.Reflection.Assembly.GetExecutingAssembly());
            Common.Icons = new Icons(System.Reflection.Assembly.GetExecutingAssembly());

            Dependencies.Add("Experior.Catalog.Logistic.Basic.dll");
            Dependencies.Add("Experior.Catalog.Logistic.Track.dll");

            Add(Common.Icons.Get("accumulation"), "Conveyor Units", Environment.Simulation.Events);
            Add(Common.Icons.Get("3way"), "Transfer", "3 Way", Environment.Simulation.Events);
        }

        public override Assembly Construct(string title, string subtitle, object properties) {         
            return ConstructAssembly.CreateAssembly(title, subtitle);         
        }

        public override Image Logo {
            get {return Common.Icons.Get("dematic");}                            
        }

    }
}
