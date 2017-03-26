
using Experior.Catalog.Dematic.SimulationUK.Assemblies;
using Experior.Core.Assemblies;

namespace Experior.Catalog.Dematic.SimulationUK
{
    //internal class Common
    //{
    //    public static Experior.Core.Resources.Meshes Meshes;
    //    public static Experior.Core.Resources.Icons Icons;
    //}

    public class ConstructAssembly
    {
        public static Assembly CreateAssembly(string title, string subtitle/*, object properties*/)
        {
            //Experior.Catalog.Assemblies.MyAssemblyInfo info = new Experior.Catalog.Assemblies.MyAssemblyInfo();
            //info.name = Experior.Core.Assemblies.Assembly.GetValidName("MyAssembly");
            //Experior.Catalog.Assemblies.MyAssembly assembly = new Experior.Catalog.Assemblies.MyAssembly(info);

            Experior.Core.Assemblies.Assembly assembly = null;


            if (title == "Simulation UK")
            {
                SimulationUKInfo simulationInfo = new SimulationUKInfo();
                simulationInfo.name = Experior.Core.Assemblies.Assembly.GetValidName("Simulation ");
                simulationInfo.position.Y = 0;
                return new SimControllerUK(simulationInfo);
            }

            return assembly;
        }
    }
}