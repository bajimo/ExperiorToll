using System.Drawing;
using Experior.Core;


namespace Experior.Catalog.Dematic.DatcomUK
{
    public class Controllers : Experior.Core.Catalog
    {
        public Controllers() : base("DATCOM UK Controllers")
        {

            Simulation = Experior.Core.Environment.Simulation.Events | Experior.Core.Environment.Simulation.Physics;

            Common.Meshes = new Experior.Core.Resources.Meshes(System.Reflection.Assembly.GetExecutingAssembly());
            Common.Icons = new Experior.Core.Resources.Icons(System.Reflection.Assembly.GetExecutingAssembly());

            Add(Common.Icons.Get("PLC"), "DATCOM UK PLC", "BK10", Simulation);
            //Add(Common.Icons.Get("PLC"), "MSC", "Captive (V3.3)", Core.Environment.Simulation.Events);
            Add(Common.Icons.Get("PLC"), "DATCOM UK MultiShuttle", "V3.4", Core.Environment.Simulation.Events);

            //Add(Common.Icons.Get("DematicLogo"), "DEMATIC Logo", "", Simulation);
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
