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

        #region Constructors

        public MHEControl_CommPoint(CommPointDatcomAusInfo info, CommunicationPoint cPoint)
        {
            commPoint = cPoint;
            commPointDatcomInfo = info;
            Info = info;  // set this to save properties 
            commPoint.commPointArrival = ap_Enter; //CommunicationPoint will use this delegate for ap_enter.
            casePLC = CommPoint.Controller as MHEControllerAUS_Case;
        }

        #endregion

        public void ap_Enter(DematicCommunicationPoint sender, Load load)
        {      

            //if (CommPointType == CommPointDatcomAusInfo.DatcomAusCommPointTypes.DelayPoint)
            //{
            //    load.WaitingTime = DelayTime;
            //    return;
            //}
            //else if (CommPointType == CommPointDatcomAusInfo.DatcomAusCommPointTypes.Arrival_Delay)
            //{
            //    load.WaitingTime = DelayTime;
            //}

            Case_Load caseload = load as Case_Load;
            if (caseload == null)
            {
                return;
            }

            if (CommPoint.ControllerName != string.Empty)
            {
                if (((CaseData)caseload.Case_Data).PLCName != CommPoint.ControllerName && ((CaseData)caseload.Case_Data).PLCName != string.Empty)
                {
                    //Case enters new plc area. Remove from routing table                    
                    IController oldplc = Experior.Core.Assemblies.Assembly.Items[((CaseData)caseload.Case_Data).PLCName] as IController;

                    if (oldplc != null)
                        oldplc.RemoveSSCCBarcode(caseload.SSCCBarcode);
                }

                ((CaseData)caseload.Case_Data).PLCName = CommPoint.ControllerName;
            }

            switch (CommPointType)
            {
                case CommPointDatcomAusInfo.DatcomAusCommPointTypes.None:
                    break;
                case CommPointDatcomAusInfo.DatcomAusCommPointTypes.Arrival_Point:
                //case CommPointDatcomAusInfo.DatcomAusCommPointTypes.Arrival_Delay:

                    ToteArrived(CommPoint, caseload);

                    if (RemoveFromRoutingTableArrival)
                    {
                        casePLC.RemoveSSCCBarcode(caseload.SSCCBarcode);
                    }
                    break;

                //case CommPointDatcomAusInfo.DatcomAusCommPointTypes.CallForwardPoint:

                //    CallForwardArrived((CommunicationPoint)CommPoint.Parent, caseload);

                //    if (RemoveFromRoutingTableArrival)
                //    {
                //        casePLC.RemoveSSCCBarcode(caseload.SSCCBarcode);
                //    }
                //    break;

                //case CommPointDatcomAusInfo.DatcomAusCommPointTypes.MultishuttleDropStation:
                //    if (caseload.UserData != null)
                //    {
                //        //caseload.UserData should be a reference to the Multishuttle.
                //        //Send handshake message so multishuttle can continue.
                //        HandshakeMessage msg = new HandshakeMessage();
                //        msg.MessageType = HandshakeMessage.MessageTypes.Tote_Arrived_On_Dropstation;
                //        msg.Load = load;
                //        Experior.Core.Communication.Internal.SendMessage(this, caseload.UserData, msg);
                //        caseload.UserData = null;
                //        caseload.UserDeletable = true;
                //    }
                //    break;

                default:
                    break;
            }

        }

        private void ToteArrived(CommunicationPoint commPoint, Case_Load caseload)
        {
            if (AlwaysArrival) //Always send arrival message
                casePLC.SendDivertConfirmation(location: commPoint.Name, SSCCBarcode: caseload.SSCCBarcode);
            else if (!casePLC.RoutingTable.ContainsKey(caseload.SSCCBarcode))
            {
                //Only send arrival message if ULID not found in routing table
                casePLC.SendDivertConfirmation(location: commPoint.Name, SSCCBarcode: caseload.SSCCBarcode);
                return;
            }
        }

        //private void CallForwardArrived(CommunicationPoint commPoint, Case_Load caseload)
        //{
        //    caseload.Stop();
        //    caseload.Case_Data.RoutingTableUpdateWait = false;
        //    ((CaseData)caseload.Case_Data).CallforwardWait = true;
        //    //CaseDatcomPLC PLCdatcom = CommPoint.Controller as CaseDatcomPLC;

        //    if (CallForwardArrivalMessage && CallForwardType == CommPointDatcomAusInfo.CallForwardTypes.OnArrival)
        //    {
        //        if (caseload.CurrentPosition != commPoint.Name) //If a tote is feed to this location then an 02 should not be sent. The feeding method will set caseload.CurrentPosition = location...
        //        {
        //            //Send arrival message (02)
        //            casePLC.SendDivertConfirmation(location: commPoint.Name, SSCCBarcode: caseload.SSCCBarcode);
        //        }
        //    }

        //    caseload.CurrentPosition = string.Empty;

        //    if (casePLC.callForwardTable.ContainsKey(commPoint.Name))
        //    {
        //        ushort count = casePLC.callForwardTable[commPoint.Name].Quantity;
        //        count--;
        //        casePLC.callForwardTable[commPoint.Name].Quantity = count;
        //        casePLC.callForwardTable[commPoint.Name].Timer.Reset();
        //        casePLC.callForwardTable[commPoint.Name].Timer.Start();
        //        //For each tote that is launched, the CCC sends a Divert Confirmation Message (02) with the barcode
        //        //of the tote from the order start location.
        //        if (CallForwardArrivalMessage && CallForwardType == CommPointDatcomAusInfo.CallForwardTypes.OnLeaving)
        //            casePLC.SendDivertConfirmation(commPoint.Name, caseload.SSCCBarcode);

        //        caseload.Release();
        //        caseload.Case_Data.CallforwardWait = false;

        //        if (casePLC.callForwardTable[commPoint.Name].Quantity == 0)
        //            casePLC.RemoveCallForwardLocation(commPoint.Name);
        //    }
        //}

        //public bool CallForwardRecieved(string[] telegramFields, ushort number_of_blocks, string location, ushort quantity, ushort block)
        //{
        //    CommunicationPoint loc = Experior.Core.Assemblies.Assembly.Items[location] as CommunicationPoint;
        //    //CaseDatcomPLC PLCdatcom = CommPoint.Controller as CaseDatcomPLC;

        //    //StraightConveyor conv = Core.Assemblies.Assembly.Items[location] as StraightConveyor;
        //    if (loc != null && casePLC != null)
        //    {
        //        //TODO uncomment this
        //        //if (conv.CommunicationPoint != StraightConveyor.CaseCommunicationPoints.CallForwardPoint)
        //        //{
        //        //    Core.Environment.Log.Write(this.Name + " recieved Call forward with location " + location + " but this conv has not been set as a Call forward point!.", Color.Red);
        //        //    return;
        //        //}

        //        if (telegramFields[2] == "86")
        //        {
        //            Case_Load caseload = loc.apCommPoint.ActiveLoad as Case_Load;

        //            if (caseload != null)
        //            {
        //                //A case load is waiting to be relased.
        //                string barcode = telegramFields[8 + block * 2];

        //                if (barcode != caseload.SSCCBarcode)
        //                    Core.Environment.Log.Write(string.Format("Type 86: Barcode of load in location {0} does not match the barcode in the call forward point - UL released anyway", location), Color.Orange);

        //                loc.apCommPoint.Release();

        //                if (CallForwardArrivalMessage && CallForwardType == CommPointDatcomAusInfo.CallForwardTypes.OnLeaving)
        //                    casePLC.SendDivertConfirmation(location, caseload.SSCCBarcode);

        //                return true;
        //            }
        //        }

        //        //This message contains a quantity of empty totes required.
        //        //The CCC will then release the required quantity of totes. MFH can update the number of totes
        //        //required at any time by sending a new type 26 message with an updated quantity. If the CCC is still
        //        //releasing totes from the last message, the new quantity will replace the number left to release. In this
        //        //way, MFH can also cancel a call forward instruction by sending a message with zero in the quantity
        //        //field.

        //        if (casePLC.callForwardTable.ContainsKey(location))
        //        {
        //            //Location already exists. Give new time and update count
        //            casePLC.callForwardTable[location].Quantity = quantity;
        //            casePLC.callForwardTable[location].Timer.Stop();
        //            casePLC.callForwardTable[location].Timer.Timeout = CallForwardTimeout;
        //            casePLC.callForwardTable[location].Timer.Start();

        //            if (quantity == 0)
        //            {
        //                casePLC.RemoveCallForwardLocation(location);
        //                return true;
        //            }
        //        }
        //        else if (quantity > 0)
        //        {
        //            //Create location count and timer
        //            Timer timer = new Timer(CallForwardTimeout);
        //            timer.OnElapsed += new Timer.ElapsedEvent(casePLC.CallForwardElapsed);
        //            timer.UserData = location;
        //            timer.Start();

        //            CallForwardLocation callforwardlocation = new CallForwardLocation();
        //            callforwardlocation.Location = location;
        //            callforwardlocation.Quantity = quantity;
        //            callforwardlocation.Timer = timer;
        //            casePLC.callForwardTable.Add(location, callforwardlocation);
        //        }

        //        if (quantity > 0)
        //        {
        //            Case_Load caseload = CommPoint.apCommPoint.ActiveLoad as Case_Load;

        //            if (caseload != null)
        //            {
        //                //A case load is waiting to be relased.
        //                CommPoint.apCommPoint.Release();
        //                caseload.Case_Data.CallforwardWait = false;
        //                ushort count = quantity;
        //                count--;
        //                casePLC.callForwardTable[location].Quantity = count;
        //                //For each tote that is launched, the CCC sends a Divert Confirmation Message (02) with the barcode
        //                //of the tote from the order start location.

        //                if (CallForwardArrivalMessage && CallForwardType == CommPointDatcomAusInfo.CallForwardTypes.OnLeaving)
        //                    casePLC.SendDivertConfirmation(location, caseload.SSCCBarcode);

        //                if (casePLC.callForwardTable[location].Quantity == 0)
        //                    casePLC.RemoveCallForwardLocation(location);
        //            }
        //        }
        //    }
        //    return false;
        //}

        #region Properties

        [Browsable(false)]
        public CommunicationPoint CommPoint
        {
            get { return commPoint; }
            set { commPoint = value; }
        }

        #region user interface

        //public void DynamicPropertyArrival_Point_Arrival_Delay_CallForwardPoint(PropertyAttributes attributes)
        public void DynamicPropertyArrival_Point(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = (commPointDatcomInfo.commPointType == CommPointDatcomAusInfo.DatcomAusCommPointTypes.Arrival_Point);// ||
                                      //commPointDatcomInfo.commPointType == CommPointDatcomAusInfo.DatcomAusCommPointTypes.Arrival_Delay ||
                                      //commPointDatcomInfo.commPointType == CommPointDatcomAusInfo.DatcomAusCommPointTypes.CallForwardPoint);
        }

        //public void DynamicPropertyDelayPoint(PropertyAttributes attributes)
        //{
        //    attributes.IsBrowsable = (CommPointType == CommPointDatcomAusInfo.DatcomAusCommPointTypes.DelayPoint || CommPointType == CommPointDatcomAusInfo.DatcomAusCommPointTypes.Arrival_Delay);
        //}

        //public void DynamicPropertyCallForwardPoint(PropertyAttributes attributes)
        //{
        //    attributes.IsBrowsable = CommPointType == CommPointDatcomAusInfo.DatcomAusCommPointTypes.CallForwardPoint;   //.CallForwardPoint;
        //}

        public void DynamicPropertyArrivalPoint(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = CommPointType == CommPointDatcomAusInfo.DatcomAusCommPointTypes.Arrival_Point;
        }

        //[Category("Configuration")]
        //[DisplayName("Call Forward Timeout (sec.)")]
        //[Description("Call Forward Time Out in seconds. If the right quantity of totes have not been released within this time then a Call forward Exception (type 27) is sent.")]
        //[PropertyOrder(4)]
        //[PropertyAttributesProvider("DynamicPropertyCallForwardPoint")]
        //public float CallForwardTimeout
        //{
        //    get
        //    {
        //        return commPointDatcomInfo.callForwardTimeout;
        //    }
        //    set
        //    {
        //        if (value > 1)
        //            commPointDatcomInfo.callForwardTimeout = value;
        //    }
        //}

        //[Category("Configuration")]
        //[DisplayName("Call Forward Arrival message")]
        //[Description("If true an arrival message (02) will be sent (Before or after call forward depending on CallForwardType).")]
        //[PropertyOrder(5)]
        //[PropertyAttributesProvider("DynamicPropertyCallForwardPoint")]
        //public bool CallForwardArrivalMessage
        //{
        //    get
        //    {
        //        return commPointDatcomInfo.callForwardArrivalMessage;
        //    }
        //    set
        //    {
        //        commPointDatcomInfo.callForwardArrivalMessage = value;
        //    }
        //}

        //[Category("Configuration")]
        //[DisplayName("Call Forward mode")]
        //[Description("Send 02 when case arrives or leaves the call forward position.")]
        //[PropertyOrder(6)]
        //[PropertyAttributesProvider("DynamicPropertyCallForwardPoint")]
        //public CommPointDatcomAusInfo.CallForwardTypes CallForwardType
        //{
        //    get
        //    {
        //        return commPointDatcomInfo.callForwardType;
        //    }
        //    set
        //    {
        //        commPointDatcomInfo.callForwardType = value;
        //    }
        //}

        //[DisplayName("Delay Time (sec.)")]
        //[Description("Delay Time in seconds. Tote is stopped and released when timer elapses. Ex. label machine.")]
        //// [PropertyOrder(3)]
        //[PropertyAttributesProvider("DynamicPropertyDelayPoint")]
        //public float DelayTime
        //{
        //    get
        //    {
        //        return commPointDatcomInfo.delayTime;
        //    }
        //    set
        //    {
        //        if (value > 0)
        //        {
        //            commPointDatcomInfo.delayTime = value;
        //        }
        //    }
        //}

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

        #endregion

        #endregion


        public override void Dispose()
        {
            throw new NotImplementedException();
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
            //Arrival_Delay, 
            //CallForwardPoint, 
            //MultishuttleDropStation, 
            //DelayPoint, 
            ControllerPoint 
        }
        
        public DatcomAusCommPointTypes commPointType = DatcomAusCommPointTypes.None;

        //public enum CallForwardTypes { OnArrival, OnLeaving };
        //public CallForwardTypes callForwardType = CallForwardTypes.OnArrival;

        public int x = 0, y = 0;
        //public float delayTime;
        public bool removeFromRoutingTableArrival;
        public bool alwaysArrival = true;
        //public float callForwardTimeout = 30;
        //public bool callForwardArrivalMessage;

        //  public StraightConveyor straightConveyor;

    }

    //public class HandshakeMessage
    //{
    //    public enum MessageTypes
    //    {
    //        Pallet_Received_From_Crane,
    //        Pallet_Removed_From_Conv,
    //        Tote_Arrived,
    //        Tote_Ready_To_Miniload,
    //        Tote_Removed_From_Conv,
    //        Tote_Arrived_On_Dropstation
    //    }

    //    public MessageTypes MessageType
    //    {
    //        get;
    //        set;
    //    }
    //    public Load Load
    //    {
    //        get;
    //        set;
    //    }
    //}
}