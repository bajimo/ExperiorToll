using Experior.Catalog.Logistic.Track;
using Experior.Core;
using Experior.Core.Assemblies;
using Experior.Core.Loads;
using Experior.Core.Parts;
using Experior.Core.Properties;
using Experior.Core.Routes;
using Experior.Dematic.Base;
using Experior.Dematic.Base.Devices;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Xml.Serialization;

namespace Experior.Catalog.Dematic.Pallet.Assemblies
{
    public class LiftTable : Catalog.Logistic.Track.Straight, IPalletRouteStatus, IControllable
    {
        LiftTableInfo liftTableInfo;
        ActionPoint leftEnd, rightEnd, straightEnd, straightAP, leftAP, rightAP;
        protected FixPoint LeftFixPoint, RightFixPoint;
        Core.TransportSections.StraightTransportSection leftTransport, rightTransport;
        Modes leftMode = Modes.None;
        Modes rightMode = Modes.None;
        Modes straightMode = Modes.None;
        Timer releaseDelayTimer = new Timer(0);
        protected bool LoadHandledByController = false;
        private double lastLoadArrived = -1;

        RouteStatus ThisAvailableStatusMergeStraight;
        RouteStatus ThisAvailableStatusMergeLeft;
        RouteStatus ThisAvailableStatusMergeRight;

        IRouteStatus NextConveyorStraight;
        IRouteStatus NextConveyorLeft;
        IRouteStatus NextConveyorRight;

        public RouteStatus NextAvailableStatusStraight;
        public RouteStatus NextAvailableStatusLeft;
        public RouteStatus NextAvailableStatusRight;

        RouteStatus PreviousAvailableStatusStraight;

        IRouteStatus PreviousConveyorStraight;
        IRouteStatus PreviousConveyorLeft;
        IRouteStatus PreviousConveyorRight;

        LoadWaitingStatus PreviousLoadWaitingStatusStraight;
        LoadWaitingStatus PreviousLoadWaitingStatusLeft;
        LoadWaitingStatus PreviousLoadWaitingStatusRight;

        LoadWaitingStatus ThisLoadWaitingStatusStraight = new LoadWaitingStatus();
        LoadWaitingStatus ThisLoadWaitingStatusLeft = new LoadWaitingStatus();
        LoadWaitingStatus ThisLoadWaitingStatusRight = new LoadWaitingStatus();

        List<Direction> mergeQueue = new List<Direction>();

        Direction LoadRoute = Direction.None; //This is used when exiting the conveyor, only the relevant exit route will trigger the release of the next loads (make route available)

        //This is the event that can be subscribed to in the routing script
        public delegate void OnDivertPointEvent(LiftTable sender, Load load);
        public static event OnDivertPointEvent OnLoadOnDivertPoint;

        public void LoadOnDivertPoint(LiftTable sender, Load load)
        {
            OnLoadOnDivertPoint?.Invoke(sender, load);
        }

        //These are the delegates that should be used in the MHEControl
        public delegate bool DivertLoadStatus(Load load);
        /// <summary>
        /// The MHE control object will assign a divertArrival method in its contructor
        /// </summary>
        public DivertLoadStatus divertArrival;
        public DivertLoadStatus releasedStraight;
        public DivertLoadStatus releasedLeft;
        public DivertLoadStatus releasedRight;
        public DivertLoadStatus loadDeleted;

        private DirectionAvailability preferedRouting;
        private List<DirectionAvailability> directionAvilabilities = null;

        private DirectionAvailability leftDirectionAvailable = new DirectionAvailability { direction = Direction.Left };
        private DirectionAvailability rightDirectionAvailable = new DirectionAvailability { direction = Direction.Right };
        private DirectionAvailability straightDirectionAvailable = new DirectionAvailability { direction = Direction.Straight };

        public event EventHandler<LoadArrivedEventArgs> OnLoadArrived;

        public virtual void LoadArrived(LoadArrivedEventArgs e)
        {
            OnLoadArrived?.Invoke(this, e);
        }

        #region Constructor

        public LiftTable(LiftTableInfo info): base(info)
        {
            liftTableInfo = info;

            TransportSection.Route.DragDropLoad = false;
            Core.Environment.Scene.OnLoaded += Scene_OnLoaded;
            if (TransportSection.Route.Arrow != null)
            {
                TransportSection.Route.Arrow.Visible = false;
            }
            straightAP = TransportSection.Route.InsertActionPoint(info.length / 2);
            straightAP.OnEnter += new ActionPoint.EnterEvent(straightAP_Enter);

            straightEnd = TransportSection.Route.InsertActionPoint(info.length);
            straightEnd.Edge = ActionPoint.Edges.Trailing;
            straightEnd.OnEnter += new ActionPoint.EnterEvent(straightEnd_Enter);

            leftTransport = new Core.TransportSections.StraightTransportSection(liftTableInfo.color, GetTransportSectionLength(true), 0.1f);
            leftTransport.Route.Motor.Speed = liftTableInfo.speed;
            Add(leftTransport);
            leftTransport.LocalPosition = new Microsoft.DirectX.Vector3(0, liftTableInfo.thickness / 2, -liftTableInfo.width / 4);
            leftTransport.LocalYaw = (float)Math.PI / 2;
            leftTransport.Visible = false;
            leftTransport.Route.Arrow.Visible = false;

            leftAP = leftTransport.Route.InsertActionPoint(0);//(info.length / 2);
            leftAP.OnEnter += new ActionPoint.EnterEvent(leftAP_Enter);

            leftEnd = leftTransport.Route.InsertActionPoint(leftTransport.Route.Length - 0.01f);
            leftEnd.Edge = ActionPoint.Edges.Trailing;
            leftEnd.OnEnter += new ActionPoint.EnterEvent(leftEnd_Enter);

            rightTransport = new Core.TransportSections.StraightTransportSection(liftTableInfo.color, GetTransportSectionLength(false), 0.1f);
            rightTransport.Route.Motor.Speed = liftTableInfo.speed;
            Add(rightTransport);
            rightTransport.LocalPosition = new Microsoft.DirectX.Vector3(0, liftTableInfo.thickness / 2, liftTableInfo.width / 4);
            rightTransport.LocalYaw = -(float)Math.PI / 2;
            rightTransport.Visible = false;
            rightTransport.Route.Arrow.Visible = false;

            rightAP = rightTransport.Route.InsertActionPoint(0);
            rightAP.OnEnter += new ActionPoint.EnterEvent(rightAP_Enter);

            rightEnd = rightTransport.Route.InsertActionPoint(rightTransport.Route.Length - 0.01f);
            rightEnd.Edge = ActionPoint.Edges.Trailing;
            rightEnd.OnEnter += new ActionPoint.EnterEvent(rightEnd_Enter);

            LeftFixPoint = new Core.Parts.FixPoint(Core.Parts.FixPoint.Types.Left, this);
            Add(LeftFixPoint);
            LeftFixPoint.LocalPosition = new Microsoft.DirectX.Vector3(0, 0, -liftTableInfo.width / 2);
            LeftFixPoint.LocalYaw = (float)Math.PI / 2;
            LeftFixPoint.Route = leftTransport.Route;
            LeftFixPoint.OnBeforeSnapping += LeftFixPoint_OnBeforeSnapping;
            LeftFixPoint.OnSnapped += LeftFixPoint_OnSnapped;
            LeftFixPoint.OnUnSnapped += LeftFixPoint_OnUnSnapped;

            RightFixPoint = new Core.Parts.FixPoint(Core.Parts.FixPoint.Types.Right, this);
            Add(RightFixPoint);
            RightFixPoint.LocalPosition = new Microsoft.DirectX.Vector3(0, 0, liftTableInfo.width / 2);
            RightFixPoint.LocalYaw = -(float)Math.PI / 2;
            RightFixPoint.Route = rightTransport.Route;
            RightFixPoint.OnBeforeSnapping += RightFixPoint_OnBeforeSnapping;
            RightFixPoint.OnSnapped += RightFixPoint_OnSnapped;
            RightFixPoint.OnUnSnapped += RightFixPoint_OnUnSnapped;

            StartFixPoint.Dragable = false;
            StartFixPoint.OnSnapped += StartFixPoint_OnSnapped;
            StartFixPoint.OnUnSnapped += StartFixPoint_OnUnSnapped;

            EndFixPoint.Dragable = false;
            EndFixPoint.OnSnapped += EndFixPoint_OnSnapped;
            EndFixPoint.OnUnSnapped += EndFixPoint_OnUnSnapped;

            this.OnNameChanged += new NameChangedEvent(NameChange);

            TransportSection.Route.OnLoadRemoved += Route_OnLoadRemoved;
            rightTransport.Route.OnLoadRemoved += Route_OnLoadRemoved;
            leftTransport.Route.OnLoadRemoved += Route_OnLoadRemoved;
            releaseDelayTimer.Timeout = ReleaseDelay;
            releaseDelayTimer.OnElapsed += releaseDelayTimer_OnElapsed;

            RouteBlockedBehaviour = liftTableInfo.routeBlockedBehaviour;
            RouteBlockedTimeoutNormal = liftTableInfo.routeBlockedTimeoutNormal;
            RouteBlockedTimeoutPriority = liftTableInfo.routeBlockedTimeoutPriority;

            ControllerProperties = StandardCase.SetMHEControl(liftTableInfo, this);

            Intersectable = false;

            SetupOffsetTransportSections();
        }

