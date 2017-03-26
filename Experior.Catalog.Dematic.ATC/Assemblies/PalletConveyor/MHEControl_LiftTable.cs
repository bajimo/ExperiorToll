using Experior.Catalog.Dematic.Pallet;
using Experior.Catalog.Dematic.Pallet.Assemblies;
using Experior.Core.Loads;
using Experior.Core.Properties;
using Experior.Dematic.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Serialization;

namespace Experior.Catalog.Dematic.ATC.Assemblies.PalletConveyor
{
    public class MHEControl_LiftTable : MHEControl
    {
        private LiftTableATCInfo liftTableATCInfo;
        private LiftTable liftTableConveyor;
        private MHEController_Pallet palletPLC;
        private List<string> LeftRoutes = null;
        private List<string> RightRoutes = null;
        private List<string> StraightRoutes = null;
        private List<string> PriorityRoutes = null;
        
        public MHEControl_LiftTable(LiftTableATCInfo info, LiftTable mergeDivert)
        {
            liftTableConveyor = mergeDivert;
            liftTableATCInfo = info;
            Info = info;  // set this to save properties 
            liftTableConveyor.divertArrival = divertArrival;
            liftTableConveyor.loadDeleted = loadDeleted;

            liftTableConveyor.releasedStraight = releasedStraight;
            liftTableConveyor.releasedLeft = releasedLeft;
            liftTableConveyor.releasedRight = releasedRight;
            
            palletPLC = liftTableConveyor.Controller as MHEController_Pallet;

            //Anything with setup code in the "set" of a property except setting the value will need to be called 
            //explicitly so that the "set" code will execute when loading from a saved configuration
            LeftRoutingCode = info.leftRoutingCode;
            RightRoutingCode = info.rightRoutingCode;
            StraightRoutingCode = info.straightRoutingCode;
        }

        bool divertArrival(Load load)
        {
            IATCPalletLoadType atcLoad = load as IATCPalletLoadType;

            if (LoadDestination && liftTableConveyor.Name == atcLoad.Destination && !atcLoad.LoadWaitingForWCS)
            {
                atcLoad.Stop();
                atcLoad.LoadWaitingForWCS = true;
                atcLoad.Location = liftTableConveyor.Name;
                palletPLC.SendTransportFinishedTelegram(atcLoad);
                return true;
                //Send a message to WMS and wait for the routing
            }

            List<Direction> validRoutes = new List<Direction>();

            Experior.Dematic.Base.EuroPallet caseload = load as Experior.Dematic.Base.EuroPallet;
            if (atcLoad.Destination != null && liftTableConveyor.LeftMode == Modes.Divert && LeftRoutes != null && LeftRoutes.Contains(atcLoad.Destination))
            {
                validRoutes.Add(Direction.Left);
            }

            if (atcLoad.Destination != null && liftTableConveyor.RightMode == Modes.Divert && RightRoutes != null && RightRoutes.Contains(atcLoad.Destination))
            {
                validRoutes.Add(Direction.Right);
            }

            if (atcLoad.Destination != null && liftTableConveyor.StraightMode == Modes.Divert && StraightRoutes != null && StraightRoutes.Contains(atcLoad.Destination))
            {
                validRoutes.Add(Direction.Straight);
            }

            if (validRoutes.Count == 0 && liftTableConveyor.DefaultRouting == Direction.None)
            {
                if (liftTableConveyor.StraightMode == Modes.Divert)
                {
                    validRoutes.Add(Direction.Straight);
                }
                if (liftTableConveyor.RightMode == Modes.Divert)
                {
                    validRoutes.Add(Direction.Right);
                }
                if (liftTableConveyor.LeftMode == Modes.Divert)
                {
                    validRoutes.Add(Direction.Left);
                }
            }

            //Check if the load has the priority bit set
            bool priority = false;
            if (atcLoad.Destination != null && PriorityRoutes != null && PriorityRoutes.Contains(atcLoad.Destination))
            {
                priority = true;
            }

            liftTableConveyor.RouteLoad(load, validRoutes, priority);
            return true; //returns true if handled by this controller
        }

