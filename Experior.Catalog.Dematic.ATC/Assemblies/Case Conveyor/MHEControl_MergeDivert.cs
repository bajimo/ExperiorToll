using Experior.Catalog.Dematic.Case;
using Experior.Catalog.Dematic.Case.Components;
using Experior.Core.Loads;
using Experior.Core.Properties;
using Experior.Dematic.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Serialization;

namespace Experior.Catalog.Dematic.ATC.Assemblies.CaseConveyor
{
    public class MHEControl_MergeDivert : MHEControl
    {
        private MergeDivertATCInfo mergeDivertDatcomInfo;
        private MergeDivertConveyor mergeDivertConveyor;
        private MHEController_Case casePLC;
        private List<string> LeftRoutes = null;
        private List<string> RightRoutes = null;
        private List<string> StraightRoutes = null;
        private List<string> PriorityRoutes = null;
        
        public MHEControl_MergeDivert(MergeDivertATCInfo info, MergeDivertConveyor mergeDivert)
        {
            mergeDivertConveyor = mergeDivert;
            mergeDivertDatcomInfo = info;
            Info = info;  // set this to save properties 
            mergeDivertConveyor.divertArrival = divertArrival;
            mergeDivertConveyor.loadDeleted = loadDeleted;

            mergeDivertConveyor.releasedStraight = releasedStraight;
            mergeDivertConveyor.releasedLeft = releasedLeft;
            mergeDivertConveyor.releasedRight = releasedRight;
            
            casePLC = mergeDivertConveyor.Controller as MHEController_Case;

            //Anything with setup code in the "set" of a property except setting the value will need to be called 
            //explicitly so that the "set" code will execute when loading from a saved configuration
            LeftRoutingCode = info.leftRoutingCode;
            RightRoutingCode = info.rightRoutingCode;
            StraightRoutingCode = info.straightRoutingCode;
        }

        bool divertArrival(Load load)
        {
            IATCCaseLoadType atcLoad = load as IATCCaseLoadType;

            if (LoadDestination && mergeDivertConveyor.Name == atcLoad.Destination && !atcLoad.LoadWaitingForWCS)
            {
                atcLoad.Stop();
                atcLoad.LoadWaitingForWCS = true;
                atcLoad.Location = mergeDivertConveyor.Name;
                casePLC.SendTransportFinishedTelegram(atcLoad);
                return true;
                //Send a message to WMS and wait for the routing
            }

            List<Direction> validRoutes = new List<Direction>();

            Case_Load caseload = load as Case_Load;
            if (atcLoad.Destination != null && mergeDivertConveyor.LeftMode == Modes.Divert && LeftRoutes != null && LeftRoutes.Contains(atcLoad.Destination))
            {
                validRoutes.Add(Direction.Left);
            }

            if (atcLoad.Destination != null && mergeDivertConveyor.RightMode == Modes.Divert && RightRoutes != null && RightRoutes.Contains(atcLoad.Destination))
            {
                validRoutes.Add(Direction.Right);
            }

            if (atcLoad.Destination != null && mergeDivertConveyor.StraightMode == Modes.Divert && StraightRoutes != null && StraightRoutes.Contains(atcLoad.Destination))
            {
                validRoutes.Add(Direction.Straight);
            }

            //Check if the load has the priority bit set
            bool priority = false;
            if (atcLoad.Destination != null && PriorityRoutes != null && PriorityRoutes.Contains(atcLoad.Destination))
            {
                priority = true;
            }

            mergeDivertConveyor.RouteLoad(load, validRoutes, priority);
            return true; //returns true if handled by this controller
        }

        bool loadDeleted(Load load)
        {
            return true;
        }

        void loadReleasedComplete(Case_Load caseLoad)
        {

        }

        bool loadRoutedFailedToDivert(Case_Load caseLoad)
        {
            if (mergeDivertConveyor.failedToDivertLoad == caseLoad && !string.IsNullOrEmpty(FailedToDivertStatus) && !string.IsNullOrEmpty(FailedToDivertLocation)) //Load has been routed to Default route so send failed to divert location
            {
                SendTransportFinishedTelegram(caseLoad, FailedToDivertLocation, FailedToDivertStatus);
                return true;
            }
            else if (mergeDivertConveyor.failedToDivertLoad == caseLoad && !string.IsNullOrEmpty(FailedToDivertStatus))
            {
                ATCCaseLoad atcCaseLoad = caseLoad as ATCCaseLoad;
                atcCaseLoad.PresetStateCode = FailedToDivertStatus;
            }
            return false;
        }