        public void Scene_OnLoaded()
        {
            if (ControllerProperties == null)
            {
                ControllerProperties = StandardCase.SetMHEControl(liftTableInfo, this);
            }
        }

        #endregion

        #region IRouteStatus
        public RouteStatus GetRouteStatus(FixPoint startFixPoint)
        {
            //Set no routes available to start with, for merging the merge/divert sets the routes available only when there is a load waiting

            if (startFixPoint == StartFixPoint)
            {
                ThisAvailableStatusMergeStraight = new RouteStatus { Available = RouteStatuses.Request };
                return ThisAvailableStatusMergeStraight;
            }
            else if (startFixPoint == LeftFixPoint)
            {
                ThisAvailableStatusMergeLeft = new RouteStatus { Available = RouteStatuses.Request };
                return ThisAvailableStatusMergeLeft;
            }
            else if (startFixPoint == RightFixPoint)
            {
                ThisAvailableStatusMergeRight = new RouteStatus { Available = RouteStatuses.Request };
                return ThisAvailableStatusMergeRight;
            }
            return null;
        }

        public LoadWaitingStatus GetLoadWaitingStatus(FixPoint endFixPoint)
        {
            if (endFixPoint == LeftFixPoint)
                return ThisLoadWaitingStatusLeft;
            else if (endFixPoint == RightFixPoint)
                return ThisLoadWaitingStatusRight;
            else if (endFixPoint == EndFixPoint)
                return ThisLoadWaitingStatusStraight;

            return null;
        }

        [DisplayName("Load Count")]
        [Category("Status")]
        [Description("Number of loads on this transport section")]
        public int LoadCount
        {
            get
            {
                return leftTransport.Route.Loads.Count + rightTransport.Route.Loads.Count + TransportSection.Route.Loads.Count;
            }
        }
        #endregion

        /// <summary>
        /// Returns the load waiting on the Divert Point
        /// </summary>
        [Browsable(false)]
        public Load ActiveLoad //This is the load that is waiting on the Divert Point
        {
            get
            {
                if (straightAP.Active)
                {
                    return straightAP.ActiveLoad;
                }
                else if (leftAP.Active)
                {
                    return leftAP.ActiveLoad;
                }
                else if (rightAP.Active)
                {
                    return rightAP.ActiveLoad;
                }
                else
                {
                    return null;
                }
            }
        }
        