        bool loadDeleted(Load load)
        {
            return true;
        }

        void loadReleasedComplete(Experior.Dematic.Base.EuroPallet caseLoad)
        {

        }

        bool loadRoutedFailedToDivert(Experior.Dematic.Base.EuroPallet caseLoad)
        {
            //if (liftTableConveyor.failedToDivertLoad == caseLoad && FailedToDivertLocation != null) //Load has been routed to Default route so send failed to divert location
            //{
            //    SendTransportFinishedTelegram(caseLoad, FailedToDivertLocation, FailedToDivertStatus);
            //    return true;
            //}
            //else
                return false;
        }

        bool releasedStraight(Load load)
        {
            Experior.Dematic.Base.EuroPallet palletLoad = load as Experior.Dematic.Base.EuroPallet;
            //if (!loadRoutedFailedToDivert(palletLoad) && !string.IsNullOrEmpty(StraightRoutingLocation))
            if (!string.IsNullOrEmpty(StraightRoutingLocation))
            {
                SendLocationLeftTelegram(palletLoad, StraightRoutingLocation);
            }
            loadReleasedComplete(palletLoad);
            return true;
        }

        bool releasedLeft(Load load)
        {
            Experior.Dematic.Base.EuroPallet palletLoad = load as Experior.Dematic.Base.EuroPallet;
            //if (!loadRoutedFailedToDivert(palletLoad) && !string.IsNullOrEmpty(LeftRoutingLocation))
            if (!string.IsNullOrEmpty(LeftRoutingLocation))
            {
                SendLocationLeftTelegram(palletLoad, LeftRoutingLocation);
            }
            loadReleasedComplete(palletLoad);
            return true;
        }

        bool releasedRight(Load load)
        {
            Experior.Dematic.Base.EuroPallet palletLoad = load as Experior.Dematic.Base.EuroPallet;
            //if (!loadRoutedFailedToDivert(palletLoad) && !string.IsNullOrEmpty(RightRoutingLocation))
            if (!string.IsNullOrEmpty(RightRoutingLocation))
            {
                SendLocationLeftTelegram(palletLoad, RightRoutingLocation);
            }
            loadReleasedComplete(palletLoad);
            return true;
        }

        private void SendLocationLeftTelegram(Experior.Dematic.Base.EuroPallet palletLoad, string Location)
        {
            IATCLoadType atcLoad = palletLoad as IATCLoadType;
            atcLoad.Location = Location;
            palletPLC.SendLocationLeftTelegram(palletLoad as IATCLoadType);
        }

        //private void SendConfirmationTelegram(Experior.Dematic.Base.EuroPallet palletLoad, string Location)
        //{
        //    IATCLoadType atcLoad = palletLoad as IATCLoadType;
        //    atcLoad.Location = Location;
        //    switch (DivertMessageType)
        //    {
        //        case DivertTelegram.LocationArrived: palletPLC.SendLocationArrivedTelegram(palletLoad as IATCLoadType); break;
        //        case DivertTelegram.LocationLeft: palletPLC.SendLocationLeftTelegram(palletLoad as IATCLoadType); break;
        //        case DivertTelegram.TransportFinished: palletPLC.SendTransportFinishedTelegram(palletLoad as IATCLoadType); break;
        //        case DivertTelegram.TransportRequest: palletPLC.SendTransportRequestTelegram(palletLoad as IATCLoadType); break;
        //    }
        //}

        //private void SendTransportFinishedTelegram(Experior.Dematic.Base.EuroPallet palletLoad, string Location, string status)
        //{
        //    IATCPalletLoadType atcLoad = palletLoad as IATCPalletLoadType;
        //    atcLoad.Location = Location;
        //    if (status != null)
        //    {
        //        atcLoad.PresetStateCode = status;
        //    }
        //    palletPLC.SendTransportFinishedTelegram(palletLoad as IATCPalletLoadType);
        //}

