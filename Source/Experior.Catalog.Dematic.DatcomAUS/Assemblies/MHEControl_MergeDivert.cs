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
    public class MHEControl_MergeDivert : MHEControl
    {
        private MergeDivertDatcomAusInfo mergeDivertDatcomInfo;
        private MergeDivertConveyor mergeDivertConveyor;
        private MHEControllerAUS_Case casePLC;
        private List<string> LeftRoutes = null;
        private List<string> RightRoutes = null;
        private List<string> StraightRoutes = null;
        private List<string> PriorityRoutes = null;
        //Check if the load should divert if in the correct loop
        private List<string> StraightAndRoutes = null;
        private List<string> LeftAndRoutes = null;
        private List<string> RightAndRoutes = null;
        private List<string> StraightOrRoutes = null;
        private List<string> LeftOrRoutes = null;
        private List<string> RightOrRoutes = null;

        public MHEControl_MergeDivert(MergeDivertDatcomAusInfo info, MergeDivertConveyor mergeDivert)
        {
            mergeDivertConveyor = mergeDivert;
            mergeDivertDatcomInfo = info;
            Info = info;  // set this to save properties 
            mergeDivertConveyor.divertArrival = divertArrival;
            mergeDivertConveyor.loadDeleted = loadDeleted;

            mergeDivertConveyor.releasedStraight = releasedStraight;
            mergeDivertConveyor.releasedLeft = releasedLeft;
            mergeDivertConveyor.releasedRight = releasedRight;

            casePLC = mergeDivertConveyor.Controller as MHEControllerAUS_Case;

            //Anything with setup code in the "set" of a property except setting the value will need to be called 
            //explicitly so that the "set" code will execute when loading from a saved configuration
            LeftRoutingCode = info.leftRoutingCode;
            RightRoutingCode = info.rightRoutingCode;
            StraightRoutingCode = info.straightRoutingCode;
        }

        bool divertArrival(Load load)
        {
            List<Direction> validRoutes = new List<Direction>();

            Case_Load caseload = load as Case_Load;
            if (mergeDivertConveyor.LeftMode == Modes.Divert &&
                ((casePLC.DivertSet(caseload.Identification, LeftRoutes) &&
                (LeftAndRoutes == null || casePLC.DivertSet(caseload.Identification, LeftAndRoutes))) ||
                (LeftOrRoutes != null && casePLC.DivertSet(caseload.Identification, LeftOrRoutes))))
            {
                validRoutes.Add(Direction.Left);
            }

            if (mergeDivertConveyor.RightMode == Modes.Divert &&
                ((casePLC.DivertSet(caseload.Identification, RightRoutes) &&
                (RightAndRoutes == null || casePLC.DivertSet(caseload.Identification, RightAndRoutes))) ||
                (RightOrRoutes != null && casePLC.DivertSet(caseload.Identification, RightOrRoutes))))
            //if (casePLC.DivertSet(caseload.SSCCBarcode, RightRoutes) && mergeDivertConveyor.RightMode == MergeDivertConveyor.Modes.Divert)
            {
                validRoutes.Add(Direction.Right);
            }
            if (mergeDivertConveyor.StraightMode == Modes.Divert &&
                ((casePLC.DivertSet(caseload.Identification, StraightRoutes) &&
                (StraightAndRoutes == null || casePLC.DivertSet(caseload.Identification, StraightAndRoutes))) ||
                (StraightOrRoutes != null && casePLC.DivertSet(caseload.Identification, StraightOrRoutes))))
            //if (casePLC.DivertSet(caseload.SSCCBarcode, StraightRoutes) && mergeDivertConveyor.StraightMode == MergeDivertConveyor.Modes.Divert)
            {
                validRoutes.Add(Direction.Straight);
            }

            //Check if the load has the priority bit set
            bool priority = false;
            if (casePLC.DivertSet(caseload.Identification, new List<string>()))
            {
                priority = true;
            }

            mergeDivertConveyor.RouteLoad(load, validRoutes, priority);
            return true; //returns true if handled by this controller
        }

        bool loadDeleted(Load load)
        {
            Case_Load caseLoad = load as Case_Load;

            casePLC.RoutingTable.Remove(caseLoad.Identification);

            return true;
        }

        bool loadRoutedFailedToDivert(Case_Load caseLoad)
        {
            if (mergeDivertConveyor.failedToDivertLoad == caseLoad && FailedToDivertLocation != null) //Load has been routed to Default route so send failed to divert location
            {
                casePLC.SendArrivalMessage(FailedToDivertLocation, caseLoad);
                return true;
            }
            else
                return false;
        }

        bool releasedStraight(Load load)
        {
            Case_Load caseLoad = load as Case_Load;
            if (!loadRoutedFailedToDivert(caseLoad) && StraightRoutingLocation != string.Empty)
                casePLC.SendArrivalMessage(StraightRoutingLocation, caseLoad);
            loadReleasedComplete(caseLoad);
            return true;
        }

        bool releasedLeft(Load load)
        {
            Case_Load caseLoad = load as Case_Load;
            if (!loadRoutedFailedToDivert(caseLoad) && LeftRoutingLocation != string.Empty)
                casePLC.SendArrivalMessage(LeftRoutingLocation, caseLoad);
            loadReleasedComplete(caseLoad);
            return true;
        }

        bool releasedRight(Load load)
        {
            Case_Load caseLoad = load as Case_Load;
            if (!loadRoutedFailedToDivert(caseLoad) && RightRoutingLocation != string.Empty)
                casePLC.SendArrivalMessage(RightRoutingLocation, caseLoad);
            loadReleasedComplete(caseLoad);
            return true;
        }

        void loadReleasedComplete(Case_Load caseLoad)
        {
            removeFromRoutingTable(caseLoad);
        }

        void removeFromRoutingTable(Case_Load caseLoad)
        {
            if (RemoveFromRoutingTable)
                casePLC.RoutingTable.Remove(caseLoad.Identification);
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
        [DisplayName("Remove From Routing Table")]
        [Description("When load has been routed should the entry in the PLC routing table be removed")]
        [PropertyOrder(5)]
        public bool RemoveFromRoutingTable
        {
            get { return mergeDivertDatcomInfo.removeFromRoutingTable; }
            set { mergeDivertDatcomInfo.removeFromRoutingTable = value; }
        }

        [Category("Configuration")]
        [DisplayName("Failed To Divert Location")]
        [Description("If the load is routed to the default route and the failed to divert location is entered, then a divert confirmation message will be sent with this location, the failed to divert location and the deafult route location should not both be entered")]
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

        public void DynamicPropertyFailedToDivertLocation(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = mergeDivertConveyor.RouteBlockedBehaviour != RouteBlocked.Wait_Until_Route_Available;
        }

        [Category("Straight Divert")]
        [DisplayName("Straight Routing Code")]
        [Description("Routing code for straight routing. format destination1,destination2")]
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
                    StraightAndRoutes = null;
                    StraightOrRoutes = null;
                    return;
                }

                string[] splitRoutes = new string[] { value };

                if (value.Contains("&"))
                {
                    splitRoutes = value.Split('&');
                    string[] splitOrRoutes = new string[] { splitRoutes[1] };

                    if (splitRoutes[1].Contains("|"))
                    {
                        splitOrRoutes = splitRoutes[1].Split('|');
                        StraightOrRoutes = casePLC.ValidateRoutingCode(splitOrRoutes[1]);
                    }
                    else
                    {
                        StraightOrRoutes = null;
                    }
                    StraightAndRoutes = casePLC.ValidateRoutingCode(splitOrRoutes[0]);
                }
                else
                {
                    StraightAndRoutes = null;
                    StraightOrRoutes = null;
                }

                StraightRoutes = casePLC.ValidateRoutingCode(splitRoutes[0]);

                if (StraightRoutes != null)
                {
                    mergeDivertDatcomInfo.straightRoutingCode = value;
                }
            }
        }

        [Category("Straight Divert")]
        [DisplayName("Straight Route Location")]
        [Description("Location name in divert confirmation message when successfully diverted, if blank then no divert confirmation message will be sent")]
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

        [Category("Left Divert")]
        [DisplayName("Left Routing Code")]
        [Description("Routing code for straight routing: format destination1,destination2")]
        [PropertyAttributesProvider("DynamicPropertyLeftModeDivert")]
        [PropertyOrder(9)]
        public string LeftRoutingCode
        {
            get { return mergeDivertDatcomInfo.leftRoutingCode; }
            set
            {
                if (value == null || value == "")
                {
                    mergeDivertDatcomInfo.leftRoutingCode = null;
                    LeftRoutes = null;
                    LeftAndRoutes = null;
                    LeftOrRoutes = null;
                    return;
                }

                string[] splitRoutes = new string[] { value };

                if (value.Contains("&"))
                {
                    splitRoutes = value.Split('&');
                    string[] splitOrRoutes = new string[] { splitRoutes[1] };

                    if (splitRoutes[1].Contains("|"))
                    {
                        splitOrRoutes = splitRoutes[1].Split('|');
                        LeftOrRoutes = casePLC.ValidateRoutingCode(splitOrRoutes[1]);
                    }
                    else
                    {
                        LeftOrRoutes = null;
                    }
                    LeftAndRoutes = casePLC.ValidateRoutingCode(splitOrRoutes[0]);
                }
                else
                {
                    LeftAndRoutes = null;
                    LeftOrRoutes = null;
                }

                LeftRoutes = casePLC.ValidateRoutingCode(splitRoutes[0]);

                if (LeftRoutes != null)
                {
                    mergeDivertDatcomInfo.leftRoutingCode = value;
                }
            }
        }


        [Category("Left Divert")]
        [DisplayName("Left Route Location")]
        [Description("Location name in divert confirmation message when successfully diverted, if blank then no divert confirmation message will be sent")]
        [PropertyAttributesProvider("DynamicPropertyLeftModeDivert")]
        [PropertyOrder(10)]
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
        [Description("Routing code for straight routing: format destination1,destination2")]
        [PropertyAttributesProvider("DynamicPropertyRightModeDivert")]
        [PropertyOrder(11)]
        public string RightRoutingCode
        {
            get { return mergeDivertDatcomInfo.rightRoutingCode; }
            set
            {
                if (value == null || value == "")
                {
                    mergeDivertDatcomInfo.rightRoutingCode = null;
                    RightRoutes = null;
                    RightAndRoutes = null;
                    RightOrRoutes = null;
                    return;
                }

                string[] splitRoutes = new string[] { value };

                if (value.Contains("&"))
                {
                    splitRoutes = value.Split('&');
                    string[] splitOrRoutes = new string[] { splitRoutes[1] };

                    if (splitRoutes[1].Contains("|"))
                    {
                        splitOrRoutes = splitRoutes[1].Split('|');
                        RightOrRoutes = casePLC.ValidateRoutingCode(splitOrRoutes[1]);
                    }
                    else
                    {
                        RightOrRoutes = null;
                    }
                    RightAndRoutes = casePLC.ValidateRoutingCode(splitOrRoutes[0]);
                }
                else
                {
                    RightAndRoutes = null;
                    RightOrRoutes = null;
                }

                RightRoutes = casePLC.ValidateRoutingCode(splitRoutes[0]);

                if (RightRoutes != null)
                {
                    mergeDivertDatcomInfo.rightRoutingCode = value;
                }
            }
        }


        [Category("Right Divert")]
        [DisplayName("Right Route Location")]
        [Description("Location name in divert confirmation message when successfully diverted, if blank then no divert confirmation message will be sent")]
        [PropertyAttributesProvider("DynamicPropertyRightModeDivert")]
        [PropertyOrder(12)]
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
        [Description("Routing code for priority loads: format destination1,destination2,...,destionation n")]
        [PropertyAttributesProvider("DynamicPropertyRouteBlockedTimeout")]
        [PropertyOrder(13)]
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

                List<string> routes = casePLC.ValidateRoutingCode(value);
                if (routes != null)
                {
                    PriorityRoutes = routes;
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
    [XmlInclude(typeof(MergeDivertDatcomAusInfo))]
    public class MergeDivertDatcomAusInfo : ProtocolInfo
    {
        public bool controllerPoint = false;
        public string failedToDivertLocation = null;
        public bool removeFromRoutingTable = true;

        public string straightRoutingCode = null;
        public string straightRoutingLocation = null;

        public string leftRoutingCode = null;
        public string leftRoutingLocation = null;

        public string rightRoutingCode = null;
        public string rightRoutingLocation = null;

        public string priorityRoutingCode = null;
    }
}