        /// <summary>
        /// Has the conveyor got at load waiting on the Divert Point
        /// </summary>
        [Browsable(false)]
        public override bool Active
        {
            get
            {
                if (ActiveLoad != null)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// When a load has been routed already but the load should no longer be released when the route becomes available
        /// </summary>
        public void ResetRouteLoad()
        {
            //Switch the load back onto a merge point, then it cannot be released if a next conveyor becomes available
            if (Active)
            {
                if (leftMode == Modes.Merge)
                {
                    ActiveLoad.Switch(leftAP, true);
                }
                else if (rightMode == Modes.Merge)
                {
                    ActiveLoad.Switch(rightAP, true);
                }
            }
        }

        #region Merge divert logic

        void straightAP_Enter(ActionPoint sender, Load load) //On divert point
        { 
            if (ThisAvailableStatusMergeStraight != null)
            {
                ThisAvailableStatusMergeStraight.Available = RouteStatuses.Blocked;
                ThisAvailableStatusMergeStraight.Available = RouteStatuses.Request;
            }

            if (LoadRoute == Direction.None)
                LoadOnAP(load);
        }

        void rightAP_Enter(ActionPoint sender, Load load) //load entering from the right side
        {
            if (ThisAvailableStatusMergeRight != null)
            {
                ThisAvailableStatusMergeRight.Available = RouteStatuses.Blocked;
                ThisAvailableStatusMergeRight.Available = RouteStatuses.Request;
            }

            if (LoadRoute == Direction.None)
                LoadOnAP(load);
        }

        void leftAP_Enter(ActionPoint sender, Load load) //load entering from the left side
        {
            if (ThisAvailableStatusMergeLeft != null)
            {
                ThisAvailableStatusMergeLeft.Available = RouteStatuses.Blocked;
                ThisAvailableStatusMergeLeft.Available = RouteStatuses.Request;
            }

            if (LoadRoute == Direction.None)
                LoadOnAP(load);
        }

        private void LoadOnAP(Load load)
        {
            // Fire the OnLoadArrival event
            RaiseEventOnArrival(load);

            //first check if there is only one fixpoint set to divert then route to that (saves having to configure each time for single routing

            if (ControlType == ControlTypes.Local)
            {
                if (straightMode == Modes.Divert && leftMode != Modes.Divert && rightMode != Modes.Divert)
                {
                    RouteLoadStraight(load);
                    return;
                }
                if (straightMode != Modes.Divert && leftMode == Modes.Divert && rightMode != Modes.Divert)
                {
                    RouteLoadLeft(load);
                    return;
                }
                if (straightMode != Modes.Divert && leftMode != Modes.Divert && rightMode == Modes.Divert)
                {
                    RouteLoadRight(load);
                    return;
                }

                if (DefaultRouting == Direction.Straight && straightMode == Modes.Divert)
                {
                    RouteLoadStraight(load);
                    return;
                }
                else if (DefaultRouting == Direction.Left && leftMode == Modes.Divert)
                {
                    RouteLoadLeft(load);
                    return;
                }
                else if (DefaultRouting == Direction.Right && rightMode == Modes.Divert)
                {
                    RouteLoadRight(load);
                    return;
                }
            }
            ControlDivertPoint(load);
        }

        public void RaiseEventOnArrival(Load load)
        {
            if (lastLoadArrived != Core.Environment.Time.Simulated)
            {
                lastLoadArrived = Core.Environment.Time.Simulated;
                // Fire on load arrived event
                LoadArrived(new LoadArrivedEventArgs(load));
            }
        }

        protected virtual void ControlDivertPoint(Load load)
        {
            if (!LoadHandledByController || load.Stopped) //Never call the controller code more than once
            {
                LoadHandledByController = true;
                if (divertArrival == null || divertArrival(load))
                {
                    OnLoadOnDivertPoint?.Invoke(this, load); //Event can be subscribed to in the routing script if not handled by the controller
                }
            }
        }

        public void RouteLoad(Load load, List<Direction> validRoutes, bool priorityLoad)
        {
            load.UserData = "";
            bool DivertLeft = false, DivertRight = false, DivertStraight = false;

            if (validRoutes.Contains(Direction.Left)) DivertLeft = true;
            if (validRoutes.Contains(Direction.Right)) DivertRight = true;
            if (validRoutes.Contains(Direction.Straight)) DivertStraight = true;

            if (validRoutes.Count > 0)
            {
                if (validRoutes.Count > 1)
                {
                    DirectionAvailability[] directionAvailability = new DirectionAvailability[3];
                    directionAvailability[routingCheckOrder[0] - 1] = straightDirectionAvailable;
                    directionAvailability[routingCheckOrder[0] - 1].status = NextAvailableStatusStraight;

                    directionAvailability[routingCheckOrder[1] - 1] = leftDirectionAvailable;
                    directionAvailability[routingCheckOrder[1] - 1].status = NextAvailableStatusLeft;

                    directionAvailability[routingCheckOrder[2] - 1] = rightDirectionAvailable;
                    directionAvailability[routingCheckOrder[2] - 1].status = NextAvailableStatusRight;

                    directionAvilabilities = new List<DirectionAvailability>();
                    foreach (DirectionAvailability availableRouting in directionAvailability)
                    {
                        directionAvilabilities.Add(availableRouting);
                    }

                    if (validRoutes.Count == 2)
                    {
                        if (!DivertRight || RightMode != Modes.Divert)
                            directionAvilabilities.RemoveAt(routingCheckOrder[2] - 1);
                        if (!DivertLeft || LeftMode != Modes.Divert)
                            directionAvilabilities.RemoveAt(routingCheckOrder[1] - 1);
                        if (!DivertStraight || StraightMode != Modes.Divert)
                            directionAvilabilities.RemoveAt(routingCheckOrder[0] - 1);
                    }

                    preferedRouting = directionAvilabilities[0];
                }
                else
                {
                    if (DivertLeft) preferedRouting = new DirectionAvailability { direction = Direction.Left, status = NextAvailableStatusLeft };
                    else if (DivertRight) preferedRouting = new DirectionAvailability { direction = Direction.Right, status = NextAvailableStatusRight };
                    else if (DivertStraight) preferedRouting = new DirectionAvailability { direction = Direction.Straight, status = NextAvailableStatusStraight };
                }

                if (directionAvilabilities != null)
                {
                    if (directionAvilabilities[0].status.Available == RouteStatuses.Blocked)
                    {
                        if (directionAvilabilities[1].status.Available == RouteStatuses.Blocked)
                        {
                            if (directionAvilabilities.Count == 3 && directionAvilabilities[2].status.Available != RouteStatuses.Blocked)
                                preferedRouting = directionAvilabilities[2];
                        }
                        else
                            preferedRouting = directionAvilabilities[1]; //Set the prefered routing to the other route if it's clear
                    }
                }

                if (preferedRouting.direction != DefaultRouting && preferedRouting.status.Available == RouteStatuses.Blocked) //[BG] Changed from or to and!!
                {
                    if (RouteBlockedBehaviour == RouteBlocked.Route_To_Default) //Check if the selected route is blocked, if it is then route to default
                    {
                            RouteToDefault(load);
                    }
                }
                if (preferedRouting != null)
                {
                    switch (preferedRouting.direction)
                    {
                        case Direction.Left: RouteLoadLeft(load); break;
                        case Direction.Right: RouteLoadRight(load); break;
                        case Direction.Straight: RouteLoadStraight(load); break;
                        default: return;
                    }
                }
                return;
            }

            //If code drops through to here then send to default routing
            RouteToDefault(load);
        }

        void RouteToDefault(Load load)
        {
            switch (DefaultRouting)
            {
                case Direction.Straight: RouteLoadStraight(load); break;
                case Direction.Left: RouteLoadLeft(load); break;
                case Direction.Right: RouteLoadRight(load); break;
                default:
                    load.Stop();
                    Experior.Core.Environment.Log.Write(string.Format("MergeDivert {0}: No valid default routing set, load has stopped", Name), Color.Red);
                    break;
            }
        }
       
        public void RouteLoadStraight(Load load)
        {
            ThisLoadWaitingStatusLeft.SetLoadWaiting(false, LoadRoute == Direction.Left);
            ThisLoadWaitingStatusRight.SetLoadWaiting(false, LoadRoute == Direction.Right);
                        
            LoadRoute = Direction.Straight;

            if (load != null)
            {
                if (straightAP.ActiveLoad != load)
                {
                    load.Switch(straightAP, true);
                }
                if (NextAvailableStatusStraight.Available != RouteStatuses.Available)
                {
                    load.Stop();
                    ThisLoadWaitingStatusStraight.SetLoadWaiting(true, false, load);
                }
                else
                {
                    if (load.Stopped)
                        load.Release();
                    releasedLoadStraight(load);
                }
            }
        }

        public void RouteLoadLeft(Load load)
        {
            ThisLoadWaitingStatusRight.SetLoadWaiting(false, LoadRoute == Direction.Right);
            ThisLoadWaitingStatusStraight.SetLoadWaiting(false, LoadRoute == Direction.Straight);

            if (load != null)
            {
                if (leftAP.ActiveLoad != load)
                {
                    load.Switch(leftAP, true);
                }
                LoadRoute = Direction.Left;

                //Check if there is a valid route left
                if (leftMode != Modes.Divert)
                {
                    Experior.Core.Environment.Log.Write(string.Format("Merge/Divert {0}: Cannot route left as it is not configured as a divert route", Name), System.Drawing.Color.Red);
                    return;
                }

                if (NextAvailableStatusLeft.Available != RouteStatuses.Available)
                {
                    load.Stop();
                    ThisLoadWaitingStatusLeft.SetLoadWaiting(true, false, load);
                }
                else
                {
                    if (load.Stopped)
                        load.Release();
                    releasedLoadLeft(load);
                }
            }
        }

        public void RouteLoadRight(Load load)
        {
            ThisLoadWaitingStatusLeft.SetLoadWaiting(false, LoadRoute == Direction.Left);
            ThisLoadWaitingStatusStraight.SetLoadWaiting(false, LoadRoute == Direction.Straight);

            if (load != null)
            {
                if (rightAP.ActiveLoad != load)
                {
                    load.Switch(rightAP, true);
                }
                LoadRoute = Direction.Right;

                //Check if there is a valid route right
                if (rightMode != Modes.Divert)
                {
                    Experior.Core.Environment.Log.Write(string.Format("Merge/Divert {0}: Cannot route right as it is not configured as a divert route", Name), System.Drawing.Color.Red);
                    return;
                }

                if (NextAvailableStatusRight.Available != RouteStatuses.Available)
                {
                    load.Stop();
                    ThisLoadWaitingStatusRight.SetLoadWaiting(true, false, load);
                }
                else
                {
                    if (load.Stopped)
                        load.Release();
                    releasedLoadRight(load);
                }
            }
        }

        void straightEnd_Enter(ActionPoint sender, Load load) //load exiting from the straight side
        {
            ThisLoadWaitingStatusStraight.SetLoadWaiting(false, false, load);
            //LoadExitConveyor(load);
        }

        void rightEnd_Enter(ActionPoint sender, Load load) //load exiting from the right side
        {
            ThisLoadWaitingStatusRight.SetLoadWaiting(false, false, load);
            //LoadExitConveyor(load);
        }

        void leftEnd_Enter(ActionPoint sender, Load load) //load exiting from the left side
        {
            ThisLoadWaitingStatusLeft.SetLoadWaiting(false, false, load);
            //LoadExitConveyor(load);
        }

        void LoadExitConveyor(Load load)
        {
            Experior.Dematic.Base.EuroPallet palletLoad = load as Experior.Dematic.Base.EuroPallet;
            if (palletLoad != null && palletLoad.FlowControl)
            {
                palletLoad.OnDisposed -= Load_OnDisposed;
                palletLoad.FlowControl = false;
            }
            LoadHandledByController = false;
            if (ReleaseDelay == 0)
                ReleaseLoadIntoConveyor();
            else
            {
                if (!releaseDelayTimer.Running && timerHasRun)
                    ReleaseLoadIntoConveyor();
                else
                {
                    releaseDelayTimer.Start();
                }
            }
        }

        bool timerHasRun = false;
        void releaseDelayTimer_OnElapsed(Timer sender)
        {
            timerHasRun = true;
            releaseDelayTimer.Stop();
            releaseDelayTimer.Reset();
            ReleaseLoadIntoConveyor();
        }

        void ReleaseLoadIntoConveyor()
        {
            if (ActiveLoad == null)
            {
                LiftTableActiveLoad = null;
                //Now decide which load to allow into divert

                if (mergeQueue.Count > 0)
                {
                    switch (mergeQueue[0])
                    {
                        case Direction.Straight: ThisAvailableStatusMergeStraight.Available = RouteStatuses.Available; break;
                        //case Direction.Left: ThisAvailableStatusMergeLeft.Available = RouteStatuses.Available; break;
                        case Direction.Left: LeftRouteStatus = RouteStatuses.Available; break;
                        //case Direction.Right: ThisAvailableStatusMergeRight.Available = RouteStatuses.Available; break;
                        case Direction.Right: RightRouteStatus = RouteStatuses.Available; break;

                    }
                    mergeQueue.RemoveAt(0);
                }

                timerHasRun = false;
                LoadRoute = Direction.None;
            }
        }

        public void mergeQueue_Add(Direction direction)
        {
            if (!mergeQueue.Contains(direction))
                mergeQueue.Add(direction);
        }

        public void mergeQueue_Remove(Direction direction)
        {
            if (mergeQueue.Contains(direction))
                mergeQueue.Remove(direction);
        }

        void NextAvailableStatusStraight_OnAvailableChanged(object sender, RouteStatusChangedEventArgs e)
        {
            //When the route becomes available if there is a load in the ap then release it
            if (straightAP.Active && e._available == RouteStatuses.Available)
            {
                straightAP.ActiveLoad.Release();
                releasedLoadStraight(straightAP.ActiveLoad);
            }

            //If the load has been stopped because both the prefered routing and the default routing are blocked
            //then the preferred routing becomes available, then send the load to the preferred routing 
            if (e._available != RouteStatuses.Blocked)
            {
                if (((directionAvilabilities != null && directionAvilabilities.Contains(straightDirectionAvailable)) 
                    || RouteBlockedBehaviour == RouteBlocked.Route_To_Default && DefaultRouting == Direction.Straight))
                {
                    RouteLoadStraight(LiftTableActiveLoad);
                }
            }

            //When the next conveyor becomes occupied (blocked) then another load can be released in
            if (e._available == RouteStatuses.Blocked)
            {
                LoadExitConveyor(LiftTableActiveLoad);
            }
        }

        void NextAvailableStatusLeft_OnAvailableChanged(object sender, RouteStatusChangedEventArgs e)
        {
            //This will only get triggered if left is a divert
            if (leftAP.Active && e._available == RouteStatuses.Available)
            {
                leftAP.ActiveLoad.Release();
                releasedLoadLeft(leftAP.ActiveLoad);
            }

            //If the load has been stopped because both the prefered routing and the default routing are blocked
            //then the preferred routing becomes available, then send the load to the preferred routing 
            if (e._available != RouteStatuses.Blocked)
            {
                if (((directionAvilabilities != null && directionAvilabilities.Contains(leftDirectionAvailable)) 
                    || RouteBlockedBehaviour == RouteBlocked.Route_To_Default && DefaultRouting == Direction.Left))
                {
                    RouteLoadLeft(LiftTableActiveLoad);
                }
            }

            //When the next conveyor becomes occupied (blocked) then another load can be released in
            if (e._available == RouteStatuses.Blocked)
            {
                LoadExitConveyor(LiftTableActiveLoad);
            }
        }

        void NextAvailableStatusRight_OnAvailableChanged(object sender, RouteStatusChangedEventArgs e)
        {
            //This will only get triggered if right is a divert
            if (rightAP.Active && e._available == RouteStatuses.Available)
            {
                rightAP.ActiveLoad.Release();
                releasedLoadRight(rightAP.ActiveLoad);
            }

            //If the load has been stopped because both the prefered routing and the default routing are blocked
            //then the preferred routing becomes available, then send the load to the preferred routing 
            if (e._available != RouteStatuses.Blocked)
            {
                if (((directionAvilabilities != null && directionAvilabilities.Contains(rightDirectionAvailable)) 
                    || RouteBlockedBehaviour == RouteBlocked.Route_To_Default && DefaultRouting == Direction.Right))
                {
                    RouteLoadRight(LiftTableActiveLoad);
                }
            }

            //When the next conveyor becomes occupied (blocked) then another load can be released in
            if (e._available == RouteStatuses.Blocked)
            {
                LoadExitConveyor(LiftTableActiveLoad);
            }
        }

        void releasedLoadLeft(Load load)
        {
            releasedLeft?.Invoke(load);
            loadReleasedComplete();
        }

        void releasedLoadRight(Load load)
        {
            releasedRight?.Invoke(load);
            loadReleasedComplete();
        }

        void releasedLoadStraight(Load load)
        {
            releasedStraight?.Invoke(load);
            loadReleasedComplete();
        }

        void loadReleasedComplete()
        {
            //If the route was blocked because the onward route was still blocked, then set the status back to request when the load is eventually released

            if (ThisAvailableStatusMergeLeft != null && ThisAvailableStatusMergeLeft.Available == RouteStatuses.Blocked)
            {
                LeftRouteStatus = RouteStatuses.Request;
            }
            if (ThisAvailableStatusMergeRight != null && ThisAvailableStatusMergeRight.Available == RouteStatuses.Blocked)
            {
                RightRouteStatus = RouteStatuses.Request;
            } 
            if (ThisAvailableStatusMergeStraight != null && ThisAvailableStatusMergeStraight.Available == RouteStatuses.Blocked)
            {
                ThisAvailableStatusMergeStraight.Available = RouteStatuses.Request;
            }
            preferedRouting = null;
            directionAvilabilities = null;
        }

        #endregion

        #region Snapping
        void RightFixPoint_OnUnSnapped(FixPoint stranger)
        {
            rightMode = Modes.None;
            NextConveyorRight = null;
            if (NextAvailableStatusRight != null) NextAvailableStatusRight.OnRouteStatusChanged -= NextAvailableStatusRight_OnAvailableChanged;
            NextAvailableStatusRight = null;
            PreviousConveyorRight = null;
            if (PreviousLoadWaitingStatusRight != null) PreviousLoadWaitingStatusRight.OnLoadWaitingChanged -= PreviousLoadWaitingStatusRight_OnLoadWaitingChanged;
            PreviousLoadWaitingStatusRight = null;
            ThisAvailableStatusMergeRight = null;

            Reset();
        }

        void RightFixPoint_OnSnapped(FixPoint stranger, FixPoint.SnapEventArgs e)
        {
            if (stranger.Type == Core.Parts.FixPoint.Types.Start) //If it's a divert
            {
                rightMode = Modes.Divert;
                rightTransport.LocalYaw = (float)Math.PI / 2;
                rightAP.Distance = 0;

                NextConveyorRight = stranger.Parent as IRouteStatus;
                NextAvailableStatusRight = NextConveyorRight.GetRouteStatus(stranger);
                NextAvailableStatusRight.OnRouteStatusChanged += NextAvailableStatusRight_OnAvailableChanged;
            }
            else if (stranger.Type == FixPoint.Types.End) //If it's a merge
            {
                rightMode = Modes.Merge;
                rightTransport.LocalYaw = -(float)Math.PI / 2;
                rightAP.Distance = rightTransport.Route.Length - 0.01f;

                PreviousConveyorRight = stranger.Parent as IRouteStatus;
                PreviousLoadWaitingStatusRight = PreviousConveyorRight.GetLoadWaitingStatus(stranger);
                PreviousLoadWaitingStatusRight.OnLoadWaitingChanged += PreviousLoadWaitingStatusRight_OnLoadWaitingChanged;
            }
        }

        void RightFixPoint_OnBeforeSnapping(FixPoint sender, FixPoint stranger, FixPoint.SnapEventArgs e)
        {
            if (stranger.Type == Core.Parts.FixPoint.Types.Start || stranger.Type == Core.Parts.FixPoint.Types.End)
            {
                if (stranger.Type == Core.Parts.FixPoint.Types.Start)
                {
                    RightFixPoint.LocalYaw = (float)Math.PI / 2;
                }
                else
                {
                    RightFixPoint.LocalYaw = -(float)Math.PI / 2;
                }
            }
            else
                e.Cancel = true;
        }

        void LeftFixPoint_OnUnSnapped(FixPoint stranger)
        {
            leftMode = Modes.None;
            NextConveyorLeft = null;
            if (NextAvailableStatusLeft != null) NextAvailableStatusLeft.OnRouteStatusChanged -= NextAvailableStatusLeft_OnAvailableChanged;
            NextAvailableStatusLeft = null;
            PreviousConveyorLeft = null;
            if (PreviousLoadWaitingStatusLeft != null) PreviousLoadWaitingStatusLeft.OnLoadWaitingChanged -= PreviousLoadWaitingStatusLeft_OnLoadWaitingChanged;
            PreviousLoadWaitingStatusLeft = null;
            ThisAvailableStatusMergeLeft = null;

            Reset();
        }

        void LeftFixPoint_OnSnapped(FixPoint stranger, FixPoint.SnapEventArgs e)
        {
            if (stranger.Type == Core.Parts.FixPoint.Types.Start)
            {
                leftMode = Modes.Divert;
                leftTransport.LocalYaw = -(float)Math.PI / 2;
                leftAP.Distance = 0;

                NextConveyorLeft = stranger.Parent as IRouteStatus;
                NextAvailableStatusLeft = NextConveyorLeft.GetRouteStatus(stranger);
                NextAvailableStatusLeft.OnRouteStatusChanged += NextAvailableStatusLeft_OnAvailableChanged;
            }
            else if (stranger.Type == FixPoint.Types.End)
            {
                leftMode = Modes.Merge;
                leftTransport.LocalYaw = (float)Math.PI / 2;
                leftAP.Distance = leftTransport.Route.Length;// -0.01f;

                PreviousConveyorLeft = stranger.Parent as IRouteStatus;
                PreviousLoadWaitingStatusLeft = PreviousConveyorLeft.GetLoadWaitingStatus(stranger);
                PreviousLoadWaitingStatusLeft.OnLoadWaitingChanged += PreviousLoadWaitingStatusLeft_OnLoadWaitingChanged;
            }
        }

        void LeftFixPoint_OnBeforeSnapping(FixPoint sender, FixPoint stranger, FixPoint.SnapEventArgs e)
        {
            if (stranger.Type == Core.Parts.FixPoint.Types.Start || stranger.Type == Core.Parts.FixPoint.Types.End)
            {
                if (stranger.Type == Core.Parts.FixPoint.Types.Start)
                {
                    LeftFixPoint.LocalYaw = -(float)Math.PI / 2;
                }
                else
                {
                    LeftFixPoint.LocalYaw = (float)Math.PI / 2;
                }
            }
            else
                e.Cancel = true;
        }

        void StartFixPoint_OnSnapped(Core.Parts.FixPoint stranger, Core.Parts.FixPoint.SnapEventArgs e)
        {
            PreviousConveyorStraight = stranger.Parent as IRouteStatus;
            PreviousLoadWaitingStatusStraight = PreviousConveyorStraight.GetLoadWaitingStatus(stranger);
            if (PreviousLoadWaitingStatusStraight != null)
            {
                PreviousLoadWaitingStatusStraight.OnLoadWaitingChanged += PreviousLoadWaitingStatusStraight_OnLoadWaitingChanged;
                PreviousAvailableStatusStraight = PreviousConveyorStraight.GetRouteStatus(stranger);
                if (PreviousAvailableStatusStraight != null)
                {
                    PreviousAvailableStatusStraight.OnRouteStatusChanged += PreviousAvailableStatusStraight_OnRouteStatusChanged;
                }
            }
        }

        void StartFixPoint_OnUnSnapped(Core.Parts.FixPoint stranger)
        {
            PreviousConveyorStraight = null;
            PreviousLoadWaitingStatusStraight.OnLoadWaitingChanged -= PreviousLoadWaitingStatusStraight_OnLoadWaitingChanged;
            if (PreviousAvailableStatusStraight != null)
            {
                PreviousAvailableStatusStraight.OnRouteStatusChanged -= PreviousAvailableStatusStraight_OnRouteStatusChanged;
            }
            PreviousLoadWaitingStatusStraight = null;
            ThisAvailableStatusMergeStraight = null;
            PreviousAvailableStatusStraight = null;

            Reset();
        }

        void EndFixPoint_OnSnapped(FixPoint stranger, FixPoint.SnapEventArgs e)
        {
            straightMode = Modes.Divert;
            NextConveyorStraight = stranger.Parent as IRouteStatus;
            NextAvailableStatusStraight = NextConveyorStraight.GetRouteStatus(stranger);
            if (NextAvailableStatusStraight != null)
            {
                NextAvailableStatusStraight.OnRouteStatusChanged += NextAvailableStatusStraight_OnAvailableChanged;
            }
            if (!Core.Environment.Scene.Loading) Reset();
        }

        void EndFixPoint_OnUnSnapped(FixPoint stranger)
        {
            straightMode = Modes.None;
            NextConveyorStraight = null;
            NextAvailableStatusStraight.OnRouteStatusChanged -= NextAvailableStatusStraight_OnAvailableChanged;
            NextAvailableStatusStraight = null;
        }
        
        private bool AvailableSetOnAnyMerge()
        {
            if (ThisAvailableStatusMergeStraight != null && ThisAvailableStatusMergeStraight.Available == RouteStatuses.Available)
                return true;
            if (ThisAvailableStatusMergeLeft != null && ThisAvailableStatusMergeLeft.Available == RouteStatuses.Available)
                return true;
            if (ThisAvailableStatusMergeRight != null && ThisAvailableStatusMergeRight.Available == RouteStatuses.Available)
                return true;

            return false;
        }

        private bool LoadAciveOnAnyConveyor()
        {
            if (TransportSection.Route.Loads.Count > 0 || rightTransport.Route.Loads.Count > 0 || leftTransport.Route.Loads.Count > 0)
                return true;
            else
                return false;
        }

        void PreviousLoadWaitingStatusStraight_OnLoadWaitingChanged(object sender, LoadWaitingChangedEventArgs e)
        {
            //If a load arrives and there is nothing else in the divert make it available
            if (e._loadWaiting)
            {
                if (!AvailableSetOnAnyMerge() && !LoadActive)
                    ThisAvailableStatusMergeStraight.Available = RouteStatuses.Available;
                else
                    mergeQueue_Add(Direction.Straight);
            }
            if (!e._loadWaiting)
            {
                //Once the load has stopped waiting, then the load will become active in the transfer
                if (ThisAvailableStatusMergeStraight.Available == RouteStatuses.Available)
                {
                    //Testing forward blocking
                    if (leftMode != Modes.Divert && rightMode != Modes.Divert && NextAvailableStatusStraight != null && NextAvailableStatusStraight.Available == RouteStatuses.Blocked)
                    {
                        ThisAvailableStatusMergeStraight.Available = RouteStatuses.Blocked;
                    }
                    else
                    {
                        ThisAvailableStatusMergeStraight.Available = RouteStatuses.Request;
                    }

                    if (!e._loadDeleted)
                    {
                        LiftTableActiveLoad = e._waitingLoad;
                    }
                    else
                    {
                        LiftTableActiveLoad = null;
                        LoadExitConveyor(null);
                    }
                }
                else //The load has been deleted or has gone on another route
                {
                    mergeQueue_Remove(Direction.Straight);
                }
            }
        }

        void PreviousLoadWaitingStatusLeft_OnLoadWaitingChanged(object sender, LoadWaitingChangedEventArgs e)
        {
            //If a load arrives and there is nothing else in the divert make it available
            if (e._loadWaiting)
            {
                if (!AvailableSetOnAnyMerge() && !LoadActive)
                    //ThisAvailableStatusMergeLeft.Available = RouteStatuses.Available;
                    LeftRouteStatus = RouteStatuses.Available;
                else
                    mergeQueue_Add(Direction.Left);
            }
            //Once the load has stopped waiting, then the load will become active
            if (!e._loadWaiting)
            {
                if (ThisAvailableStatusMergeLeft.Available == RouteStatuses.Available)
                {
                    //Testing forward blocking
                    if (leftMode != Modes.Divert && rightMode != Modes.Divert && NextAvailableStatusStraight != null && NextAvailableStatusStraight.Available == RouteStatuses.Blocked)
                    {
                        LeftRouteStatus = RouteStatuses.Blocked;
                        //ThisAvailableStatusMergeLeft.Available = RouteStatuses.Blocked;
                    }
                    else
                    {
                        LeftRouteStatus = RouteStatuses.Request;
                        //ThisAvailableStatusMergeLeft.Available = RouteStatuses.Request;
                    }

                    if (!e._loadDeleted)
                    {
                        LiftTableActiveLoad = e._waitingLoad;
                    }
                    else
                    {
                        LiftTableActiveLoad = null;
                        LoadExitConveyor(null);
                    }
                }
                else //The load has been deleted or has gone on another route
                {
                    mergeQueue_Remove(Direction.Left);
                }
            }
        }

        void PreviousLoadWaitingStatusRight_OnLoadWaitingChanged(object sender, LoadWaitingChangedEventArgs e)
        {
            //If a load arrives and there is nothing else in the divert make it available
            if (e._loadWaiting)
            {
                if (!AvailableSetOnAnyMerge() && !LoadActive)
                    //ThisAvailableStatusMergeRight.Available = RouteStatuses.Available;
                    RightRouteStatus = RouteStatuses.Available;
                else
                    mergeQueue_Add(Direction.Right);
            }

            //Once the load has stopped waiting, then the load will become active
            if (!e._loadWaiting)
            {
                if (ThisAvailableStatusMergeRight.Available == RouteStatuses.Available)
                {
                    //Testing forward blocking
                    if (leftMode != Modes.Divert && rightMode != Modes.Divert && NextAvailableStatusStraight != null && NextAvailableStatusStraight.Available == RouteStatuses.Blocked)
                    {
                        RightRouteStatus = RouteStatuses.Blocked;
                        //ThisAvailableStatusMergeRight.Available = RouteStatuses.Blocked;
                    }
                    else
                    {
                        RightRouteStatus = RouteStatuses.Request;
                        //ThisAvailableStatusMergeRight.Available = RouteStatuses.Request;
                    }

                    if (!e._loadDeleted)
                    {
                        LiftTableActiveLoad = e._waitingLoad;
                    }
                    else
                    {
                        LiftTableActiveLoad = null;
                        LoadExitConveyor(null);
                    }
                }
                else //The load has been deleted or has gone on another route
                {
                    mergeQueue_Remove(Direction.Right);
                }
            }
        }

        void PreviousAvailableStatusStraight_OnRouteStatusChanged(object sender, RouteStatusChangedEventArgs e)
        {
        }
        
        #endregion

        #region Assembly methods Reset, Dispose, DoubleClick, Visible
        public override bool Visible
        {
            get
            {
                return base.Visible;
            }
            set
            {
                base.Visible = value;

                leftTransport.Visible = false;
                rightTransport.Visible = false;
            }
        }

        public override void DoubleClick()
        {

        }

        public override void Reset()
        {
            lastLoadArrived = -1;
            LiftTableActiveLoad = null;
            LoadRoute = Direction.None;
            mergeQueue.Clear();
            timerHasRun = false;
            releaseDelayTimer.Stop();
            releaseDelayTimer.Reset();
            LoadHandledByController = false;

            if (ThisAvailableStatusMergeStraight != null) ThisAvailableStatusMergeStraight.Available = RouteStatuses.Request;
            if (ThisAvailableStatusMergeLeft != null) LeftRouteStatus = RouteStatuses.Request;
            if (ThisAvailableStatusMergeRight != null) RightRouteStatus = RouteStatuses.Request;

            preferedRouting = null;

            LoadExitConveyor(null);

            base.Reset();
        }

        void Route_OnLoadRemoved(Route sender, Load load)
        {
            Experior.Dematic.Base.EuroPallet palletLoad = load as Experior.Dematic.Base.EuroPallet;

            if (load.StartDisposing)
            {
                RemoveLoad(load);
            }

            if (palletLoad.FlowControl == false && !load.StartDisposing)
            {
                palletLoad.FlowControl = true;
                palletLoad.OnDisposed += Load_OnDisposed;
            }
        }

        private void Load_OnDisposed(Load load)
        {
            //load.OnDisposed -= Load_OnDisposed;
            RemoveLoad(load);
        }

        /// <summary>
        /// When switching a load from the the merge divert, then call this to allow the next load in.
        /// </summary>
        /// <param name="load"></param>
        public void RemoveLoad(Load load)
        {
            loadDeleted?.Invoke(load);
            //If the load being dealt with is deleted then tidy all the data ready for the next one to be released in.
            preferedRouting = null;
            //Reset();

            LoadExitConveyor(load);
        }

        public override string Category
        {
            get { return "Pallet Lift Table"; }
        }

        public override System.Drawing.Image Image
        {
            get
            {
                return Common.Icons.Get("MergeDivertConveyor");
            }
        }

        public override void Dispose()
        {
            straightAP.OnEnter -= new ActionPoint.EnterEvent(straightAP_Enter);
            straightEnd.OnEnter -= new ActionPoint.EnterEvent(straightEnd_Enter);
            leftEnd.OnEnter -= new ActionPoint.EnterEvent(leftEnd_Enter);
            rightEnd.OnEnter -= new ActionPoint.EnterEvent(rightEnd_Enter);
            leftAP.OnEnter -= new ActionPoint.EnterEvent(leftAP_Enter);
            rightAP.OnEnter -= new ActionPoint.EnterEvent(rightAP_Enter);

            straightAP.Dispose();
            straightEnd.Dispose();
            leftAP.Dispose();
            rightAP.Dispose();
            leftEnd.Dispose();
            rightEnd.Dispose();

            LeftFixPoint.OnBeforeSnapping -= LeftFixPoint_OnBeforeSnapping;
            LeftFixPoint.OnSnapped -= LeftFixPoint_OnSnapped;
            LeftFixPoint.OnUnSnapped -= LeftFixPoint_OnUnSnapped;
            RightFixPoint.OnBeforeSnapping -= RightFixPoint_OnBeforeSnapping;
            RightFixPoint.OnSnapped -= RightFixPoint_OnSnapped;
            RightFixPoint.OnUnSnapped -= RightFixPoint_OnUnSnapped;

            //Core.Environment.Scene.OnLoaded -= new Core.Environment.Scene.Event(SetPLC);   
            this.OnNameChanged -= new NameChangedEvent(NameChange);

            if (straightAP != null)
            {
                straightAP.Dispose();
            }

            base.Dispose();
        }

        #endregion

        #region Helper methods

        void NameChange(Assembly sender, string current, string old)
        {
            if (straightAP != null)
                straightAP.Name = this.Name;

        }

        private float GetTransportSectionLength(bool isLeft)
        {
            float length = Width / 2;
            if (liftTableInfo.routeOffset != 0)
            {
                length = (Width / 2) + liftTableInfo.routeOffset;
                if (!isLeft)
                {
                    length = (Width / 2) - liftTableInfo.routeOffset;
                }
            }
            return length;
        }

        private float GetTransportPosition(bool isLeft, float leftLength, float rightLength)
        {
            var position = isLeft ? -Width / 4 : Width / 4;
            if (liftTableInfo.routeOffset != 0)
            {
                if (isLeft)
                {
                    position = (Width / 2) - (leftLength / 2) - rightLength;
                }
                else
                {
                    position = (Width / 2) - (rightLength / 2);
                }
            }
            return position;
        }

        void SetupOffsetTransportSections()
        {
            // Update position of infeed route
            var offset = Length / 2;
            LeftFixPoint.LocalPosition = new Microsoft.DirectX.Vector3(0, 0, -liftTableInfo.width / 2);
            RightFixPoint.LocalPosition = new Microsoft.DirectX.Vector3(0, 0, liftTableInfo.width / 2);
            StartFixPoint.LocalPosition = new Microsoft.DirectX.Vector3(offset, 0, liftTableInfo.routeOffset);
            EndFixPoint.LocalPosition = new Microsoft.DirectX.Vector3(-offset, 0, liftTableInfo.routeOffset);
            var leftLength = GetTransportSectionLength(true);
            var rightLength = GetTransportSectionLength(false);
            var leftPosition = GetTransportPosition(true, leftLength, rightLength);
            var rightPosition = GetTransportPosition(false, leftLength, rightLength);
            // Update Left 
            leftTransport.Length = leftLength;
            leftTransport.LocalPosition = new Microsoft.DirectX.Vector3(0, liftTableInfo.thickness / 2, leftPosition);
            leftEnd.Distance = leftTransport.Length - 0.01f;
            // Update Right 
            rightTransport.Length = rightLength;
            rightTransport.LocalPosition = new Microsoft.DirectX.Vector3(0, liftTableInfo.thickness / 2, rightPosition);
            rightEnd.Distance = rightTransport.Length - 0.01f;
        }

        #endregion

        #region Control interface

        [Browsable(false)]
        public Core.Parts.FixPoint RightFix
        {
            get { return RightFixPoint; }
        }

        [Browsable(false)]
        public Core.Parts.FixPoint LeftFix
        {
            get { return LeftFixPoint; }
        }

        [Browsable(false)]
        public Modes LeftMode
        {
            get { return leftMode; }
        }
        [Browsable(false)]
        public Modes RightMode
        {
            get { return rightMode; }
        }
        [Browsable(false)]
        public Modes StraightMode
        {
            get { return straightMode; }
        }

        

        #endregion

        #region User interface

        #region  User interface Position

        [Category("Position")]
        [DisplayName("Height")]
        [Description("Height of the conveyor (meter)")]
        [TypeConverter()]
        [PropertyOrder(9)]
        public override float Height
        {
            get
            {
                return base.Position.Y;
            }
            set
            {
                base.Position = new Microsoft.DirectX.Vector3(base.Position.X, value, base.Position.Z);
            }
        }


        #endregion

        #region User Interface Routing

        private Load _LiftTableActive = null;
        [Browsable(false)]
        public Load LiftTableActiveLoad
        {
            get { return _LiftTableActive; }
            set
            {
                _LiftTableActive = value;
            }
        }
        
        public bool LoadActive
        {
            get
            {
                if (LiftTableActiveLoad == null)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
        }

        [Category("Routing")]
        [DisplayName("Control Type")]
        [Description("Defines if the control is handled by a controller, by a routing script or uses only local control. ")]
        [PropertyOrder(1)]
        public ControlTypes ControlType
        {
            get
            {
                return liftTableInfo.ControlType;
            }
            set
            {
                liftTableInfo.ControlType = value;
                if (ControllerProperties != null && value != ControlTypes.Controller)
                {
                    ControllerName = "No Controller";
                }
                Core.Environment.Properties.Refresh();
            }
        }


        [Category("Routing")]
        [DisplayName("Controller")]
        [Description("Controller name that handles this conveyor")]
        [PropertyAttributesProvider("DynamicPropertyControllers")]
        [TypeConverter(typeof(PalletControllerConverter))]
        [PropertyOrder(2)]
        public string ControllerName
        {
            get
            {
                return liftTableInfo.ControllerName;
            }
            set
            {
                if (!value.Equals(liftTableInfo.ControllerName))
                {
                    ControllerProperties = null;
                    liftTableInfo.ProtocolInfo = null;
                    Controller = null;
                }

                liftTableInfo.ControllerName = value;
                if (value != null)
                {
                    //Note StandardCase.SetMHEControl will set the ControllerProperties and Controller properties!
                    ControllerProperties = StandardCase.SetMHEControl(liftTableInfo, this);
                    if (ControllerProperties == null)
                    {
                        liftTableInfo.ControllerName = "No Controller";
                    }
                }
            }
        }

        public void DynamicPropertyControllers(Core.Properties.PropertyAttributes attributes)
        {
            attributes.IsBrowsable = liftTableInfo.ControlType == ControlTypes.Controller;
        }

        [Category("Routing")]
        [DisplayName("Control")]
        [Description("Embedded routing control with protocol and routing specific configuration")]
        [PropertyOrder(3)]
        [PropertyAttributesProvider("DynamicPropertyAssemblyPLCconfig")]
        public MHEControl ControllerProperties
        {
            get
            {
                return controllerProperties;
            }
            set
            {
                controllerProperties = value;
                if (value == null)
                {
                    Controller = null;
                }
                Experior.Core.Environment.Properties.Refresh();
            }
        }

        public void DynamicPropertyAssemblyPLCconfig(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = Controller != null;
        }

        [Category("Routing")]
        [DisplayName("Default Route")]
        [Description("Set the default route, if there is no entry in the routing table or no routing bits are set then the load will be routed here")]
        [PropertyOrder(3)]
        public Direction DefaultRouting
        {
            get
            {
                return liftTableInfo.defaultRouting;
            }
            set
            {
                if ((value == Direction.Straight && NextConveyorStraight == null) || 
                    (value == Direction.Left && NextConveyorLeft == null) || 
                    (value == Direction.Right && NextConveyorRight == null))
                {
                    Log.Write(string.Format("Warning Merge Divert {0}: Cannot set default routing as there is no connected conveyor on that route", Name), Color.Red);
                    return;
                }

                liftTableInfo.defaultRouting = value;
            }
        }

        [Category("Routing")]
        [DisplayName("Route Blocked Behaviour")]
        [Description("Define what the happens to the load when the routed destination is blocked")]
        [PropertyOrder(4)]
        public RouteBlocked RouteBlockedBehaviour
        {
            get { return liftTableInfo.routeBlockedBehaviour; }
            set
            {
                liftTableInfo.routeBlockedBehaviour = value;
                Experior.Core.Environment.Properties.Refresh();
            }
        }

        [Category("Routing")]
        [DisplayName("Release Delay")]
        [PropertyOrder(5)]
        [Description("Delay time to allow load into merge/divert after the previous load has cleared the conveyor")]
        public float ReleaseDelay
        {
            get { return liftTableInfo.releaseDelay; }
            set
            {
                if (releaseDelayTimer.Running)
                {
                    releaseDelayTimer.Stop();
                    releaseDelayTimer.Timeout = value;
                    releaseDelayTimer.Start();
                }
                else
                    releaseDelayTimer.Timeout = value;
                liftTableInfo.releaseDelay = value;
            }
        }

        [Category("Routing")]
        [DisplayName("Normal Route Blocked TimeOut")]
        [Description("Define how long a load should wait if the route is blocked under normal conditions")]
        [PropertyAttributesProvider("DynamicPropertyRouteBlockedTimeout")]
        [PropertyOrder(6)]
        public float RouteBlockedTimeoutNormal
        {
            get { return liftTableInfo.routeBlockedTimeoutNormal; }
            set
            {
                liftTableInfo.routeBlockedTimeoutNormal = value;
            }
        }

        [Category("Routing")]
        [DisplayName("Priority Route Blocked TimeOut")]
        [Description("Define how long a load should wait if the route is blocked under priority load conditions")]
        [PropertyAttributesProvider("DynamicPropertyRouteBlockedTimeout")]
        [PropertyOrder(7)]
        public float RouteBlockedTimeoutPriority
        {
            get { return liftTableInfo.routeBlockedTimeoutPriority; }
            set
            {
                liftTableInfo.routeBlockedTimeoutPriority = value;
            }
        }

        [Category("Routing")]
        [DisplayName("Force Default on Blocked")]
        [Description("If the load has stopped because the preferred route is not available and is timing out, then force to default route if the previous conveyor becomes full")]
        [PropertyAttributesProvider("DynamicPropertyRouteBlockedTimeout")]
        [PropertyOrder(9)]
        public bool ForceDefaultBlocked
        {
            get { return liftTableInfo.forceDefaultBlocked; }
            set
            {
                liftTableInfo.forceDefaultBlocked = value;
            }
        }

        public void DynamicPropertyRouteBlockedTimeout(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = RouteBlockedBehaviour == RouteBlocked.Wait_Timeout;
        }

        [Category("Routing")]
        [DisplayName("Routing Priority")]
        [Description("Order that the different routes should be checked, used if the load has two valid destinations, therefore which has priority. Format n,n,n where n = 1, 2, or 3 value is checked in the order [straight],[left],[right] e.g. 2,1,3 will check the left route, then the straight route, then the right route")]
        [PropertyAttributesProvider("DynamicPropertyRoutingPriority")]
        [PropertyOrder(9)]
        public string RoutingPriority
        {
            get { return liftTableInfo.routingPriority; }
            set
            {
                int[] checkOrder = new int[3];

                string[] splitValue = value.Split(',');
                if (splitValue.Length == 3)
                {
                    int i = 0;
                    string[] s = new string[] { "", "", "" };
                    foreach (string order in splitValue)
                    {
                        if ((order == "1" || order == "2" || order == "3") && (order != s[0] && order != s[1] && order != s[2]))
                        {
                            int.TryParse(order, out checkOrder[i]);
                            s[i] = order;
                            i++;
                        }
                        else
                            return;
                    }
                    liftTableInfo.routingPriority = value;
                    routingCheckOrder = checkOrder;
                    return;
                }
                else
                    return;
            }
        }

        [PropertyOrder(10)]
        [Category("Routing")]
        [DisplayName("Route Offset")]
        [Description("Postion of the start and end fixpoints on route conveyor from the centre")]
        public float RouteOffset
        {
            get
            {
                return liftTableInfo.routeOffset;
            }
            set
            {
                liftTableInfo.routeOffset = value;
                Core.Environment.Invoke(() => SetupOffsetTransportSections());
            }
        }

        #endregion

        #region User Interface Size and Speed

        [Category("Size and Speed")]
        [DisplayName("Length")]
        [Description("Length of the conveyor based on standard Dematic merge divert lengths")]
        [PropertyOrder(1)]
        public float ConveyorLength
        {
            get { return liftTableInfo.conveyorLength; }
            set
            {
                Length = value;
                liftTableInfo.conveyorLength = value;
            }
        }

        [Category("Size and Speed")]
        [DisplayName("Width")]
        [Description("Width of the conveyor based on standard Dematic conveyor widths")]
        [PropertyOrder(2)]
        public float ConveyorWidth
        {
            get { return liftTableInfo.conveyorWidth; }
            set
            {
                Width = value;
                liftTableInfo.conveyorWidth = value;
            }
        }

        [PropertyOrder(3)]
        [Category("Size and Speed")]
        public override float Speed
        {
            get
            {
                return base.Speed;
            }
            set
            {
                base.Speed = value;
            }
        }

        #endregion

        #region User Interface Status
        RouteStatuses _LeftRouteStatus = RouteStatuses.Request;
        [Category("Status")]
        [DisplayName("Left Available")]
        [Description("Is this conveyor route available to be released into from the left")]
        [ReadOnly(true)]
        public RouteStatuses LeftRouteStatus
        {
            get { return _LeftRouteStatus; }
            set
            {
                if (value != _LeftRouteStatus)
                {
                    _LeftRouteStatus = value;

                    if (ThisAvailableStatusMergeLeft != null)
                        ThisAvailableStatusMergeLeft.Available = value;
                }
            }
        }

        RouteStatuses _RightRouteStatus = RouteStatuses.Request;
        [Category("Status")]
        [DisplayName("Right Available")]
        [Description("Is this conveyor route available to be released into from the right")]
        [ReadOnly(true)]
        public RouteStatuses RightRouteStatus
        {
            get { return _RightRouteStatus; }
            set
            {
                if (value != _RightRouteStatus)
                {
                    _RightRouteStatus = value;

                    if (ThisAvailableStatusMergeRight != null)
                        ThisAvailableStatusMergeRight.Available = value;
                }
            }
        }
        #endregion

        [Browsable(false)]
        public override Route.SpacingTypes SpacingType
        {
            get
            {
                return base.SpacingType;
            }
            set
            {
                base.SpacingType = value;
            }
        }

        [Browsable(false)]
        public override bool Enabled
        {
            get
            {
                return base.Enabled;
            }
            set
            {
                base.Enabled = value;
            }
        }

        [Browsable(false)]
        public override Core.Assemblies.EventCollection Events
        {
            get
            {
                return base.Events;
            }
        }

        [Browsable(false)]
        [TypeConverter()]
        public override float Length
        {
            get
            {
                return base.Length;
            }
            set
            {
                if (value > 0)
                {
                    base.Length = value;
                    Core.Environment.Invoke(() => UpdateLength());
                }
            }
        }

        [Browsable(false)]
        public override float Yaw
        {
            get
            {
                return base.Yaw;
            }
            set
            {
                base.Yaw = value;
            }
        }

        [Browsable(false)]
        public override float Roll
        {
            get
            {
                return base.Roll;
            }
            set
            {
                base.Roll = value;
            }
        }

        void UpdateLength()
        {
            straightAP.Distance = Length / 2;
            straightEnd.Distance = Length;
        }

        [Browsable(false)]
        [TypeConverter()]
        public override float Width
        {
            get
            {
                return base.Width;
            }
            set
            {
                if (value > 0)
                {
                    base.Width = value;
                    Core.Environment.Invoke(() => SetupOffsetTransportSections());
                }
            }
        }

        [Browsable(false)]
        public override bool Bidirectional
        {
            get
            {
                return base.Bidirectional;
            }
            set
            {
                base.Bidirectional = value;
            }
        }

        [Browsable(false)]
        public override float Spacing
        {
            get
            {
                return base.Spacing;
            }
            set
            {
                base.Spacing = value;
            }
        }


        [Browsable(false)]
        public override SnapProperties SnapStartTransformation
        {
            get
            {
                return base.SnapStartTransformation;
            }
            set
            {
                base.SnapStartTransformation = value;
            }
        }

        [Browsable(false)]
        public override SnapProperties SnapEndTransformation
        {
            get
            {
                return base.SnapEndTransformation;
            }
            set
            {
                base.SnapEndTransformation = value;
            }
        }

        [Browsable(false)]
        public override float AccumulationReleaseDelay
        {
            get
            {
                return base.AccumulationReleaseDelay;
            }
            set
            {
                base.AccumulationReleaseDelay = value;
            }
        }

        #endregion

        public void DynamicPropertyDiverting(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = rightMode == Modes.Divert || leftMode == Modes.Divert || EndFixPoint.IsSnapped;
        }
      
        int[] routingCheckOrder = new int[] { 1, 2, 3 };

        public void DynamicPropertyRoutingPriority(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = RightMode == Modes.Divert
                                  || StraightMode == Modes.Divert
                                  || LeftMode == Modes.Divert;
        }

        private MHEControl controllerProperties;
        private IController controller;

        [Browsable(false)]
        public IController Controller
        {
            get
            {
                return controller;
            }
            set
            {
                if (value != null)
                {   //If the PLC is deleted then any conveyor referencing the PLC will need to remove references to the deleted PLC.
                    value.OnControllerDeletedEvent += controller_OnControllerDeletedEvent;
                    value.OnControllerRenamedEvent += controller_OnControllerRenamedEvent;
                }
                else if (controller != null && value == null)
                {
                    controller.OnControllerDeletedEvent -= controller_OnControllerDeletedEvent;
                    controller.OnControllerRenamedEvent -= controller_OnControllerRenamedEvent;
                }
                controller = value;
                Core.Environment.Properties.Refresh();
            }
        }

        void controller_OnControllerRenamedEvent(object sender, EventArgs e)
        {
            liftTableInfo.ControllerName = ((Assembly)sender).Name;
            Core.Environment.Properties.Refresh();
        }

        public void controller_OnControllerDeletedEvent(object sender, EventArgs e)
        {
            if (controller != null) { controller.OnControllerDeletedEvent -= controller_OnControllerDeletedEvent; }
            ControllerName = "No Controller";
            Controller = null;
            ControllerProperties = null;
            liftTableInfo.ProtocolInfo = null;
        }

        #region IControllable Implementation
        #endregion

    }

    [Serializable]
    [XmlInclude(typeof(LiftTableInfo))]
    public class LiftTableInfo : Catalog.Logistic.Track.StraightInfo, IControllableInfo
    {
        public ControlTypes ControlType;
        public bool RemoveFromRoutingTable;
        public bool DivertFullLane = true;
        public bool AlwaysUseDefaultDirection;
        public bool ControllerPoint;
        public bool Keeporientation = true;
        public float conveyorWidth = 0.977f;
        public float conveyorLength = 1.4f;
        public float routeOffset = 0;

        public float releaseDelay = 0;

        //Routing info
        public Direction defaultRouting = Direction.None;
        public RouteBlocked routeBlockedBehaviour = RouteBlocked.Wait_Until_Route_Available;
        public float routeBlockedTimeoutNormal = 5;
        public float routeBlockedTimeoutPriority = 10;
        public bool forceDefaultBlocked = false;
        public string routingPriority = "1,2,3";

        private string controllerName = "No Controller";
        public string ControllerName
        {
            get { return controllerName;}
            set { controllerName = value; }
        }

        private ProtocolInfo protocolInfo;
        public ProtocolInfo ProtocolInfo
        {
            get
            {
                return protocolInfo;
            }
            set
            {
                protocolInfo = value;
            }
        }
    }

    public class LoadArrivalEventArgs : EventArgs
    {
        public readonly Load _load;
        public LoadArrivalEventArgs(Load load)
        {
            _load = load;
        }
    }

    public class DirectionAvailability
    {
        public Direction direction = Direction.None;
        public RouteStatus status = null;
    }
}
