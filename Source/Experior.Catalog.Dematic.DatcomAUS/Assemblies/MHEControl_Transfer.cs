using Experior.Catalog.Dematic.Case.Components;
using Experior.Core.Properties;
using Experior.Dematic.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Xml.Serialization;
using Experior.Core;
using Experior.Core.Loads;

namespace Experior.Catalog.Dematic.DatcomAUS.Assemblies
{
    class MHEControl_Transfer : MHEControl
    {
        private TransferDatcomAusInfo transferDatcomInfo;
        private Transfer theTransfer;
        private MHEControllerAUS_Case casePLC;

        private List<string> LeftRoutes = null; 
        private List<string> RightRoutes = null; 
        private List<string> PriorityRoutes = null;

        Timer LeftBlockedTimer = new Timer(1);
        Timer RightBlockedTimer = new Timer(1);

        public MHEControl_Transfer(TransferDatcomAusInfo info, Transfer transfer)
        {
            Info               = info;  // set this to save properties 
            transferDatcomInfo = info;
            theTransfer        = transfer;
            casePLC            = transfer.Controller as MHEControllerAUS_Case;
            theTransfer.OnDivertCompleteController += transfer_OnDivertCompleteController;
            theTransfer.OnArrivedAtTransferController += transfer_OnArrivedAtTransferController;
            theTransfer.OnTransferStatusChangedController += theTransfer_OnTransferStatusChangedController;
            LHSRoutingCode     = info.lhsRoutingCode;
            RHSRoutingCode     = info.rhsRoutingCode;

            LeftBlockedTimer.OnElapsed += LeftBlockedTimer_OnElapsed;
            RightBlockedTimer.OnElapsed += RightBlockedTimer_OnElapsed;
        }

        public override void Dispose()
        {
            theTransfer.OnArrivedAtTransferController -= transfer_OnArrivedAtTransferController;
            theTransfer.OnDivertCompleteController -= transfer_OnDivertCompleteController;
            theTransfer.OnTransferStatusChangedController -= theTransfer_OnTransferStatusChangedController;

            theTransfer = null;
            transferDatcomInfo = null;
        }   

        //Send the divert confirmation messages
        void transfer_OnDivertCompleteController(object sender, TransferDivertedArgs e)
        {
            Case_Load caseLoad = e._load as Case_Load;

            //First check if the load has failed to divert
            if (theTransfer.PreferredLoadRoutes.ContainsKey(caseLoad) && theTransfer.PreferredLoadRoutes[caseLoad] != e._divertedRoute && !string.IsNullOrEmpty(FailedRoutingLocation))
            {
                casePLC.SendDivertConfirmation(FailedRoutingLocation, caseLoad.SSCCBarcode);
            }
            else if (e._divertedRoute == Side.Right && !string.IsNullOrEmpty(RHSRoutingLocation))
            {
                casePLC.SendDivertConfirmation(RHSRoutingLocation, caseLoad.SSCCBarcode);
            }
            else if (e._divertedRoute == Side.Left && !string.IsNullOrEmpty(LHSRoutingLocation))
            {
                casePLC.SendDivertConfirmation(LHSRoutingLocation, caseLoad.SSCCBarcode);
            }

            if (theTransfer.PreferredLoadRoutes.ContainsKey(caseLoad))
            {
                theTransfer.PreferredLoadRoutes.Remove(caseLoad);
            }
        }

