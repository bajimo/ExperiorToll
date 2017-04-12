using Experior.Catalog.Dematic.Case.Components;
using Experior.Core.Loads;
using Experior.Core.Properties;
using Experior.Dematic.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Serialization;

namespace Experior.Catalog.Dematic.DatcomAUS.Assemblies
{
    public class MHEControl_AngledDivert : MHEControl
    {
        private AngledDivertDatcomAusInfo divertDatcomInfo;
        private AngledDivert divertConveyor;
        private MHEControllerAUS_Case casePLC;
        private List<string> DivertRoutes = null;
        //private List<int[]> RightRoutes = null;
        //private List<int[]> PriorityRoutes = null;
        DivertRoute selectedRoute = DivertRoute.None;

        #region Constructors
        public MHEControl_AngledDivert(AngledDivertDatcomAusInfo info, AngledDivert angledDivert)
        {
            divertConveyor = angledDivert;
            divertDatcomInfo = info;
            Info = info;  // set this to save properties 

            //Subscribe to the diverter events
            divertConveyor.OnDivertPointArrivedControl += divertConveyor_OnDivertPointArrivedControl;
            divertConveyor.OnDivertPointDivertedControl += divertConveyor_OnDivertPointDivertedControl;


            casePLC = divertConveyor.Controller as MHEControllerAUS_Case;

            //Anything with setup code in the "set" of a property except setting the value will need to be called 
            //explicitly so that the "set" code will execute when loading from a saved configuration
            DivertRoutingCode = info.divertRoutingCode;
            //StraightRoutingCode = info.straightRoutingCode;
        }

        //Load has arrived at divert point
        void divertConveyor_OnDivertPointArrivedControl(object sender, AngleDivertArgs e)
        {
            Case_Load caseload = e._load as Case_Load;

            if (casePLC.DivertSet(caseload.Identification, DivertRoutes))
            {
                selectedRoute = DivertRoute.Divert;
                divertConveyor.RouteLoad(DivertRoute.Divert);
            }
            else if (casePLC.DivertSet(caseload.Identification, StraightRoutes))
            {
                selectedRoute = DivertRoute.Straight;
                divertConveyor.RouteLoad(DivertRoute.Straight);
            }
            else
            {
                selectedRoute = divertConveyor.DefaultRoute;
                divertConveyor.RouteLoad(divertConveyor.DefaultRoute);
            }
        }

        void divertConveyor_OnDivertPointDivertedControl(object sender, AngleDivertArgs e)
        {
            Case_Load caseLoad = e._load as Case_Load;

            if (e._direction != selectedRoute && !string.IsNullOrEmpty(FailedToDivertLocation))
            {
                //Send failed to divert message

                if (FailedToDivertMessageType == MHEControllerAUS_Case.FailedDatcomAusMessageType._02)
                {
                    casePLC.SendArrivalMessage(FailedToDivertLocation, caseLoad, "08");
                }
                else if (FailedToDivertMessageType == MHEControllerAUS_Case.FailedDatcomAusMessageType._06)
                {
                    casePLC.SendExceptionMessage(FailedToDivertLocation, caseLoad, "08");
                }
            }
            else if (e._direction == DivertRoute.Divert && !string.IsNullOrEmpty(DivertRoutingLocation))
            {
                //Send Diverted message
                casePLC.SendArrivalMessage(DivertRoutingLocation, caseLoad);
            }
            else if (e._direction == DivertRoute.Straight && !string.IsNullOrEmpty(StraightRoutingLocation))
            {
                //Send Diverted straight message
                casePLC.SendArrivalMessage(StraightRoutingLocation, caseLoad);
            }
        }
        #endregion

        bool loadDeleted(Load load) //Not used no event at the moment (Don't think we care!!!)
        {
            Case_Load caseLoad = load as Case_Load;
            
            if (casePLC.RoutingTable.ContainsKey(caseLoad.Identification))
                casePLC.RoutingTable.Remove(caseLoad.Identification);

            return true;
        }

        #region User Interface

        [Category("Configuration")]
        [DisplayName("Controller Point")]
        [Description("Set true if the controller script should handle the routing. The routing will not be handleb by the selected PLC, however the configuration can still be used for routing within the controller script")]
        [PropertyOrder(1)]
        public bool ControllerPoint
        {
            get
            {
                return divertDatcomInfo.controllerPoint;
            }
            set
            {
                divertDatcomInfo.controllerPoint = value;
            }
        }

        [Category("Configuration")]
        [DisplayName("Remove From Routing Table")]
        [Description("When load has been routed should the entry in the PLC routing table be removed")]
        [PropertyOrder(3)]
        public bool RemoveFromRoutingTable
        {
            get { return divertDatcomInfo.removeFromRoutingTable; }
            set { divertDatcomInfo.removeFromRoutingTable = value; }
        }

