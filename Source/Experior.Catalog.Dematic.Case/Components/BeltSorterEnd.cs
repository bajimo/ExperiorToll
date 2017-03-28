using Experior.Catalog.Dematic.Case.Devices;
using Experior.Core.Assemblies;
using Experior.Core.Loads;
using Experior.Core.Properties;
using System;
using System.ComponentModel;
using System.Xml.Serialization;

namespace Experior.Catalog.Dematic.Case.Components
{
    public class BeltSorterEnd : StraightBeltConveyor
    {
        public BeltSorterEndInfo beltSorterEndInfo;


        #region Constructors

        public BeltSorterEnd(BeltSorterEndInfo info): base(info)
        {
            beltSorterEndInfo = info;
        }
        #endregion

        #region User Interface

        #endregion

    }

    [Serializable]
    [XmlInclude(typeof(StraightBeltConveyorInfo))]
    public class BeltSorterEndInfo : StraightBeltConveyorInfo
    {
    }
}
