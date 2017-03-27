using Experior.Core.Assemblies;
using System;
using System.Xml.Serialization;

namespace Experior.Dematic.Base.Devices
{
    [Serializable]
    [XmlInclude(typeof(PhotocellDisplayInfo))]
    public class PhotocellDisplayInfo : AssemblyInfo
    {
        public PositionPoint distanceFrom;
    }
}
