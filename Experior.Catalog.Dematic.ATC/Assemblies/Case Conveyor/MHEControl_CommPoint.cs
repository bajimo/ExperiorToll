using Experior.Catalog.Dematic.Case.Devices;
using Experior.Core;
using Experior.Core.Loads;
using Experior.Core.Properties;
using Experior.Dematic.Base;
using Experior.Dematic.Base.Devices;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Xml.Serialization;

namespace Experior.Catalog.Dematic.ATC.Assemblies.CaseConveyor
{
    /// <summary>
    /// This is your MHE control class it is instansuated by the controller (PLC etc) and passed back to the communicationPoint
    /// it controlls the MHE and it the routing and decession making processes of the MHE.
    /// </summary>
    public class MHEControl_CommPoint : MHEControl
    {
        private CommPointATCInfo commPointATCInfo;
        private CommunicationPoint commPoint;
        private MHEController_Case casePLC;

        #region Constructors

        public MHEControl_CommPoint(CommPointATCInfo info, CommunicationPoint cPoint)
        {
            commPoint = cPoint;
            commPointATCInfo = info;
            Info = info;  // set this to save properties 
            commPoint.commPointArrival = ap_Enter; //CommunicationPoint will use this delegate for ap_enter.
            casePLC = CommPoint.Controller as MHEController_Case;
        }

        #endregion

        public void ap_Enter(DematicCommunicationPoint sender, Load load)
        {
            IATCCaseLoadType caseLoad = load as IATCCaseLoadType;
            if (caseLoad == null)
            {
                return;
            }

            caseLoad.Location = commPoint.Name; //Update the caseload location

            switch (CommPointType)
            {
                case CommPointATCInfo.CommPointATCTypes.None: break;
                case CommPointATCInfo.CommPointATCTypes.LocationArrived: LocationArrived(CommPoint, caseLoad); break;
                case CommPointATCInfo.CommPointATCTypes.LocationLeft: LocationLeft(commPoint, caseLoad); break;
                case CommPointATCInfo.CommPointATCTypes.TransportRequest: TransportRequest(commPoint, caseLoad); break;
                case CommPointATCInfo.CommPointATCTypes.TransportFinished: TransportFinished(commPoint, caseLoad); break;
                case CommPointATCInfo.CommPointATCTypes.TransportRequestOrFinished: TransportRequestOrFinished(commPoint, caseLoad); break;
                default: break;
            }
        }

        private void LocationArrived(CommunicationPoint commPoint, IATCCaseLoadType caseLoad)
        {
            if (AlwaysArrival || caseLoad.Location == caseLoad.Destination)
            {
                caseLoad.Location = commPoint.Name;
                //caseLoad.MTS = commPoint.ControllerName;
                casePLC.SendLocationArrivedTelegram(caseLoad as IATCCaseLoadType);
            }
        }

        private void LocationLeft(CommunicationPoint commPoint, IATCCaseLoadType caseLoad)
        {
            if (AlwaysArrival || caseLoad.Location == caseLoad.Destination)
            {
                caseLoad.Location = commPoint.Name;
                //caseLoad.MTS = commPoint.ControllerName;
                casePLC.SendLocationLeftTelegram(caseLoad as IATCCaseLoadType);
            }
        }

        //TODO: For the loads that are stopping on the case conveyor at these communication points it
        //has to be placed in the correct position otherwise its can be an issue (I have found that placing 
        //them in exactly the correct place compaired to a accumulation sensor is a good position as all atction point are triggered
        private void TransportRequest(CommunicationPoint commPoint, IATCCaseLoadType caseLoad)
        {
            if (AlwaysArrival || caseLoad.Location == caseLoad.Destination || string.IsNullOrEmpty(caseLoad.Destination))
            {
                caseLoad.Location = commPoint.Name;
                //caseLoad.MTS = commPoint.ControllerName;
                casePLC.SendTransportRequestTelegram(caseLoad);

                if (LoadWait)
                {
                    //[BG] Not happy with this, if this is used then it needs to be understood what the issues are with it 
                    //(Like stopping on a belt is bad and you need to put the Comm Point in the right place on accumulation conveyor)
                    caseLoad.LoadWaitingForWCS = true;
                    caseLoad.Stop();

                }
            }
        }

        private void TransportFinished(CommunicationPoint commPoint, IATCCaseLoadType caseLoad)
        {
            if (AlwaysArrival || caseLoad.Location == caseLoad.Destination)
            {
                caseLoad.Location = commPoint.Name;
                casePLC.SendTransportFinishedTelegram(caseLoad);

                if (LoadWait)
                {
                    caseLoad.LoadWaitingForWCS = true;
                    caseLoad.Stop();
                }
            }
        }

        private void TransportRequestOrFinished(CommunicationPoint commPoint, IATCCaseLoadType caseLoad)
        {
            if (caseLoad.Location == caseLoad.Destination)
            {
                //Send TransportFinishedTelegram
                caseLoad.Location = commPoint.Name;
                casePLC.SendTransportFinishedTelegram(caseLoad);
            }
            else
            {
                //Send TransportRequestTelegram
                caseLoad.Location = commPoint.Name;
                casePLC.SendTransportRequestTelegram(caseLoad);
            }

            if (LoadWait)
            {
                caseLoad.LoadWaitingForWCS = true;
                caseLoad.Stop();
            }
        }

        #region Properties

        [Browsable(false)]
        public CommunicationPoint CommPoint
        {
            get { return commPoint; }
            set { commPoint = value; }
        }

        #region user interface

        public void DynamicPropertyAlwaysTelegram(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = (CommPointType == CommPointATCInfo.CommPointATCTypes.LocationArrived ||
                                      CommPointType == CommPointATCInfo.CommPointATCTypes.LocationLeft ||
                                      CommPointType == CommPointATCInfo.CommPointATCTypes.TransportRequest ||
                                      CommPointType == CommPointATCInfo.CommPointATCTypes.TransportFinished);
        }

        public void DynamicPropertyLoadWait(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = (CommPointType == CommPointATCInfo.CommPointATCTypes.TransportRequest ||
                                      CommPointType == CommPointATCInfo.CommPointATCTypes.TransportFinished || 
                                      CommPointType == CommPointATCInfo.CommPointATCTypes.TransportRequestOrFinished);
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
        public CommPointATCInfo.CommPointATCTypes CommPointType
        {
            get { return commPointATCInfo.commPointType; }
            set
            {
                commPointATCInfo.commPointType = value;
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
    [XmlInclude(typeof(CommPointATCInfo))]
    public class CommPointATCInfo : ProtocolInfo
    {
        public enum CommPointATCTypes
        {
            None,
            LocationArrived,
            LocationLeft,
            TransportRequest,
            TransportFinished,
            TransportRequestOrFinished,
            ControllerPoint
        }
        
        public CommPointATCTypes commPointType = CommPointATCTypes.None;
        public bool alwaysArrival = true;
        public bool loadWait = true;

    }
}