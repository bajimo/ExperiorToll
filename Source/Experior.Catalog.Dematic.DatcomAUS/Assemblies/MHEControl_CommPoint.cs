using Experior.Catalog.Dematic.Case.Devices;
using Experior.Core.Loads;
using Experior.Core.Properties;
using Experior.Dematic.Base;
using Experior.Dematic.Base.Devices;
using System;
using System.ComponentModel;
using System.Xml.Serialization;

namespace Experior.Catalog.Dematic.DatcomAUS.Assemblies
{
    /// <summary>
    /// This is your MHE control class it is instansuated by the controller (PLC etc) and passed back to the communicationPoint
    /// it controlls the MHE and it the routing and decession making processes of the MHE.
    /// </summary>
    public class MHEControl_CommPoint : MHEControl
    {
        private CommPointDatcomAusInfo commPointDatcomInfo;
        private CommunicationPoint commPoint;
        private MHEControllerAUS_Case casePLC;

        public MHEControl_CommPoint(CommPointDatcomAusInfo info, CommunicationPoint cPoint)
        {
            commPoint = cPoint;
            commPointDatcomInfo = info;
            Info = info;  // set this to save properties 
            commPoint.commPointArrival = ap_Enter; //CommunicationPoint will use this delegate for ap_enter.
            casePLC = CommPoint.Controller as MHEControllerAUS_Case;
        }

        public void ap_Enter(DematicCommunicationPoint sender, Load load)
        {      
            var caseload = load as Case_Load;
            if (caseload == null)
            {
                return;
            }

            var caseData = caseload.Case_Data as CaseData;
            if (caseData == null)
            {
                //Case data is null or another type.
                caseData = new CaseData();
                caseload.Case_Data = caseData;
            }

            if (CommPoint.ControllerName != string.Empty)
            {
                if (((CaseData)caseload.Case_Data).PLCName != CommPoint.ControllerName && ((CaseData)caseload.Case_Data).PLCName != string.Empty)
                {
                    //Case enters new plc area. Remove from routing table                    
                    IController oldplc = Experior.Core.Assemblies.Assembly.Items[((CaseData)caseload.Case_Data).PLCName] as IController;

                    if (oldplc != null)
                        oldplc.RemoveFromRoutingTable(caseload.Identification);
                }

                ((CaseData)caseload.Case_Data).PLCName = CommPoint.ControllerName;
            }

            switch (CommPointType)
            {
                case CommPointDatcomAusInfo.DatcomAusCommPointTypes.None:
                    break;
                case CommPointDatcomAusInfo.DatcomAusCommPointTypes.Arrival_Point:
                    ToteArrived(CommPoint, caseload);

                    if (RemoveFromRoutingTableArrival)
                    {
                        casePLC.RemoveFromRoutingTable(caseload.Identification);
                    }
                    break;
            }
        }

        private void ToteArrived(IControllable commPoint, Case_Load caseload)
        {
            if (OnlyActiveIfDestination)
            {
                if (casePLC.RoutingTable.ContainsKey(caseload.Identification))
                {
                    var destination = casePLC.RoutingTable[caseload.Identification];
                    if (destination != commPoint.Name)
                    {
                        //This is not the destination. Dont send arrival.
                        return;
                    }
                }
            }

            if (WaitForTransportOrder)
            {
                caseload.LoadWaitingForWCS = true;
                caseload.StopLoad();
                caseload.OnDisposing += Load_OnDisposed;
                caseload.OnReleased += Load_OnReleased;
            }

            if (AlwaysArrival) //Always send arrival message
                casePLC.SendArrivalMessage(commPoint.Name, caseload);
            else if (!casePLC.RoutingTable.ContainsKey(caseload.Identification))
            {
                //Only send arrival message if ULID not found in routing table
                casePLC.SendArrivalMessage(commPoint.Name, caseload);
                return;
            }
        }

        private void Load_OnDisposed(Load load)
        {
            //Load deleted while waiting for 01 message.
            //Send manually deleted exception
            load.OnDisposing -= Load_OnDisposed;
            load.OnReleased -= Load_OnReleased;
            casePLC.SendExceptionMessage(commPoint.Name, ((Case_Load)load), "MD");  
        }

