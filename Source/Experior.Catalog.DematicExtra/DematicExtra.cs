using System.Drawing;
using Experior.Catalog.Extras;

namespace Experior.Catalog {

    public class DematicExtra : Experior.Core.Catalog {

        public DematicExtra()
            : base("DematicExtra")
        {
            Simulation = Experior.Core.Environment.Simulation.Events;

            Common.Meshes = new Experior.Core.Resources.Meshes(System.Reflection.Assembly.GetExecutingAssembly());
            Common.Icons = new Experior.Core.Resources.Icons(System.Reflection.Assembly.GetExecutingAssembly());

           // Experior.Core.Catalog.Load("Experior.Catalog.Logistic.Basic.dll");

            Add(Common.Icons.Get("KUKAKR180"), "KUKA KR180", "", Experior.Core.Environment.Simulation.Events);
            Add(Common.Icons.Get("Man1"), "Man1", "", Experior.Core.Environment.Simulation.Events);
            Add(Common.Icons.Get("Man2"), "Man2", "", Experior.Core.Environment.Simulation.Events);
            Add(Common.Icons.Get("Text"), "Text", "", Experior.Core.Environment.Simulation.Events);
            Add(Common.Icons.Get("DematicLogo"), "Logo", "", Simulation);
            Add(Common.Icons.Get("Text"), "3D Text", "", Simulation);
            //Add(Common.Icons.Get("logo"), "Image", "", Simulation);
            Add(Common.Icons.Get("Box"), "Box", "", Simulation);

        }

        public override Core.Assemblies.Assembly Construct(string title, string subtitle, object properties) {         
            return ConstructAssembly.CreateAssembly(title, subtitle);         
        }

        public override Image Logo {
            get {return Common.Icons.Get("dematic");}                            
        }

    }
}
