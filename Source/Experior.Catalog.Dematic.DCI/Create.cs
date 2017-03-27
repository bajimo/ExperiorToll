using Experior.Catalog.Dematic.DCI.Assemblies.Storage;
using Experior.Core.Assemblies;

namespace Experior.Catalog.Dematic.ATC
{
    public class Create
    {
        public static Assembly DCIMultiShuttlePLC(string title, string subtitle, object properties)
        {
            MHEController_MultishuttleDCIInfo plcinfo = new MHEController_MultishuttleDCIInfo();
            plcinfo.name = Experior.Core.Assemblies.Assembly.GetValidName("MSC ");
            return new MHEController_Multishuttle(plcinfo);
        }
    }
}
