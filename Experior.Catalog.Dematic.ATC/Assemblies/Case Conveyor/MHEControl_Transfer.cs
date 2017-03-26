using Experior.Catalog.Dematic.Case.Components;
using Experior.Core.Assemblies;
using Experior.Core.Properties;
using Experior.Dematic.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.Drawing;
using Experior.Core;
using Experior.Core.Loads;

namespace Experior.Catalog.Dematic.ATC.Assemblies.CaseConveyor
{
    class MHEControl_Transfer : MHEControl
    {
        private TransferATCInfo transferDatcomInfo;
        private Transfer theTransfer;
        private MHEController_Case casePLC;

        private List<string> LeftRoutes = null; 
        private List<string> RightRoutes = null; 
        //private List<string> PriorityRoutes = null;

        //Timer LeftBlockedTimer = new Timer(1);
        //Timer RightBlockedTimer = new Timer(1);


        public MHEControl_Transfer(TransferATCInfo info, Transfer transfer)
        {
            Info               = info;  // set this to save properties 
            transferDatcomInfo = info;
            theTransfer        = transfer;
            casePLC            = transfer.Controller as MHEController_Case;
            theTransfer.OnDivertCompleteController += transfer_OnDivertCompleteController;
            theTransfer.OnArrivedAtTransferController += transfer_OnArrivedAtTransferController;
            theTransfer.OnTransferStatusChangedController += theTransfer_OnTransferStatusChangedController;
            LHSRoutingCode     = info.lhsRoutingCode;
            RHSRoutingCode     = info.rhsRoutingCode;

            //LeftBlockedTimer.OnElapsed += LeftBlockedTimer_OnElapsed;
            //RightBlockedTimer.OnElapsed += RightBlockedTimer_OnElapsed;
        }

        public override void Dispose()
        {
            theTransfer.OnArrivedAtTransferController -= transfer_OnArrivedAtTransferController;
            //theTransfer.OnDivertCompleteController -= transfer_OnDivertCompleteController;
            theTransfer.OnTransferStatusChangedController -= theTransfer_OnTransferStatusChangedController;

            theTransfer = null;
            transferDatcomInfo = null;
        }   

        //Send the divert confirmation messages
        void transfer_OnDivertCompleteController(object sender, TransferDivertedArgs e)
        {

        }

