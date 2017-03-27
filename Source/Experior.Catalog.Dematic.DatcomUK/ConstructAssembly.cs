
using Experior.Catalog.Dematic.DatcomUK.Assemblies;
using Experior.Core.Assemblies;
using Experior.Dematic.Base;

namespace Experior.Catalog.Dematic.DatcomUK
{
    internal class Common
    {
        public static Experior.Core.Resources.Meshes Meshes;
        public static Experior.Core.Resources.Icons Icons;
    }

    public class ConstructAssembly
    {
        public static Assembly CreateAssembly(string title, string subtitle/*, object properties*/)
        {
            //Experior.Catalog.Assemblies.MyAssemblyInfo info = new Experior.Catalog.Assemblies.MyAssemblyInfo();
            //info.name = Experior.Core.Assemblies.Assembly.GetValidName("MyAssembly");
            //Experior.Catalog.Assemblies.MyAssembly assembly = new Experior.Catalog.Assemblies.MyAssembly(info);

            Experior.Core.Assemblies.Assembly assembly = null;

            if (title == "DATCOM UK PLC")
            {
                CaseDatcomInfo plcinfo = new CaseDatcomInfo();
                plcinfo.name = Experior.Core.Assemblies.Assembly.GetValidName("PLC ");
                plcinfo.position.Y = 0;
                return new CasePLC_Datcom(plcinfo);
            }
            //else if (title == "DEMATIC Logo")
            //{
            //    DematicLogoInfo logoinfo = new DematicLogoInfo();
            //    logoinfo.name = Experior.Core.Assemblies.Assembly.GetValidName("DEMATIC Logo ");
            //    return new Dematic_Logo(logoinfo);
            //}
            else if (title == "DATCOM UK MultiShuttle")
            {
                MHEController_MultishuttleInfo mHEController_MultishuttleInfo = new MHEController_MultishuttleInfo();
                mHEController_MultishuttleInfo.name = Experior.Core.Assemblies.Assembly.GetValidName("PLC ");
                return new MHEController_Multishuttle(mHEController_MultishuttleInfo);
            }
            //else if (title == "MSC")
            //{
            //    MultiShuttleControllerInfo info = new MultiShuttleControllerInfo();
            //    info.name = Experior.Core.Assemblies.Assembly.GetValidName("MSC " + subtitle + " ");
            //    return new MultiShuttleController(info);
            //}

            return assembly;
        }
    }
}