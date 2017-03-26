using Experior.Catalog.Logistic.Track;
using Experior.Dematic.Base.Devices;
using System;
using System.Xml.Serialization;

namespace Experior.Dematic.Pallet.Devices
{
    /// <summary>
    /// This is a generic type of communication point. When this is added to a conveyor
    /// and the PLC is set then the correct properties will be avaiable
    /// </summary>
    public class PalletCommunicationPoint : DematicCommunicationPoint
    {
        public PalletCommunicationPoint(PalletCommunicationPointInfo info, BaseTrack conv) : base(info, conv)
        {
        }
    }

    [Serializable]
    [XmlInclude(typeof(PalletCommunicationPointInfo))]
    public class PalletCommunicationPointInfo : DematicCommunicationPointInfo
    {
    }
}