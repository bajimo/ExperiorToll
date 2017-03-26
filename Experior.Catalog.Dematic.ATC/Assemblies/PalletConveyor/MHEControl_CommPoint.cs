using Experior.Core.Loads;
using Experior.Core.Properties;
using Experior.Dematic.Base;
using Experior.Dematic.Base.Devices;
using Experior.Dematic.Pallet.Devices;
using System;
using System.ComponentModel;
using System.Xml.Serialization;

namespace Experior.Catalog.Dematic.ATC.Assemblies.PalletConveyor
{
    /// <summary>
    /// This is your MHE control class it is instansuated by the controller (PLC etc) and passed back to the communicationPoint
    /// it controlls the MHE and it the routing and decession making processes of the MHE.
    /// </summary>
    public class MHEControl_PalletCommPoint : MHEControl
    {
        private PalletCommPointATCInfo commPointATCInfo;
        private PalletCommunicationPoint commPoint;
        private MHEController_Pallet casePLC;

        #region Constructors

        public MHEControl_PalletCommPoint(PalletCommPointATCInfo info, PalletCommunicationPoint cPoint)
        {
            commPoint = cPoint;
            commPointATCInfo = info;
            Info = info;  // set this to save properties 
            commPoint.commPointArrival = ap_Enter; //CommunicationPoint will use this delegate for ap_enter.
            casePLC = CommPoint.Controller as MHEController_Pallet;
        }

        #endregion

        public void ap_Enter(DematicCommunicationPoint sender, Load load)
        {
            IATCPalletLoadType palletLoad = load as IATCPalletLoadType;
            if (palletLoad == null)
            {
                return;
            }

            palletLoad.Location = commPoint.Name; //Update the palletLoad location

            switch (CommPointType)
            {
                case PalletCommPointATCInfo.PalletCommPointATCTypes.None: break;
                case PalletCommPointATCInfo.PalletCommPointATCTypes.LocationArrived: LocationArrived(CommPoint, palletLoad); break;
                case PalletCommPointATCInfo.PalletCommPointATCTypes.LocationLeft: LocationLeft(commPoint, palletLoad); break;
                case PalletCommPointATCInfo.PalletCommPointATCTypes.TransportRequest: TransportRequest(commPoint, palletLoad); break;
                case PalletCommPointATCInfo.PalletCommPointATCTypes.TransportFinished: TransportFinished(commPoint, palletLoad); break;
                case PalletCommPointATCInfo.PalletCommPointATCTypes.TransportRequestOrFinished: TransportRequestOrFinished(commPoint, palletLoad); break;
                default: break;
            }
        }

        private void LocationArrived(PalletCommunicationPoint commPoint, IATCPalletLoadType palletLoad)
        {
            if (AlwaysArrival || palletLoad.Location == palletLoad.Destination)
            {
                palletLoad.Location = commPoint.Name;
                //palletLoad.MTS = commPoint.ControllerName;
                casePLC.SendLocationArrivedTelegram(palletLoad as IATCPalletLoadType);
            }
        }

        private void LocationLeft(PalletCommunicationPoint commPoint, IATCPalletLoadType palletLoad)
        {
            if (AlwaysArrival || palletLoad.Location == palletLoad.Destination)
            {
                palletLoad.Location = commPoint.Name;
                //palletLoad.MTS = commPoint.ControllerName;
                casePLC.SendLocationLeftTelegram(palletLoad as IATCPalletLoadType);
            }
        }

        //TODO: For the loads that are stopping on the case conveyor at these communication points it
        //has to be placed in the correct position otherwise its can be an issue (I have found that placing 
        //them in exactly the correct place compaired to a accumulation sensor is a good position as all atction point are triggered
        private void TransportRequest(PalletCommunicationPoint commPoint, IATCPalletLoadType palletLoad)
        {
            if (AlwaysArrival || palletLoad.Location == palletLoad.Destination || string.IsNullOrEmpty(palletLoad.Destination))
            {
                palletLoad.Location = commPoint.Name;
                //palletLoad.MTS = commPoint.ControllerName;
                casePLC.SendTransportRequestTelegram(palletLoad);

                if (LoadWait)
                {
                    //[BG] Not happy with this, if this is used then it needs to be understood what the issues are with it 
                    //(Like stopping on a belt is bad and you need to put the Comm Point in the right place on accumulation conveyor)
                    palletLoad.LoadWaitingForWCS = true;
                    palletLoad.Stop();

                }
            }
        }

