using System.Drawing;

namespace Experior.Catalog.Dematic.DatcomAUS
{
    public class Controllers : Core.Catalog
    {
        public Controllers() : base("DATCOM AUS Controllers")
        {
            Simulation = Core.Environment.Simulation.Events | Core.Environment.Simulation.Physics;
            Common.Meshes = new Core.Resources.Meshes(System.Reflection.Assembly.GetExecutingAssembly());
            Common.Icons = new Core.Resources.Icons(System.Reflection.Assembly.GetExecutingAssembly());
            Add(Common.Icons.Get("PLC"), "DATCOM AUS PLC", "BK10", Simulation, ConstructAssembly.CreateDatcomAusPlc);
        }
        public override Image Logo
        {
            get{ return Common.Icons.Get("dematic"); }
        }
    }
}