        void transfer_OnArrivedAtTransferController(object sender, TransferArrivalArgs e)
        {
            IATCCaseLoadType atcLoad = e._load as IATCCaseLoadType;

            //Load has arrived at one of entry points to the transfer

            //Calculate the preferred route for the load and save
            if (!theTransfer.PreferredLoadRoutes.ContainsKey((Case_Load)e._load))
            {
                if (LeftRoutes.Contains(atcLoad.Destination))
                {
                    theTransfer.PreferredLoadRoutes.Add((Case_Load)e._load, Side.Left);
                }
                else if (RightRoutes.Contains(atcLoad.Destination))
                {
                    theTransfer.PreferredLoadRoutes.Add((Case_Load)e._load, Side.Right);
                }
            }

            //bool LoadRouted = false;
            if (!theTransfer.ReleaseDelayTimerRunning) //Timer not running can i release a load
            {
                if (LeftRoutes.Contains(atcLoad.Destination) && theTransfer.RouteAvailable(Side.Left))
                {
                    ReleaseLoad(e._fromSide, Side.Left, e._load);
                    //LoadRouted = true;
                }
                else if (RightRoutes.Contains(atcLoad.Destination) && theTransfer.RouteAvailable(Side.Right))
                {
                    ReleaseLoad(e._fromSide, Side.Right, e._load);
                    //LoadRouted = true;
                }
                else if (!LeftRoutes.Contains(atcLoad.Destination) && !RightRoutes.Contains(atcLoad.Destination) && theTransfer.RouteAvailable(e._defaultDirection))
                {
                    ReleaseLoad(e._fromSide, e._defaultDirection, (Case_Load)e._load);
                    //LoadRouted = true;
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
                    IATCCaseLoadType atcLoad = theTransfer.SideLoadWaitingStatus(side).WaitingLoad as IATCCaseLoadType;
                    if (atcLoad != null)
                    {
                        bool routeLoadLeft = LeftRoutes.Contains(atcLoad.Destination) ? true : false;
                        bool routeLoadRight = RightRoutes.Contains(atcLoad.Destination) ? true : false;

                        if (routeLoadLeft && theTransfer.RouteAvailable(Side.Left))
                        {
                            //Load can travel left
                            ReleaseLoad(side, Side.Left, (Load)atcLoad);
                            return;
                        }
                        else if (routeLoadRight && theTransfer.RouteAvailable(Side.Right))
                        {
                            //Load can travel right
                            ReleaseLoad(side, Side.Right, (Load)atcLoad);
                            return;
                        }
                        else 
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
        //void RightBlockedTimer_OnElapsed(Timer sender)
        //{
        //    rightTimeOutElapsed = true; //Reset this if the load is released, if not then leave it set and wait some more until the default route is available
        //    //The load waiting at the right hand side has not been released and the timer has timed out

        //    //The load can now be released to the default route (if different from the selected route)

        //    //What happens when both loads want to go to the same route which on has priority? (might need another priority property to allow for this)

        //    //Load has not been released in time, route the load to the default route
        //    //If the default route is also not available then wait until either route is
        //    if (!theTransfer.ReleaseDelayTimerRunning && ReleaseLoadDefault(Side.Right, theTransfer.SideLoadWaitingStatus(Side.Right).WaitingLoad, true));
        //    {
        //        rightTimeOutElapsed = false;
        //    }
        //}

        //bool leftTimeoutElapsed = false;
        //void LeftBlockedTimer_OnElapsed(Timer sender)
        //{
        //    leftTimeoutElapsed = true;
        //    //Load has not been released in time, route the load to the default route
        //    //If the default route is also not available then wait until either is

        //    //Note: if the timer is not running and there is a load waiting, then the load should be released to any route (but prefer the actual route)
        //    if (!theTransfer.ReleaseDelayTimerRunning && ReleaseLoadDefault(Side.Left, theTransfer.SideLoadWaitingStatus(Side.Left).WaitingLoad, true));
        //    {
        //        leftTimeoutElapsed = false;
        //    }
        //}

        /// <summary>
        /// Check if the load can be released to default, and release it.
        /// </summary>
        /// <param name="side">Which side is waiting to be released</param>
        /// <param name="caseLoad">The load to be released</param>
        /// <param name="waitTimeout">Should the load wait for the timeout or not i.e. should it go to default anyway</param>
        /// <returns></returns>
        //private bool ReleaseLoadDefault(Side side, Load caseLoad, bool waitTimeout)
        //{
        //    if (side == Side.Left)
        //    {
        //        if (theTransfer.RouteAvailable(theTransfer.LHSDefaultDirection) && (!LeftBlockedTimer.Running || !waitTimeout))
        //        {
        //            ReleaseLoad(side, theTransfer.LHSDefaultDirection, caseLoad);
        //            return true;
        //        }
        //    }
        //    else if (side == Side.Right)
        //    {
        //        if (theTransfer.RouteAvailable(theTransfer.RHSDefaultDirection) && (!RightBlockedTimer.Running || !waitTimeout))
        //        {
        //            ReleaseLoad(side, theTransfer.RHSDefaultDirection, caseLoad);
        //            return true;
        //        }
        //    }
        //    return false;
        //}

        private void ReleaseLoad(Side fromSide, Side toSide, Load caseLoad)
        {
            //if (fromSide == Side.Left)
            //{
            //    LeftBlockedTimer.Stop();
            //    LeftBlockedTimer.Reset();
            //    leftTimeoutElapsed = false;
            //}
            //else if (fromSide == Side.Right)
            //{
            //    RightBlockedTimer.Stop();
            //    RightBlockedTimer.Reset();
            //    rightTimeOutElapsed = false;
            //}

            theTransfer.RouteLoad(fromSide, toSide, caseLoad);
        }


        [DisplayName("Right Routing Code")]
        [Description("Routing destinations of the load for routing Left: format xxxx,yyyy if the load destination is set to either xxxx or yyyy it will be routed Right")]
        [PropertyAttributesProvider("DynamicPropertyRightModeDivert")]
        [PropertyOrder(7)]
        public string RHSRoutingCode
        {
            get { return transferDatcomInfo.rhsRoutingCode; }
            set
            {
                if (value == null || value == "")
                {
                    transferDatcomInfo.rhsRoutingCode = null;
                    LeftRoutes = null;
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
                    transferDatcomInfo.rhsRoutingCode = value;
                }
            }
        }

        [DisplayName("Left Routing Code")]
        [Description("Routing destinations of the load for routing Left: format xxxx,yyyy if the load destination is set to either xxxx or yyyy it will be routed Left")]
        [PropertyAttributesProvider("DynamicPropertyLeftModeDivert")]
        [PropertyOrder(7)]
        public string LHSRoutingCode
        {
            get { return transferDatcomInfo.lhsRoutingCode; }
            set
            {
                if (value == null || value == "")
                {
                    transferDatcomInfo.lhsRoutingCode = null;
                    LeftRoutes = null;
                    return;
                }

                string[] splitRoutes = value.Split(',');
                LeftRoutes = new List<string>();
                foreach (string route in splitRoutes)
                {
                    LeftRoutes.Add(route);
                }

                if (LeftRoutes != null)
                {
                    transferDatcomInfo.lhsRoutingCode = value;
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









        public void DynamicPropertyRightModeDivert(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = theTransfer.SideNextRouteStatus(Side.Right) !=null;
        }
        public void DynamicPropertyLeftModeDivert(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = theTransfer.SideNextRouteStatus(Side.Left) != null;
        }
  
    }

    [Serializable]
    [XmlInclude(typeof(TransferATCInfo))]
    public class TransferATCInfo : ProtocolInfo
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