        private void TransportFinished(PalletCommunicationPoint commPoint, IATCPalletLoadType palletLoad)
        {
            if (AlwaysArrival || palletLoad.Location == palletLoad.Destination)
            {
                palletLoad.Location = commPoint.Name;
                casePLC.SendTransportFinishedTelegram(palletLoad);

                if (LoadWait)
                {
                    palletLoad.LoadWaitingForWCS = true;
                    palletLoad.Stop();
                }
            }
        }

        private void TransportRequestOrFinished(PalletCommunicationPoint commPoint, IATCPalletLoadType palletLoad)
        {
            if (palletLoad.Location == palletLoad.Destination)
            {
                //Send TransportFinishedTelegram
                palletLoad.Location = commPoint.Name;
                casePLC.SendTransportFinishedTelegram(palletLoad);
            }
            else
            {
                //Send TransportRequestTelegram
                palletLoad.Location = commPoint.Name;
                casePLC.SendTransportRequestTelegram(palletLoad);
            }

            if (LoadWait)
            {
                palletLoad.LoadWaitingForWCS = true;
                palletLoad.Stop();
            }
        }

        #region Properties

        [Browsable(false)]
        public PalletCommunicationPoint CommPoint
        {
            get { return commPoint; }
            set { commPoint = value; }
        }

        #region user interface

        public void DynamicPropertyAlwaysTelegram(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = (CommPointType == PalletCommPointATCInfo.PalletCommPointATCTypes.LocationArrived ||
                                      CommPointType == PalletCommPointATCInfo.PalletCommPointATCTypes.LocationLeft ||
                                      CommPointType == PalletCommPointATCInfo.PalletCommPointATCTypes.TransportRequest ||
                                      CommPointType == PalletCommPointATCInfo.PalletCommPointATCTypes.TransportFinished);
        }

        public void DynamicPropertyLoadWait(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = (CommPointType == PalletCommPointATCInfo.PalletCommPointATCTypes.TransportRequest ||
                                      CommPointType == PalletCommPointATCInfo.PalletCommPointATCTypes.TransportFinished || 
                                      CommPointType == PalletCommPointATCInfo.PalletCommPointATCTypes.TransportRequestOrFinished);
        }

        [Category("Configuration")]
        [DisplayName("Always Send Arrival")]
        [Description("If false the telegram will only be sent if the Location equals the Destination")]
        [PropertyAttributesProvider("DynamicPropertyAlwaysTelegram")]
        public bool AlwaysArrival
        {
            get { return commPointATCInfo.alwaysArrival; }
            set { commPointATCInfo.alwaysArrival = value; }
        }

        [Category("Configuration")]
        [DisplayName("Load Wait")]
        [Description("Should the load wait for a StartTransportTelegram before releasing from Comm Point")]
        [PropertyAttributesProvider("DynamicPropertyLoadWait")]
        [Experior.Core.Properties.AlwaysEditable]
        public bool LoadWait
        {
            get { return commPointATCInfo.loadWait; }
            set { commPointATCInfo.loadWait = value; }
        }

        [DisplayName("Type")]
        [DescriptionAttribute("None - No messaging.\n" +
          "LocationArrived - Send LocationArrivedTelegram when load arrives at the Comm Point\n" +
          "TransportRequest - Send TransportRequestTelegram when load arrives at the Comm Point\n" +
          "TransportFinished - Send TransportFinishedTelegram when load arrives at the Comm Point\n" +
          "TransportRequestOrFinished - Send TransportRequest When load does not have Comm Point as destination, or finished if it does" +
          "ControllerPoint - This gives an Arrival notification in the controller.\n")]
        public PalletCommPointATCInfo.PalletCommPointATCTypes CommPointType
        {
            get { return commPointATCInfo.palletCommPointType; }
            set
            {
                commPointATCInfo.palletCommPointType = value;
                Core.Environment.Properties.Refresh();
            }
        }

        #endregion

        #endregion


        public override void Dispose()
        {
            //throw new NotImplementedException();
        }
    }

    [Serializable]
    [XmlInclude(typeof(PalletCommPointATCInfo))]
    public class PalletCommPointATCInfo : ProtocolInfo
    {
        public enum PalletCommPointATCTypes
        {
            None,
            LocationArrived,
            LocationLeft,
            TransportRequest,
            TransportFinished,
            TransportRequestOrFinished,
            ControllerPoint
        }
        
        public PalletCommPointATCTypes palletCommPointType = PalletCommPointATCTypes.None;
        public bool alwaysArrival = true;
        public bool loadWait = true;

    }
}