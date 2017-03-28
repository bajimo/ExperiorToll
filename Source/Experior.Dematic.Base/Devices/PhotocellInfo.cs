using Experior.Core.Assemblies;
using System;
using System.Xml.Serialization;

namespace Experior.Dematic.Base.Devices
{
    [Serializable]
    [XmlInclude(typeof(PhotocellInfo))]
    public class PhotocellInfo : DeviceInfo
    {
        public PositionPoint distanceFrom = PositionPoint.End;
        public PhotocellState photocellStatus = PhotocellState.Clear;
        public float blockedTimeout = 0;
        public float clearTimeout = 0;
        public bool routingEvent = false;

        public override void SetCustomInfoFields(Assembly assem, object obj, ref DeviceInfo info)
        {
            info.length = ((IConstructDevice)assem).Length;
            info.width = ((IConstructDevice)assem).Width;
        }
    }
}