        bool releasedStraight(Load load)
        {
            Case_Load caseLoad = load as Case_Load;
            if (!loadRoutedFailedToDivert(caseLoad) && !string.IsNullOrEmpty(StraightRoutingLocation))
            {
                SendConfirmationTelegram(caseLoad, StraightRoutingLocation);
            }
            loadReleasedComplete(caseLoad);
            return true;
        }

        bool releasedLeft(Load load)
        {
            Case_Load caseLoad = load as Case_Load;
            if (!loadRoutedFailedToDivert(caseLoad) && !string.IsNullOrEmpty(LeftRoutingLocation))
            {
                SendConfirmationTelegram(caseLoad, LeftRoutingLocation);
            }
            loadReleasedComplete(caseLoad);
            return true;
        }

        bool releasedRight(Load load)
        {
            Case_Load caseLoad = load as Case_Load;
            if (!loadRoutedFailedToDivert(caseLoad) && !string.IsNullOrEmpty(RightRoutingLocation))
            {
                SendConfirmationTelegram(caseLoad, RightRoutingLocation);
            }
            loadReleasedComplete(caseLoad);
            return true;
        }

        private void SendConfirmationTelegram(Case_Load caseLoad, string Location)
        {
            IATCCaseLoadType atcLoad = caseLoad as IATCCaseLoadType;
            atcLoad.Location = Location;
            switch (DivertMessageType)
            {
                case DivertTelegram.LocationArrived: casePLC.SendLocationArrivedTelegram(caseLoad as IATCCaseLoadType); break;
                case DivertTelegram.LocationLeft: casePLC.SendLocationLeftTelegram(caseLoad as IATCCaseLoadType); break;
                case DivertTelegram.TransportFinished: casePLC.SendTransportFinishedTelegram(caseLoad as IATCCaseLoadType); break;
                case DivertTelegram.TransportRequest: casePLC.SendTransportRequestTelegram(caseLoad as IATCCaseLoadType); break;
            }
        }
        
        private void SendTransportFinishedTelegram(Case_Load caseLoad, string Location, string status)
        {
            IATCCaseLoadType atcLoad = caseLoad as IATCCaseLoadType;
            atcLoad.Location = Location;
            if (status != null)
            {
                atcLoad.PresetStateCode = status;
            }
            casePLC.SendTransportFinishedTelegram(caseLoad as IATCCaseLoadType);
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
                return mergeDivertDatcomInfo.controllerPoint;
            }
            set
            {
                mergeDivertDatcomInfo.controllerPoint = value;
            }
        }

        [Category("Configuration")]
        [DisplayName("Load Destination")]
        [Description("Set this true if a TransportFinishedTelegram should be sent when the load destination equals this location (Conveyor Name), the load will then wait for a new destination from the ATC")]
        [PropertyOrder(2)]
        public bool LoadDestination
        {
            get
            {
                return mergeDivertDatcomInfo.loadDestination;
            }
            set
            {
                mergeDivertDatcomInfo.loadDestination = value;
            }
        }


        [Category("Configuration")]
        [DisplayName("Failed To Divert Location")]
        [Description("If the load is routed to the default route and the failed to divert location is entered, then a notification message will be sent with this location, the failed to divert location and the deafult route location should not both be entered")]
        [PropertyAttributesProvider("DynamicPropertyFailedToDivertLocation")]
        [PropertyOrder(6)]
        public string FailedToDivertLocation
        {
            get { return mergeDivertDatcomInfo.failedToDivertLocation; }
            set
            {
                if (value == "")
                    mergeDivertDatcomInfo.failedToDivertLocation = null;
                else
                {
                    mergeDivertDatcomInfo.failedToDivertLocation = value;
                }
            }
        }

