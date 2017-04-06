
using Experior.Catalog.Dematic.DatcomAUS.Assemblies;
using Experior.Core.Assemblies;

namespace Experior.Catalog.Dematic.DatcomAUS
{
    internal class Common
    {
        public static Experior.Core.Resources.Meshes Meshes;
        public static Experior.Core.Resources.Icons Icons;
    }

    public class ConstructAssembly
    {
        public static Assembly CreateDatcomAusPlc(string title, string subtitle, object properties)
        {
            CaseDatcomAusInfo plcinfo = new CaseDatcomAusInfo();
            plcinfo.name = Assembly.GetValidName("PLC ");
            plcinfo.position.Y = 0;
            return new MHEControllerAUS_Case(plcinfo);
        }
    }
}