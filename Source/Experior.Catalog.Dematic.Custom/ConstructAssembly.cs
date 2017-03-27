//using Experior.Catalog.Dematic.Case.Components;
//using Experior.Catalog.Dematic.Custom.Components;
//using Experior.Core.Assemblies;


using Experior.Catalog.Dematic.Custom.Components;
using Experior.Core.Assemblies;
namespace Experior.Catalog.Dematic.Custom
{

    //internal class Common {
    //    public static Experior.Core.Resources.Meshes Meshes;
    //    public static Experior.Core.Resources.Icons Icons;
    //}

    public class ConstructAssembly
    {
        public static Assembly CreateAssembly(string type, string subtitle)
        {
          
            if (type == "Conveyor Units")
            {
                StraightAccumulationConveyorUnitsInfo straightAccumulationUnitsinfo = new StraightAccumulationConveyorUnitsInfo();
                straightAccumulationUnitsinfo.thickness = 0.05f;
                straightAccumulationUnitsinfo.speed = 0.7f;
                straightAccumulationUnitsinfo.width = 0.5f;
                straightAccumulationUnitsinfo.height = 0.7f;
                straightAccumulationUnitsinfo.name = Experior.Core.Assemblies.Assembly.GetValidName("CU ");
                return new StraightAccumulationConveyorUnits(straightAccumulationUnitsinfo);                       
            }
            else if (type == "Transfer")
            {
                ThreeWaySwitchInfo transferInfo = new ThreeWaySwitchInfo();
                transferInfo.height = 0.7f;
                //transferInfo.speed = 0.7f;

               if (subtitle == "3 Way")
                {
                    ThreeWaySwitchInfo threeWayInfo = new ThreeWaySwitchInfo();
                    threeWayInfo.height = 0.7f;
                    threeWayInfo.length = 3;
                    threeWayInfo.width = 1.5f;
                    //transferInfo.internalConvWidth = CaseConveyorWidth._500mm;

                    transferInfo.name = Experior.Core.Assemblies.Assembly.GetValidName("3Way ");
                    return new ThreeWaySwitch(threeWayInfo);
                }
             
            }
           
            return null;
        }
    }
}