        #region User Interface
        [Category("Configuration")]
        [DisplayName("Controller Point")]
        [Description("Set true if the controller script should handle the routing. The routing will not be handleb by the selected PLC, however the configuration can still be used for routing within the controller script")]
        [PropertyOrder(1)]
        public bool ControllerPoint
        {
            get
            {
                return liftTableATCInfo.controllerPoint;
            }
            set
            {
                liftTableATCInfo.controllerPoint = value;
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
                return liftTableATCInfo.loadDestination;
            }
            set
            {
                liftTableATCInfo.loadDestination = value;
            }
        }


        //[Category("Configuration")]
        //[DisplayName("Failed To Divert Location")]
        //[Description("If the load is routed to the default route and the failed to divert location is entered, then a notification message will be sent with this location, the failed to divert location and the deafult route location should not both be entered")]
        //[PropertyAttributesProvider("DynamicPropertyFailedToDivertLocation")]
        //[PropertyOrder(6)]
        //public string FailedToDivertLocation
        //{
        //    get { return mergeDivertDatcomInfo.failedToDivertLocation; }
        //    set
        //    {
        //        if (value == "")
        //            mergeDivertDatcomInfo.failedToDivertLocation = null;
        //        else
        //        {
        //            mergeDivertDatcomInfo.failedToDivertLocation = value;
        //        }
        //    }
        //}

        //[Category("Configuration")]
        //[DisplayName("Failed To Divert Status")]
        //[Description("What is the status code to send when the load fails to divert")]
        //[PropertyAttributesProvider("DynamicPropertyFailedToDivertLocation")]
        //[PropertyOrder(6)]
        //public string FailedToDivertStatus
        //{
        //    get { return mergeDivertDatcomInfo.failedToDivertStatus; }
        //    set
        //    {
        //        if (value == "")
        //            mergeDivertDatcomInfo.failedToDivertStatus = null;
        //        else
        //        {
        //            mergeDivertDatcomInfo.failedToDivertStatus = value;
        //        }
        //    }
        //}

        public void DynamicPropertyFailedToDivertLocation(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = liftTableConveyor.RouteBlockedBehaviour != RouteBlocked.Wait_Until_Route_Available;
        }

        [Category("Straight Divert")]
        [DisplayName("Straight Routing Code")]
        [Description("Routing destinations of the load for routing Straight: format xxxx,yyyy if the load destination is set to either xxxx or yyyy it will be routed Straight")]
        [PropertyAttributesProvider("DynamicPropertyStraightModeDivert")]
        [PropertyOrder(7)]
        public string StraightRoutingCode
        {
            get { return liftTableATCInfo.straightRoutingCode; }
            set
            {
                if (value == null || value == "")
                {
                    liftTableATCInfo.straightRoutingCode = null;
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
                    liftTableATCInfo.straightRoutingCode = value;
                }
            }
        }

        [Category("Straight Divert")]
        [DisplayName("Straight Route Location")]
        [Description("Location name in LocationLeftTelegram to ATC when successfully diverted, if blank then no message will be sent")]
        [PropertyAttributesProvider("DynamicPropertyStraightModeDivert")]
        [PropertyOrder(8)]
        public string StraightRoutingLocation
        {
            get { return liftTableATCInfo.straightRoutingLocation; }
            set
            {
                if (value == "")
                    liftTableATCInfo.straightRoutingLocation = null;
                else
                {
                    liftTableATCInfo.straightRoutingLocation = value;
                }
            }
        }

        public void DynamicPropertyStraightModeDivert(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = liftTableConveyor.StraightMode == Modes.Divert;
        }

        //[Category("Divert")]
        //[DisplayName("Divert Message Type")]
        //[Description("What type of message should be sent when confirming a divert")]
        //[PropertyOrder(9)]
        //public DivertTelegram DivertMessageType
        //{
        //    get { return mergeDivertDatcomInfo.divertMessageType; }
        //    set { mergeDivertDatcomInfo.divertMessageType = value; }
        //}