        void transfer_OnArrivedAtTransferController(object sender, TransferArrivalArgs e)
        {
            Case_Load caseLoad = e._load as Case_Load;
            
            //Load has arrived at one of entry points to the transfer

            //If the timer is running then do not release the load just start the appropriate timer depending on its routing
            //If its not running then check the available routes and route the load if the route is clear (No need to start the timer in this case).
            //Experior.Core.Environment.Scene.Pause();

            //Calculate the preferred route for the load and save
            if (!theTransfer.PreferredLoadRoutes.ContainsKey(caseLoad))
            {
                if (casePLC.DivertSet(caseLoad.SSCCBarcode, LeftRoutes))
                {
                    theTransfer.PreferredLoadRoutes.Add(caseLoad, Side.Left);
                }
                else if (casePLC.DivertSet(caseLoad.SSCCBarcode, RightRoutes))
                {
                    theTransfer.PreferredLoadRoutes.Add(caseLoad, Side.Right);
                }
            }

            bool LoadRouted = false;
            if (!theTransfer.ReleaseDelayTimerRunning) //Timer not running can i release a load
            {
                if (casePLC.DivertSet(caseLoad.SSCCBarcode, LeftRoutes) && theTransfer.RouteAvailable(Side.Left))
                {
                    ReleaseLoad(e._fromSide, Side.Left, e._load);
                    LoadRouted = true;
                }
                else if (casePLC.DivertSet(caseLoad.SSCCBarcode, RightRoutes) && theTransfer.RouteAvailable(Side.Right))
                {
                    ReleaseLoad(e._fromSide, Side.Right, e._load);
                    LoadRouted = true;
                }
                else if (!casePLC.DivertSet(caseLoad.SSCCBarcode, LeftRoutes) && !casePLC.DivertSet(caseLoad.SSCCBarcode, RightRoutes) && theTransfer.RouteAvailable(e._defaultDirection))
                {
                    ReleaseLoad(e._fromSide, e._defaultDirection, caseLoad);
                    LoadRouted = true;
                }
            }

            if (!LoadRouted)
            {
                float timeout = NormalTimeout;
                if (casePLC.DivertSet(caseLoad.SSCCBarcode, PriorityRoutes))
                {
                    timeout = PriorityTimeout;
                }

                //Set the timeout elapsed true if there is no timeout required, then when the route becomes available then the load can release
                if (timeout == 0)
                {
                    //if (e._fromSide == Side.Left)
                    //    leftTimeoutElapsed = true;
                    //else if (e._fromSide == Side.Right)
                    //    rightTimeOutElapsed = true;
                }
                
                //Can divert to default direction if available not waiting for release dealy and no timeout is required
                if (!theTransfer.ReleaseDelayTimerRunning && timeout == 0 && theTransfer.RouteAvailable(e._defaultDirection))
                {
                    ReleaseLoad(e._fromSide, e._defaultDirection, e._load);
                    LoadRouted = true;
                }

                if (timeout != 0)
                {
                    if (e._fromSide == Side.Left) //lhs conveyor
                    {
                        if (!LeftBlockedTimer.Running)
                        {
                            LeftBlockedTimer.Timeout = timeout;
                            LeftBlockedTimer.Reset();
                            LeftBlockedTimer.Start();
                        }
                        else
                        {
                            Log.Write(string.Format("Error setting timeout on transfer left hand side, conveyor {0}, load {1}", ((Transfer)sender).Name, caseLoad.SSCCBarcode));
                            Experior.Core.Environment.Scene.Pause();
                        }
                    }
                    else //rhs conveyor
                    {
                        if (!RightBlockedTimer.Running)
                        {
                            RightBlockedTimer.Timeout = timeout;
                            RightBlockedTimer.Reset();
                            RightBlockedTimer.Start();
                        }
                        else
                        {
                            Log.Write(string.Format("Error setting timeout on transfer right hand side, conveyor {0}, load {1}", ((Transfer)sender).Name, caseLoad.SSCCBarcode));
                            Experior.Core.Environment.Scene.Pause();
                        }
                    }
                }
            }
        }

        void theTransfer_OnTransferStatusChangedController(object sender, EventArgs e)
        {
            //ReleaseDelayTimer on transfer has timed out, or one of the next conveyors routes has become available,
            
            //check if there are any loads that can be released

            if (theTransfer.mergeQueue.Count > 0) //There is something waiting to transfer in
            {
                List<Side> priorityQueue = new List<Side>();
                if (theTransfer.mergeQueue.Count == 1 || LoadReleasePriority == ReleasePriority.FIFO)
                {
                    priorityQueue = theTransfer.mergeQueue.ToList();
                }
                else if (LoadReleasePriority == ReleasePriority.Left)
                {
                    priorityQueue.Add(Side.Left);
                    priorityQueue.Add(Side.Right);
                }
                else if (LoadReleasePriority == ReleasePriority.Right)
                {
                    priorityQueue.Add(Side.Right);
                    priorityQueue.Add(Side.Left);
                }

                foreach (Side side in priorityQueue)
                {
                    Case_Load caseLoad = theTransfer.SideLoadWaitingStatus(side).WaitingLoad as Case_Load;
                    if (caseLoad != null)
                    {
                        bool routeLoadLeft = casePLC.DivertSet(caseLoad.SSCCBarcode, LeftRoutes);
                        bool routeLoadRight = casePLC.DivertSet(caseLoad.SSCCBarcode, RightRoutes);

                        if (routeLoadLeft && theTransfer.RouteAvailable(Side.Left))
                        {
                            //Load can travel left
                            ReleaseLoad(side, Side.Left, caseLoad);
                            return;
                        }
                        else if (routeLoadRight && theTransfer.RouteAvailable(Side.Right))
                        {
                            //Load can travel right
                            ReleaseLoad(side, Side.Right, caseLoad);
                            return;
                        }
                        else if ((!routeLoadLeft && !routeLoadRight && ReleaseLoadDefault(side, caseLoad, false)) || ReleaseLoadDefault(side, caseLoad, true))
                        {
                            return;
                        }
                    }
                    else 
                    {
                        Log.Write(string.Format("Error: {0} load null un expected! theTransfer_OnTransferStatusChangedController", side.ToString()));
                        return;
                    }
                }
            }
        }