        [Category("Configuration")]
        [DisplayName("Failed To Divert Message Type")]
        [Description("The message type to be used on failed to divert")]
        [PropertyOrder(4)]
        public MHEControllerAUS_Case.FailedDatcomAusMessageType FailedToDivertMessageType 
        {
            get { return divertDatcomInfo.failedToDivertMessageType; }
            set 
            {
                divertDatcomInfo.failedToDivertMessageType = value;
                Experior.Core.Environment.Properties.Refresh();
            } 
        }

        [Category("Configuration")]
        [DisplayName("Failed To Divert Reason Code")]
        [Description("The Reason Code used in the 06 message, 2 characters. See 'Communication Standard MFH - Case Conveyor' Doc number 41796253")]
        [PropertyAttributesProvider("DynamicPropertyReasonCode")]
        [PropertyOrder(5)]
        public string FailedToDivertMessageReasonCode
        {
            get { return divertDatcomInfo.failedToDivertMessageReasonCode; }
            set
            {
                divertDatcomInfo.failedToDivertMessageReasonCode = value;
            }
        }

        public void DynamicPropertyReasonCode(Core.Properties.PropertyAttributes attributes)
        {
            attributes.IsBrowsable = divertDatcomInfo.failedToDivertMessageType == MHEControllerAUS_Case.FailedDatcomAusMessageType._06;
        }

        [Category("Configuration")]
        [DisplayName("Failed To Divert Location")]
        [Description("If the load is routed to the default route and the failed to divert location is entered, then a divert confirmation message will be sent with this location, the failed to divert location and the deafult route location should not both be entered")]
        [PropertyOrder(6)]
        public string FailedToDivertLocation
        {
            get { return divertDatcomInfo.failedToDivertLocation; }
            set
            {
                if (value == "")
                    divertDatcomInfo.failedToDivertLocation = null;
                else
                {
                    divertDatcomInfo.failedToDivertLocation = value;
                }
            }
        }

        [DisplayName("Straight Routing Code")]
        [Description("Routing code for straight routing: format destination1,destination2,...,destionation n")]
        [PropertyAttributesProvider("DynamicPropertyStraightModeDivert")]
        [PropertyOrder(7)]
        public string StraightRoutingCode
        {
            get { return divertDatcomInfo.straightRoutingCode; }
            set
            {
                if (value == null || value == "")
                {
                    divertDatcomInfo.straightRoutingCode = null;
                    StraightRoutes = null;
                    return;
                }

                List<string> routes = casePLC.ValidateRoutingCode(value);
                if (routes != null)
                {
                    StraightRoutes = routes;
                    divertDatcomInfo.straightRoutingCode = value;
                }
            }
        }

        private List<string> StraightRoutes = null;

        [DisplayName("Straight Route Location")]
        [Description("Location name in divert confirmation message when successfully diverted, if blank then no divert confirmation message will be sent")]
        [PropertyOrder(8)]
        public string StraightRoutingLocation
        {
            get { return divertDatcomInfo.straightRoutingLocation; }
            set
            {
                if (value == "")
                    divertDatcomInfo.straightRoutingLocation = null;
                else
                {
                    divertDatcomInfo.straightRoutingLocation = value;
                }
            }
        }

        [DisplayName("Divert Routing Code")]
        [Description("Routing code for divert routing: format destination1,destination2,...,destionation n")]
        [PropertyOrder(9)]
        public string DivertRoutingCode
        {
            get { return divertDatcomInfo.divertRoutingCode; }
            set
            {
                if (value == null || value == "")
                {
                    divertDatcomInfo.divertRoutingCode = null;
                    DivertRoutes = null;
                    return;
                }

                List<string> routes = casePLC.ValidateRoutingCode(value);
                if (routes != null)
                {
                    DivertRoutes = routes;
                    divertDatcomInfo.divertRoutingCode = value;
                }
            }
        }

        [DisplayName("Divert Route Location")]
        [Description("Location name in divert confirmation message when successfully diverted, if blank then no divert confirmation message will be sent")]
        [PropertyOrder(10)]
        public string DivertRoutingLocation
        {
            get { return divertDatcomInfo.divertRoutingLocation; }
            set
            {
                if (value == "")
                    divertDatcomInfo.divertRoutingLocation = null;
                else
                {
                    divertDatcomInfo.divertRoutingLocation = value;
                }
            }
        }

        #endregion

        public override void Dispose()
        {
        }
    }

    [Serializable]
    [XmlInclude(typeof(AngledDivertDatcomAusInfo))]
    public class AngledDivertDatcomAusInfo : ProtocolInfo
    {
        public bool controllerPoint = false;
        public string failedToDivertLocation = null;
        public bool removeFromRoutingTable = true;

        public string straightRoutingCode = null;
        public string straightRoutingLocation = null;

        public string divertRoutingCode = null;
        public string divertRoutingLocation = null;
        public MHEControllerAUS_Case.FailedDatcomAusMessageType failedToDivertMessageType =  MHEControllerAUS_Case.FailedDatcomAusMessageType._02;
        public string failedToDivertMessageReasonCode = "@@";
    }
}