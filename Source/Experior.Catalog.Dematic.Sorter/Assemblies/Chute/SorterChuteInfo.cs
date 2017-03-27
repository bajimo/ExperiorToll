using System;
using Experior.Catalog.Dematic.Case.Components;

namespace Experior.Catalog.Dematic.Sorter.Assemblies.Chute
{
    [Serializable]
    public class SorterChuteInfo : StraightConveyorInfo
    {
        //Time before Ready again after discharge
        public float RecoverTime { get; set; }

        public SorterChuteInfo()
        {
            //Default values
            RecoverTime = 1;
        }
    }
}