        [Category("Configuration")]
        [DisplayName("Failed To Divert Status")]
        [Description("What is the status code to send when the load fails to divert")]
        [PropertyAttributesProvider("DynamicPropertyFailedToDivertLocation")]
        [PropertyOrder(6)]
        public string FailedToDivertStatus
        {
            get { return mergeDivertDatcomInfo.failedToDivertStatus; }
            set
            {
                if (value == "")
                    mergeDivertDatcomInfo.failedToDivertStatus = null;
                else
                {
                    mergeDivertDatcomInfo.failedToDivertStatus = value;
                }
            }
        }

        public void DynamicPropertyFailedToDivertLocation(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = mergeDivertConveyor.RouteBlockedBehaviour != RouteBlocked.Wait_Until_Route_Available;
        }

        [Category("Straight Divert")]
        [DisplayName("Straight Routing Code")]
        [Description("Routing destinations of the load for routing Straight: format xxxx,yyyy if the load destination is set to either xxxx or yyyy it will be routed Straight")]
        [PropertyAttributesProvider("DynamicPropertyStraightModeDivert")]
        [PropertyOrder(7)]
        public string StraightRoutingCode
        {
            get { return mergeDivertDatcomInfo.straightRoutingCode; }
            set
            {
                if (value == null || value == "")
                {
                    mergeDivertDatcomInfo.straightRoutingCode = null;
                    StraightRoutes = null;
                    return;
                }

                string[] splitRoutes = value.Split(',');
                StraightRoutes = new List<string>();
                foreach (string route in splitRoutes)
                {
                    StraightRoutes.Add(route);
                }

                if (StraightRoutes != null)
                {
                    mergeDivertDatcomInfo.straightRoutingCode = value;
                }
            }
        }

        [Category("Straight Divert")]
        [DisplayName("Straight Route Location")]
        [Description("Location name in message to ATC when successfully diverted, if blank then no divert confirmation message will be sent")]
        [PropertyAttributesProvider("DynamicPropertyStraightModeDivert")]
        [PropertyOrder(8)]
        public string StraightRoutingLocation
        {
            get { return mergeDivertDatcomInfo.straightRoutingLocation; }
            set
            {
                if (value == "")
                    mergeDivertDatcomInfo.straightRoutingLocation = null;
                else
                {
                    mergeDivertDatcomInfo.straightRoutingLocation = value;
                }
            }
        }

        public void DynamicPropertyStraightModeDivert(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = mergeDivertConveyor.StraightMode == Modes.Divert;
        }

        [Category("Divert")]
        [DisplayName("Divert Message Type")]
        [Description("What type of message should be sent when confirming a divert")]
        [PropertyOrder(9)]
        public DivertTelegram DivertMessageType
        {
            get { return mergeDivertDatcomInfo.divertMessageType; }
            set { mergeDivertDatcomInfo.divertMessageType = value; }
        }

        [Category("Left Divert")]
        [DisplayName("Left Routing Code")]
        [Description("Routing destinations of the load for routing Left: format xxxx,yyyy if the load destination is set to either xxxx or yyyy it will be routed Left")]
        [PropertyAttributesProvider("DynamicPropertyLeftModeDivert")]
        [PropertyOrder(7)]
        public string LeftRoutingCode
        {
            get { return mergeDivertDatcomInfo.leftRoutingCode; }
            set
            {
                if (value == null || value == "")
                {
                    mergeDivertDatcomInfo.leftRoutingCode = null;
                    LeftRoutes = null;
                    return;
                }

                string[] splitRoutes = value.Split(',');
                LeftRoutes = new List<string>();
                foreach(string route in splitRoutes)
                {
                    LeftRoutes.Add(route);
                }

                if (LeftRoutes != null)
                {
                    mergeDivertDatcomInfo.leftRoutingCode = value;
                }
            }
        }

        [Category("Left Divert")]
        [DisplayName("Left Route Location")]
        [Description("Location name in message to ATC when successfully diverted, if blank then no divert confirmation message will be sent")]
        [PropertyAttributesProvider("DynamicPropertyLeftModeDivert")]
        [PropertyOrder(8)]
        public string LeftRoutingLocation
        {
            get { return mergeDivertDatcomInfo.leftRoutingLocation; }
            set
            {
                if (value == "")
                    mergeDivertDatcomInfo.leftRoutingLocation = null;
                else
                {
                    mergeDivertDatcomInfo.leftRoutingLocation = value;
                }
            }
        }