        //bool rightTimeOutElapsed = false;
        void RightBlockedTimer_OnElapsed(Timer sender)
        {
            //rightTimeOutElapsed = true; //Reset this if the load is released, if not then leave it set and wait some more until the default route is available
            //The load waiting at the right hand side has not been released and the timer has timed out

            //The load can now be released to the default route (if different from the selected route)

            //What happens when both loads want to go to the same route which on has priority? (might need another priority property to allow for this)

            //Load has not been released in time, route the load to the default route
            //If the default route is also not available then wait until either route is
            //if (!theTransfer.ReleaseDelayTimerRunning && ReleaseLoadDefault(Side.Right, theTransfer.SideLoadWaitingStatus(Side.Right).WaitingLoad, true));
            //{
                //rightTimeOutElapsed = false;
            //}
        }

        //bool leftTimeoutElapsed = false;
        void LeftBlockedTimer_OnElapsed(Timer sender)
        {
            //leftTimeoutElapsed = true;
            //Load has not been released in time, route the load to the default route
            //If the default route is also not available then wait until either is

            //Note: if the timer is not running and there is a load waiting, then the load should be released to any route (but prefer the actual route)
            //if (!theTransfer.ReleaseDelayTimerRunning && ReleaseLoadDefault(Side.Left, theTransfer.SideLoadWaitingStatus(Side.Left).WaitingLoad, true));
            //{
                //leftTimeoutElapsed = false;
            //}
        }

        /// <summary>
        /// Check if the load can be released to default, and release it.
        /// </summary>
        /// <param name="side">Which side is waiting to be released</param>
        /// <param name="caseLoad">The load to be released</param>
        /// <param name="waitTimeout">Should the load wait for the timeout or not i.e. should it go to default anyway</param>
        /// <returns></returns>
        private bool ReleaseLoadDefault(Side side, Load caseLoad, bool waitTimeout)
        {
            if (side == Side.Left)
            {
                if (theTransfer.RouteAvailable(theTransfer.LHSDefaultDirection) && (!LeftBlockedTimer.Running || !waitTimeout))
                {
                    ReleaseLoad(side, theTransfer.LHSDefaultDirection, caseLoad);
                    return true;
                }
            }
            else if (side == Side.Right)
            {
                if (theTransfer.RouteAvailable(theTransfer.RHSDefaultDirection) && (!RightBlockedTimer.Running || !waitTimeout))
                {
                    ReleaseLoad(side, theTransfer.RHSDefaultDirection, caseLoad);
                    return true;
                }
            }
            return false;
        }

        private void ReleaseLoad(Side fromSide, Side toSide, Load caseLoad)
        {
            if (fromSide == Side.Left)
            {
                LeftBlockedTimer.Stop();
                LeftBlockedTimer.Reset();
                //leftTimeoutElapsed = false;
            }
            else if (fromSide == Side.Right)
            {
                RightBlockedTimer.Stop();
                RightBlockedTimer.Reset();
                //rightTimeOutElapsed = false;
            }

            theTransfer.RouteLoad(fromSide, toSide, caseLoad);
        }


        [Category("RHS Divert")]
        [DisplayName("RHS Routing Code")]
        [Description("Routing code for rhs routing: format destination1,destination2,...,destionation n")]
        [PropertyAttributesProvider("DynamicPropertyRightModeDivert")]
        [PropertyOrder(7)]
        public string RHSRoutingCode
        {
            get { return transferDatcomInfo.rhsRoutingCode; }
            set
            {
                if (value == "")
                {
                    transferDatcomInfo.rhsRoutingCode = null;
                    return;
                }

                List<string> routes = casePLC.ValidateRoutingCode(value); //convert to a list of codes
                if (routes != null)
                {
                    RightRoutes = routes; 
                    transferDatcomInfo.rhsRoutingCode = value;
                }
            }
        }

        [DisplayName("LHS Routing Code")]
        [Description("Routing code for lhs routing: format destination1,destination2,...,destionation n")]
        [PropertyAttributesProvider("DynamicPropertyLeftModeDivert")]
        [PropertyOrder(7)]
        public string LHSRoutingCode
        {
            get { return transferDatcomInfo.lhsRoutingCode; }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    transferDatcomInfo.lhsRoutingCode = null;
                    return;
                }