        private void Load_OnReleased(Core.Loads.Load load)
        {
            load.OnDisposing -= Load_OnDisposed;
            load.OnReleased -= Load_OnReleased;
        }

        [Browsable(false)]
        public CommunicationPoint CommPoint
        {
            get { return commPoint; }
            set { commPoint = value; }
        }

        public void DynamicPropertyArrival_Point(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = (commPointDatcomInfo.commPointType == CommPointDatcomAusInfo.DatcomAusCommPointTypes.Arrival_Point);                                 
        }

        public void DynamicPropertyArrivalPoint(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = CommPointType == CommPointDatcomAusInfo.DatcomAusCommPointTypes.Arrival_Point;
        }

        [Category("Configuration")]
        [DisplayName("Always send arrival")]
        [Description("If false the arrival will only be sent if case ULID is not found in the routing table. If true then arrival message will always be sent (default).")]
        [PropertyAttributesProvider("DynamicPropertyArrivalPoint")]
        [PropertyOrder(7)]
        public bool AlwaysArrival
        {
            get
            {
                return commPointDatcomInfo.alwaysArrival;
            }
            set
            {
                commPointDatcomInfo.alwaysArrival = value;
            }
        }

        [Category("Configuration")]
        [DisplayName("Wait for transport order")]
        [Description("Stop and release when transport order (01) received.")]
        [PropertyAttributesProvider("DynamicPropertyArrivalPoint")]
        [PropertyOrder(8)]
        public bool WaitForTransportOrder
        {
            get
            {
                return commPointDatcomInfo.WaitForTransportOrder;
            }
            set
            {
                commPointDatcomInfo.WaitForTransportOrder = value;
            }
        }

        [Category("Configuration")]
        [DisplayName("Only active if destination")]
        [Description("The arrival is only sent if this is the destination")]
        [PropertyAttributesProvider("DynamicPropertyArrivalPoint")]
        [PropertyOrder(9)]
        public bool OnlyActiveIfDestination
        {
            get
            {
                return commPointDatcomInfo.OnlyActiveIfDestination;
            }
            set
            {
                commPointDatcomInfo.OnlyActiveIfDestination = value;
            }
        }

        [DisplayName("Type")]
        [DescriptionAttribute("None - No messaging.\n" +
          "Arrival_Point - 02 message.\n" +
          //"Arrival_Delay - 02 message. Release tote after x seconds.\n" +
          //"CallForwardPoint - Tote stops and can be released with 26. After the release type 02 is sent.\n" +
          //"MultishuttleDropStation - Handshake with multishuttle. MS will send arrival message.\n" +
          //"DelayPoint - Tote will be released after x seconds.\n" +
          "ControllerPoint - This gives an Arrival notification in the controller.\n")]
        public CommPointDatcomAusInfo.DatcomAusCommPointTypes CommPointType
        {
            get { return commPointDatcomInfo.commPointType; }
            set
            {
                commPointDatcomInfo.commPointType = value;
                Core.Environment.Properties.Refresh();
            }
        }

        [Category("Configuration")]
        [DisplayName("Remove from routing table")]
        [Description("If true the the barcode will be removed from the plc on arrival. Note: The plc must be set.")]
        [PropertyOrder(17)]
        [PropertyAttributesProvider("DynamicPropertyArrival_Point")]
        public virtual bool RemoveFromRoutingTableArrival
        {
            get { return commPointDatcomInfo.removeFromRoutingTableArrival; }
            set { commPointDatcomInfo.removeFromRoutingTableArrival = value; }
        }

        public override void Dispose()
        {
            if (commPoint != null && commPoint.commPointArrival == ap_Enter)
            {
                commPoint.commPointArrival = null;
            }
        }
    }

    [Serializable]
    [XmlInclude(typeof(CommPointDatcomAusInfo))]
    public class CommPointDatcomAusInfo : ProtocolInfo
    {
        public enum DatcomAusCommPointTypes 
        { 
            None, 
            Arrival_Point, 
            ControllerPoint 
        }
        
        public DatcomAusCommPointTypes commPointType = DatcomAusCommPointTypes.None;
        public int x = 0, y = 0;
        public bool removeFromRoutingTableArrival;
        public bool alwaysArrival = true;
        public bool WaitForTransportOrder { get; set; }
        public bool OnlyActiveIfDestination { get; set; }
    }
}