        [Category("Left Divert")]
        [DisplayName("Left Routing Code")]
        [Description("Routing destinations of the load for routing Left: format xxxx,yyyy if the load destination is set to either xxxx or yyyy it will be routed Left")]
        [PropertyAttributesProvider("DynamicPropertyLeftModeDivert")]
        [PropertyOrder(7)]
        public string LeftRoutingCode
        {
            get { return liftTableATCInfo.leftRoutingCode; }
            set
            {
                if (value == null || value == "")
                {
                    liftTableATCInfo.leftRoutingCode = null;
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
                    liftTableATCInfo.leftRoutingCode = value;
                }
            }
        }

        [Category("Left Divert")]
        [DisplayName("Left Route Location")]
        [Description("Location name in LocationLeftTelegram to ATC when successfully diverted, if blank then no message will be sent")]
        [PropertyAttributesProvider("DynamicPropertyLeftModeDivert")]
        [PropertyOrder(8)]
        public string LeftRoutingLocation
        {
            get { return liftTableATCInfo.leftRoutingLocation; }
            set
            {
                if (value == "")
                    liftTableATCInfo.leftRoutingLocation = null;
                else
                {
                    liftTableATCInfo.leftRoutingLocation = value;
                }
            }
        }

        public void DynamicPropertyLeftModeDivert(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = liftTableConveyor.LeftMode == Modes.Divert;
        }

        [Category("Right Divert")]
        [DisplayName("Right Routing Code")]
        [Description("Routing destinations of the load for routing Right: format xxxx,yyyy if the load destination is set to either xxxx or yyyy it will be routed Right")]
        [PropertyAttributesProvider("DynamicPropertyRightModeDivert")]
        [PropertyOrder(7)]
        public string RightRoutingCode
        {
            get { return liftTableATCInfo.rightRoutingCode; }
            set
            {
                if (value == null || value == "")
                {
                    liftTableATCInfo.rightRoutingCode = null;
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
                    liftTableATCInfo.rightRoutingCode = value;
                }
            }
        }

        [Category("Right Divert")]
        [DisplayName("Right Route Location")]
        [Description("Location name in LocationLeftTelegram to ATC when successfully diverted, if blank then no message will be sent")]
        [PropertyAttributesProvider("DynamicPropertyRightModeDivert")]
        [PropertyOrder(8)]
        public string RightRoutingLocation
        {
            get { return liftTableATCInfo.rightRoutingLocation; }
            set
            {
                if (value == "")
                    liftTableATCInfo.rightRoutingLocation = null;
                else
                {
                    liftTableATCInfo.rightRoutingLocation = value;
                }
            }
        }

        public void DynamicPropertyRightModeDivert(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = liftTableConveyor.RightMode == Modes.Divert;
        }

        [Category("Priority Divert")]
        [DisplayName("Priority Routing Code")]
        [Description("Routing destinations of the load for routing Priority: format xxxx,yyyy if the load destination is set to either xxxx or yyyy it will be routed as a priority based on the route clocked behaviour")]
        [PropertyAttributesProvider("DynamicPropertyRouteBlockedTimeout")]
        [PropertyOrder(9)]
        public string PriorityRoutingCode
        {
            get { return liftTableATCInfo.priorityRoutingCode; }
            set
            {
                if (value == null || value == "")
                {
                    liftTableATCInfo.priorityRoutingCode = null;
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
                    liftTableATCInfo.priorityRoutingCode = value;
                }
            }
        }

        public void DynamicPropertyRouteBlockedTimeout(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = liftTableConveyor.RouteBlockedBehaviour == RouteBlocked.Wait_Timeout;
        }

        #endregion

        public override void Dispose()
        {
            //throw new NotImplementedException();
        }
    }

    [Serializable]
    [XmlInclude(typeof(LiftTableATCInfo))]
    public class LiftTableATCInfo : ProtocolInfo
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

        //public DivertTelegram divertMessageType = DivertTelegram.TransportFinished;
    }

    //public enum DivertTelegram
    //{
    //    TransportFinished,
    //    TransportRequest,
    //    LocationLeft,
    //    LocationArrived
    //}

}