                List<string> routes = casePLC.ValidateRoutingCode(value);
                if (routes != null)
                {
                    LeftRoutes = routes;
                    transferDatcomInfo.lhsRoutingCode = value;
                }
            }
        }

        [DisplayName("LHS Route Location")]
        [Description("Location name in divert confirmation message when successfully diverted, if blank then no divert confirmation message will be sent")]
        [PropertyAttributesProvider("DynamicPropertyLeftModeDivert")]
        [PropertyOrder(10)]
        public string LHSRoutingLocation
        {
            get { return transferDatcomInfo.lhsRoutingLocation; }
            set
            {
                if (value == "")
                {
                    transferDatcomInfo.lhsRoutingLocation = null;
                }
                else
                {
                    transferDatcomInfo.lhsRoutingLocation = value;
                }
            }
        }

        [Category("Right Divert")]
        [DisplayName("RHS Route Location")]
        [Description("Location name in divert confirmation message when successfully diverted, if blank then no divert confirmation message will be sent")]
        [PropertyAttributesProvider("DynamicPropertyRightModeDivert")]
        [PropertyOrder(10)]
        public string RHSRoutingLocation
        {
            get { return transferDatcomInfo.rhsRoutingLocation; }
            set
            {
                if (value == "")
                    transferDatcomInfo.rhsRoutingLocation = null;
                else
                {
                    transferDatcomInfo.rhsRoutingLocation = value;
                }
            }
        }

        [Category("Failed Divert")]
        [DisplayName("Failed to Divert Location")]
        [Description("Location name in divert confirmation message when load has failed to divert to the configured route, if blank then no divert confirmation message will be sent")]
        [PropertyOrder(11)]
        public string FailedRoutingLocation
        {
            get { return transferDatcomInfo.failedRoutingLocation; }
            set
            {
                if (value == "")
                    transferDatcomInfo.failedRoutingLocation = null;
                else
                {
                    transferDatcomInfo.failedRoutingLocation = value;
                }
            }
        }

        [DisplayName("Release Priority")]
        [Description("When releasing loads after a route becomes available, which load is the priority: FIFO = Whichever load arrived first, LEFT = Left side first, RIGHT = Right side first")]
        [PropertyOrder(12)]
        public ReleasePriority LoadReleasePriority
        {
            get { return transferDatcomInfo.loadReleasePriority; }
            set { transferDatcomInfo.loadReleasePriority = value; }
        }

        [DisplayName("Priority Routing Code")]
        [Description("Routing code for priority routing: format destination1,destination2")]
        [PropertyOrder(13)]
        public string PriorityRoutingCode
        {
            get { return transferDatcomInfo.priorityRoutingCode; }
            set
            {
                if (value == "")
                {
                    transferDatcomInfo.priorityRoutingCode = null;
                    return;
                }

                List<string> routes = casePLC.ValidateRoutingCode(value);
                if (routes != null)
                {
                    PriorityRoutes = routes;
                    transferDatcomInfo.priorityRoutingCode = value;
                }
            }
        }

        [DisplayName("Priority Blocked Timeout")]
        [Description("Time that a load waits at transfer before being released to default route if the load has the priority bit set")]
        [PropertyOrder(14)]
        public float PriorityTimeout
        {
            get { return transferDatcomInfo.priorityTimeout; }
            set
            {
                transferDatcomInfo.priorityTimeout = value;
            }
        }

        [DisplayName("Normal Blocked Timeout")]
        [Description("Time that a load waits at transfer before being released to default route if the load does not have the priority bit set")]
        [PropertyOrder(14)]
        public float NormalTimeout
        {
            get { return transferDatcomInfo.normalTimeout; }
            set
            {
                transferDatcomInfo.normalTimeout = value;
            }
        }

        [Category("Configuration")]
        [DisplayName("Remove From Routing Table")]
        [Description("When load has been routed should the entry in the PLC routing table be removed")]
        [PropertyOrder(5)]
        public bool RemoveFromRoutingTable
        {
            get { return transferDatcomInfo.removeFromRoutingTable; }
            set { transferDatcomInfo.removeFromRoutingTable = value; }
        }

        public void DynamicPropertyRightModeDivert(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = theTransfer.SideNextRouteStatus(Side.Right) !=null;
        }
        public void DynamicPropertyLeftModeDivert(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = theTransfer.SideNextRouteStatus(Side.Left) != null;
        }

        void removeFromRoutingTable(Case_Load caseLoad)
        {
            if (RemoveFromRoutingTable && casePLC.RoutingTable.ContainsKey(caseLoad.SSCCBarcode))
                casePLC.RoutingTable.Remove(caseLoad.SSCCBarcode);
        }
    }

    [Serializable]
    [XmlInclude(typeof(TransferDatcomAusInfo))]
    public class TransferDatcomAusInfo : ProtocolInfo
    {
        public string rhsRoutingCode;
        public string lhsRoutingCode;
        public string lhsRoutingLocation;
        public string rhsRoutingLocation;
        public string failedRoutingLocation;
        public string priorityRoutingCode;
        public float priorityTimeout;
        public float normalTimeout;
        public bool removeFromRoutingTable;
        public ReleasePriority loadReleasePriority = ReleasePriority.FIFO;
    }
}