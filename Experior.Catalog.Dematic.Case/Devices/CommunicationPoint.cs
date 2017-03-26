using Experior.Catalog.Logistic.Track;
using Experior.Dematic.Base.Devices;
using System;
using System.Xml.Serialization;

namespace Experior.Catalog.Dematic.Case.Devices
{
    /// <summary>
    /// This is a generic type of communication point. When this is added to a conveyor
    /// and the PLC is set then the correct properties will be avaiable
    /// </summary>
    public class CommunicationPoint : DematicCommunicationPoint
    {
        public CommunicationPoint(CommunicationPointInfo info, BaseTrack conv): base(info, conv)
        {
        }
    }

    [Serializable]
    [XmlInclude(typeof(CommunicationPointInfo))]
    public class CommunicationPointInfo : DematicCommunicationPointInfo
    {
    }
}