        public void DynamicPropertyLeftModeDivert(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = mergeDivertConveyor.LeftMode == Modes.Divert;
        }

        [Category("Right Divert")]
        [DisplayName("Right Routing Code")]
        [Description("Routing destinations of the load for routing Right: format xxxx,yyyy if the load destination is set to either xxxx or yyyy it will be routed Right")]
        [PropertyAttributesProvider("DynamicPropertyRightModeDivert")]
        [PropertyOrder(7)]
        public string RightRoutingCode
        {
            get { return mergeDivertDatcomInfo.rightRoutingCode; }
            set
            {
                if (value == null || value == "")
                {
                    mergeDivertDatcomInfo.rightRoutingCode = null;
                    RightRoutes = null;
                    return;
                }

                string[] splitRoutes = value.Split(',');
                RightRoutes = new List<string>();
                foreach (string route in splitRoutes)
                {
                    RightRoutes.Add(route);
                }

                if (RightRoutes != null)
                {
                    mergeDivertDatcomInfo.rightRoutingCode = value;
                }
            }
        }

        [Category("Right Divert")]
        [DisplayName("Right Route Location")]
        [Description("Location name in message to ATC when successfully diverted, if blank then no divert confirmation message will be sent")]
        [PropertyAttributesProvider("DynamicPropertyRightModeDivert")]
        [PropertyOrder(8)]
        public string RightRoutingLocation
        {
            get { return mergeDivertDatcomInfo.rightRoutingLocation; }
            set
            {
                if (value == "")
                    mergeDivertDatcomInfo.rightRoutingLocation = null;
                else
                {
                    mergeDivertDatcomInfo.rightRoutingLocation = value;
                }
            }
        }

        public void DynamicPropertyRightModeDivert(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = mergeDivertConveyor.RightMode == Modes.Divert;
        }

        [Category("Priority Divert")]
        [DisplayName("Priority Routing Code")]
        [Description("Routing destinations of the load for routing Priority: format xxxx,yyyy if the load destination is set to either xxxx or yyyy it will be routed as a priority based on the route clocked behaviour")]
        [PropertyAttributesProvider("DynamicPropertyRouteBlockedTimeout")]
        [PropertyOrder(9)]
        public string PriorityRoutingCode
        {
            get { return mergeDivertDatcomInfo.priorityRoutingCode; }
            set
            {
                if (value == null || value == "")
                {
                    mergeDivertDatcomInfo.priorityRoutingCode = null;
                    PriorityRoutes = null;
                    return;
                }

                string[] splitRoutes = value.Split(',');
                PriorityRoutes = new List<string>();
                foreach (string route in splitRoutes)
                {
                    PriorityRoutes.Add(route);
                }

                if (PriorityRoutes != null)
                {
                    mergeDivertDatcomInfo.priorityRoutingCode = value;
                }
            }
        }

        public void DynamicPropertyRouteBlockedTimeout(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = mergeDivertConveyor.RouteBlockedBehaviour == RouteBlocked.Wait_Timeout;
        }

        #endregion

        public override void Dispose()
        {
            //throw new NotImplementedException();
        }


    }

    [Serializable]
    [XmlInclude(typeof(MergeDivertATCInfo))]
    public class MergeDivertATCInfo : ProtocolInfo
    {
        public bool controllerPoint = false;
        public bool loadDestination = false;

        public string failedToDivertLocation = null;
        public string failedToDivertStatus = null;

        public string straightRoutingCode = null;
        public string straightRoutingLocation = null;

        public string leftRoutingCode = null;
        public string leftRoutingLocation = null;

        public string rightRoutingCode = null;
        public string rightRoutingLocation = null;

        public string priorityRoutingCode = null;

        public DivertTelegram divertMessageType = DivertTelegram.TransportFinished;
    }

    public enum DivertTelegram
    {
        TransportFinished,
        TransportRequest,
        LocationLeft,
        LocationArrived
    }

}