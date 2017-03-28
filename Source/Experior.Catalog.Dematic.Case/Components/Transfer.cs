using Experior.Core.Assemblies;
using Experior.Core.Properties;
using Experior.Core.Routes;
using Experior.Core.TransportSections;
using Experior.Dematic.Base;
using Microsoft.DirectX;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Xml.Serialization;
using Experior.Core.Loads;
using Experior.Dematic.Base.Devices;

namespace Experior.Catalog.Dematic.Case.Components
{
    public class Transfer : Assembly, IControllable 
    {
        private TransferInfo transferInfo;
        private StraightTransportSection leftRightCrossover, rightLeftCrossover;
        //private BaseStraight leftRightCrossover, rightLeftCrossover;
        private StraightConveyor lhsConveyor, rhsConveyor;
        private ActionPoint rhsStartAP, lhsStartAP, rightToLeftStartAP, leftToRightStartAP, rhsEndAP, lhsEndAP, rightToLeftEndAP, leftToRightEndAP;
        private const float convWidth       = 0.5f;
        private const float convThickness   = 0.05f;
        private float crossoverOffset       = 0.2f; //Keep 0.2 clear at each end of the fixpoints to save potential issues/confusion for DHDM.
        private Core.Timer ReleaseDelayTimer;
        public bool ReleaseDelayTimerRunning = false;

        private MHEControl controllerProperties;
        private IController controller;
        //private LoadWaitingStatus lhsPreviousLoadWaitingStatus, rhsPreviousLoadWaitingStatus; //Reference to the waiting status of the previous conveyor

        //Controller subscription Events
        public event EventHandler<TransferArrivalArgs> OnArrivedAtTransferController; //Load has arrived at the infeed point
        public event EventHandler<EventArgs> OnTransferStatusChangedController; //Timer has timed out, or one of the routes has become available
        public event EventHandler<TransferDivertedArgs> OnDivertCompleteController; 

        //Static Routing Script Events
        public static event EventHandler<TransferArrivalArgs> OnArrivedAtTransferRoutingScript;
        public static event EventHandler<TransferDivertedArgs> OnDivertCompleteRoutingScript;

        public Dictionary<Case_Load, Side> PreferredLoadRoutes = new Dictionary<Case_Load, Side>();

        //public List<TransferWaiting> convsWithALoadToRelease = new List<TransferWaiting>();

        public List<Side> mergeQueue = new List<Side>();

        public Transfer(TransferInfo info): base(info)
        {
            transferInfo = info;
            info.height  = info.height * 2; // assembley is placed at height/2 so that it comes out at height ?!!??

            rightLeftCrossover = new StraightTransportSection(info.color, 1, convThickness, 0.25f);
            leftRightCrossover = new StraightTransportSection(info.color, 1, convThickness, 0.25f);

            rightLeftCrossover.Route.Arrow.Visible = false;
            leftRightCrossover.Route.Arrow.Visible = false;

            if (transferInfo.type == TransferType.TwoWay)
            {
                rightLeftCrossover.Route.Motor.Speed = DivertSpeed;
                leftRightCrossover.Route.Motor.Speed = DivertSpeed;
            }
            else if (transferInfo.type == TransferType.DHDM)
            {
                rightLeftCrossover.Route.Motor.Speed = DHDMSpeed;
                leftRightCrossover.Route.Motor.Speed = DHDMSpeed;
            }

            Add(rightLeftCrossover);
            Add(leftRightCrossover);

            rhsConveyor = new StraightConveyor(NewStraightInfo(info));
            lhsConveyor = new StraightConveyor(NewStraightInfo(info));

            rhsConveyor.Name = "rhs";
            lhsConveyor.Name = "lhs";

            if (transferInfo.type == TransferType.TwoWay)
            {
                crossoverOffset = info.length / 2;
            }

            Add(rhsConveyor);
            Add(lhsConveyor);

            lhsConveyor.StartFixPoint.OnSnapped   += lhsStartFixPoint_OnSnapped;
            lhsConveyor.StartFixPoint.OnUnSnapped += lhsStartFixPoint_OnUnSnapped;
            lhsConveyor.OnNextRouteStatusAvailableChanged += lhsConveyor_OnNextRouteStatusAvailableChanged;
            lhsConveyor.EndFixPoint.OnSnapped += lhsEndFixPoint_OnSnapped;

            rhsConveyor.StartFixPoint.OnSnapped   += rhsStartFixPoint_OnSnapped; 
            rhsConveyor.StartFixPoint.OnUnSnapped += rhsStartFixPoint_OnUnSnapped;
            rhsConveyor.OnNextRouteStatusAvailableChanged += rhsConveyor_OnNextRouteStatusAvailableChanged;
            rhsConveyor.EndFixPoint.OnSnapped += rhsEndFixPoint_OnSnapped;

            lhsConveyor.RouteAvailable = RouteStatuses.Request; //We are setting this to request because the DHDM controls the release of loads from the previous conveyor onto it.
            rhsConveyor.RouteAvailable = RouteStatuses.Request; //same here

            rhsStartAP = rhsConveyor.TransportSection.Route.InsertActionPoint(crossoverOffset);
            lhsStartAP = lhsConveyor.TransportSection.Route.InsertActionPoint(crossoverOffset);
            rhsEndAP   = rhsConveyor.TransportSection.Route.InsertActionPoint(DHDMLength - crossoverOffset);
            lhsEndAP   = lhsConveyor.TransportSection.Route.InsertActionPoint(DHDMLength - crossoverOffset);

            rightToLeftStartAP = rightLeftCrossover.Route.InsertActionPoint(0);
            leftToRightStartAP = leftRightCrossover.Route.InsertActionPoint(0);
            rightToLeftEndAP = rightLeftCrossover.Route.InsertActionPoint(0); //Should be placed at the end this is done in UpdateCrossoverSectionAngles when we know the correct length of the crossover sections
            leftToRightEndAP = leftRightCrossover.Route.InsertActionPoint(0); //Should be placed at the end this is done in UpdateCrossoverSectionAngles when we know the correct length of the crossover sections

            rhsStartAP.OnEnter += rhsStartAP_OnEnter;
            lhsStartAP.OnEnter += lhsStartAP_OnEnter;
            rhsEndAP.OnEnter   += rhsEndAP_OnEnter;
            lhsEndAP.OnEnter   += lhsEndAP_OnEnter;

            rightToLeftEndAP.OnEnter += rightToLeftEndAP_OnEnter;
            leftToRightEndAP.OnEnter += leftToRightEndAP_OnEnter;

            DHDMWidth = info.width; //This will adjust the crossover conveyors so that the corossover angles are correct
            DHDMLength = info.length;
            InternalConvWidth = info.internalConvWidth;
            ReleaseDelayTimer = new Core.Timer(ReleaseDelayTimeStraight);  //loads will be only be released when the timer elapses the timer will start when a load transfers 
            ReleaseDelayTimer.OnElapsed += ReleaseDelayTimer_Elapsed;
            ReleaseDelayTimer.AutoReset = false;
            
            Core.Environment.Scene.OnLoaded += Scene_OnLoaded;
            ControllerProperties = StandardCase.SetMHEControl(info, this);
        }

        private void Scene_OnLoaded()
        {
            if (ControllerProperties == null)
            {
                ControllerProperties = StandardCase.SetMHEControl(transferInfo, this);
            }
        }  

        private StraightConveyorInfo NewStraightInfo(TransferInfo info)
        {
            StraightConveyorInfo straightInfo = new StraightConveyorInfo();
            straightInfo.Length               = DHDMLength;
            //straightInfo.Speed                = info.speed;
            straightInfo.thickness            = convThickness;
            straightInfo.color                = info.color;
            straightInfo.conveyorWidth        = CaseConveyorWidth._500mm;
            return straightInfo;
        }

        public override void Reset()
        {
            //convsWithALoadToRelease.Clear();
            mergeQueue.Clear();
            base.Reset();
            lhsConveyor.RouteAvailable = RouteStatuses.Request;
            rhsConveyor.RouteAvailable = RouteStatuses.Request;
            ReleaseDelayTimerRunning = false;
            PreferredLoadRoutes.Clear();
        }

        #region Routing Configuration

        void lhsStartFixPoint_OnSnapped(Core.Parts.FixPoint stranger, Core.Parts.FixPoint.SnapEventArgs e)
        {
            lhsConveyor.PreviousConveyor = stranger.Parent as IRouteStatus;
            lhsConveyor.PreviousLoadWaiting = lhsConveyor.PreviousConveyor.GetLoadWaitingStatus(stranger);
            lhsConveyor.PreviousLoadWaiting.OnLoadWaitingChanged += lhsPreviousLoadWaitingStatus_OnLoadWaitingChanged;
            //lhsPreviousLoadWaitingStatus = lhsConveyor.PreviousConveyor.GetLoadWaitingStatus(stranger);
            //lhsPreviousLoadWaitingStatus.OnLoadWaitingChanged += lhsPreviousLoadWaitingStatus_OnLoadWaitingChanged;
        }

        void lhsStartFixPoint_OnUnSnapped(Core.Parts.FixPoint stranger)
        {
            lhsConveyor.PreviousConveyor = null;
            lhsConveyor.PreviousLoadWaiting.OnLoadWaitingChanged -= lhsPreviousLoadWaitingStatus_OnLoadWaitingChanged;
            lhsConveyor.PreviousLoadWaiting = null;
            //lhsPreviousLoadWaitingStatus.OnLoadWaitingChanged -= lhsPreviousLoadWaitingStatus_OnLoadWaitingChanged;
            //lhsPreviousLoadWaitingStatus = null;
            Reset();
        }

        void rhsStartFixPoint_OnSnapped(Core.Parts.FixPoint stranger, Core.Parts.FixPoint.SnapEventArgs e)
        {
            rhsConveyor.PreviousConveyor = stranger.Parent as IRouteStatus;
            rhsConveyor.PreviousLoadWaiting = rhsConveyor.PreviousConveyor.GetLoadWaitingStatus(stranger);
            rhsConveyor.PreviousLoadWaiting.OnLoadWaitingChanged += rhsPreviousLoadWaitingStatus_OnLoadWaitingChanged;
            //rhsPreviousLoadWaitingStatus = rhsConveyor.PreviousConveyor.GetLoadWaitingStatus(stranger);
            //rhsPreviousLoadWaitingStatus.OnLoadWaitingChanged += rhsPreviousLoadWaitingStatus_OnLoadWaitingChanged;
        }

        void rhsStartFixPoint_OnUnSnapped(Core.Parts.FixPoint stranger)
        {
            rhsConveyor.PreviousConveyor = null;
            rhsConveyor.PreviousLoadWaiting.OnLoadWaitingChanged -= rhsPreviousLoadWaitingStatus_OnLoadWaitingChanged;
            rhsConveyor.PreviousLoadWaiting = null;
            //rhsPreviousLoadWaitingStatus.OnLoadWaitingChanged -= rhsPreviousLoadWaitingStatus_OnLoadWaitingChanged;
            //rhsPreviousLoadWaitingStatus = null;
            Reset();
        }

        void lhsEndFixPoint_OnSnapped(Core.Parts.FixPoint stranger, Core.Parts.FixPoint.SnapEventArgs e)
        {
            //On the two way conveyor set the speed of the straight sections to the speed of the attached conveyor
            if (transferInfo.type == TransferType.TwoWay)
            {
                lhsConveyor.Speed = lhsConveyor.NextConveyor.Speed;
            }
        }

        void rhsEndFixPoint_OnSnapped(Core.Parts.FixPoint stranger, Core.Parts.FixPoint.SnapEventArgs e)
        {
            //On the two way conveyor set the speed of the straight sections to the speed of the attached conveyor
            if (transferInfo.type == TransferType.TwoWay)
            {
                rhsConveyor.Speed = rhsConveyor.NextConveyor.Speed;
            }
        }

        void rhsConveyor_OnNextRouteStatusAvailableChanged(object sender, RouteStatusChangedEventArgs e)
        {
            if (e._available == RouteStatuses.Available)
                NextRouteStatus_OnRouteStatusChanged();        
        }

        void lhsConveyor_OnNextRouteStatusAvailableChanged(object sender, RouteStatusChangedEventArgs e)
        {
            if (e._available == RouteStatuses.Available)
                NextRouteStatus_OnRouteStatusChanged();        
        }

        #endregion

        #region Routing Logic

        /// <summary>
        /// Triggerd each time a load arrives, has finished transfering or is deleted whilst transfering
        /// </summary>
        void lhsPreviousLoadWaitingStatus_OnLoadWaitingChanged(object sender, LoadWaitingChangedEventArgs e)
        {
            PreviousLoadWaitingStatus_OnLoadWaitingChanged(sender, e, lhsConveyor, Side.Left, LHSDefaultDirection);           
        }

        /// <summary>
        /// Triggerd each time a load arrives, has finished transfering or is deleted whilst transfering
        /// </summary>
        void rhsPreviousLoadWaitingStatus_OnLoadWaitingChanged(object sender, LoadWaitingChangedEventArgs e)
        {
            PreviousLoadWaitingStatus_OnLoadWaitingChanged(sender, e, rhsConveyor, Side.Right, RHSDefaultDirection);      
        }

        private void PreviousLoadWaitingStatus_OnLoadWaitingChanged(object sender, LoadWaitingChangedEventArgs e, StraightConveyor conv, Side arrivalConveyor, Side defaultDirection)
        {
            if (e._loadWaiting)
            {
                if (!mergeQueue.Contains(arrivalConveyor))
                {
                    mergeQueue.Add(arrivalConveyor);
                }

                if (!ReleaseDelayTimer.Running)
                {
                    TransferArrivalArgs transferArrivalArgs = new TransferArrivalArgs(arrivalConveyor, e._waitingLoad, defaultDirection);
                    if (ControlType == ControlTypes.Controller && OnArrivedAtTransferController != null)
                    {
                        OnArrivedAtTransferController(this, transferArrivalArgs);
                    }
                    else if (ControlType == ControlTypes.Project_Script && OnArrivedAtTransferRoutingScript != null)
                    {
                        OnArrivedAtTransferRoutingScript(this, transferArrivalArgs);
                    }
                    else if (ControlType == ControlTypes.Local)
                    {
                        //RouteLoad(side, conv, e._waitingLoad);
                    }
                }
            }
            else if (e._loadDeleted)
            {
                //var v = convsWithALoadToRelease.Find(x => x.ConvWithLoad.Name == conv.Name);
                //convsWithALoadToRelease.Remove(v);

                mergeQueue.Remove(defaultDirection);


            }

            if (!e._loadWaiting)
            {
                conv.RouteAvailable = RouteStatuses.Request;
                Case_Load cLoad = e._waitingLoad as Case_Load;
            }
        }

        /// <summary>
        /// Release the load from infeed point into the transfer
        /// </summary>
        /// <param name="fromSide">Side of the transfer to release</param>
        /// <param name="routeDirection">Route the load will take on the transfer</param>
        /// <param name="load">The load to be routed</param>
        public void RouteLoad(Side fromSide, Side routeDirection, Load load)
        {
            //TransferWaiting waitingConv = new TransferWaiting(conv, direction, load);
            //convsWithALoadToRelease.AddDHDMwaiting(waitingConv);
            //Release();

            StraightConveyor conv = lhsConveyor;
            if (fromSide == Side.Right)
                conv = rhsConveyor;

            //This now will actually just release the load into the divert most other control is from the controller
            load.UserData = routeDirection;
            conv.RouteAvailable = RouteStatuses.Available;
            mergeQueue.Remove(fromSide);

            //When load has been released then depending on the release set the release timer
            //If load has diverted then use the divert timer

            if (ReleaseDelayTimer.Running)
            {
                Log.Write(string.Format("Trying to set the timer but it's running, name {0}, fromSide {1}, routeDirection {2}", Name, fromSide.ToString(), routeDirection.ToString()));
                Pause();
            }
            
            if ((fromSide == Side.Left && routeDirection == Side.Right) || (fromSide == Side.Right && routeDirection == Side.Left)) //Diverted
            {
                ReleaseDelayTimer.Timeout = ReleaseDelayTimeDivert;
            }
            else //Went straight
            {
                ReleaseDelayTimer.Timeout = ReleaseDelayTimeStraight;
            }
            ReleaseDelayTimer.Start();
            ReleaseDelayTimerRunning = true;
        }

        private void NextRouteStatus_OnRouteStatusChanged()
        {
            //Release();
            //Tell the controller that one of the routes has become available, only if the timer is not running (then it will be informed when the timer has stopped)
            if (!ReleaseDelayTimerRunning && OnTransferStatusChangedController != null)
            {
                OnTransferStatusChangedController(this, new EventArgs());
            }
        }

        void ReleaseDelayTimer_Elapsed(Core.Timer sender)
        {
            ReleaseDelayTimerRunning = false;
            ReleaseDelayTimer.Reset();
            OnTransferStatusChangedController(this, new EventArgs());
            //Release();              
        }

        //void Release()
        //{

        //    if (!ReleaseDelayTimer.Running)
        //    {
        //        StraightConveyor[] conv = TransferWaiting.GetNextConv(lhsConveyor, rhsConveyor, this);

        //        if (conv != null)// && conv[1].NextRouteStatus.Available == RouteStatuses.Available)
        //        {
        //            if (conv[0] == conv[1])
        //            {
        //                //Load is going straight (use straight timer)
        //                ReleaseDelayTimer.Timeout = ReleaseDelayTimeStraight;
        //            }
        //            else
        //            {
        //                //Load is diverting (use divert timer)
        //                ReleaseDelayTimer.Timeout = ReleaseDelayTimeDivert;
        //            }

        //            ReleaseDelayTimer.Start();
        //            conv[0].RouteAvailable = RouteStatuses.Available;
        //        }
        //    }
        //    return;
        //}

        #endregion

        #region Helper Methods

        /// <summary>
        /// Check if the selected route is available
        /// </summary>
        /// <param name="defaultDirection">Side of transfer to check if available</param>
        /// <returns></returns>
        public bool RouteAvailable(Side defaultDirection)
        {
            if (defaultDirection == Side.Left)
            {
                if (lhsConveyor.NextRouteStatus != null && lhsConveyor.NextRouteStatus.Available == RouteStatuses.Available)
                    return true;
            }
            else if (defaultDirection == Side.Right)
            {
                if (rhsConveyor.NextRouteStatus != null && rhsConveyor.NextRouteStatus.Available == RouteStatuses.Available)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Returns load waiting status of the specified previous conveyor side
        /// </summary>
        /// <param name="side"></param>
        /// <returns></returns>
        public LoadWaitingStatus SideLoadWaitingStatus(Side side)
        {
            if (side == Side.Left)
            {
                return lhsConveyor.PreviousLoadWaiting;
            }
            else if (side == Side.Right)
            {
                return rhsConveyor.PreviousLoadWaiting;
            }
            return null;
        }

        /// <summary>
        /// Returns the route status of the specified next conveyor side
        /// </summary>
        /// <param name="side"></param>
        /// <returns></returns>
        public RouteStatus SideNextRouteStatus(Side side)
        {
            if (side == Side.Left)
            {
                return lhsConveyor.NextRouteStatus;
            }
            else if (side == Side.Right)
            {
                return rhsConveyor.NextRouteStatus;
            }
            return null;
        }
        
        #endregion


        void leftToRightEndAP_OnEnter(ActionPoint sender, Core.Loads.Load load)
        {
            load.Switch(rhsEndAP, true);
            //load.Yaw = 0;
            DivertedRight(load);
        }

        void rightToLeftEndAP_OnEnter(ActionPoint sender, Core.Loads.Load load)
        {
            load.Switch(lhsEndAP, true);
            //load.Yaw = 0;
            DivertedLeft(load);
        }

        void lhsEndAP_OnEnter(ActionPoint sender, Load load)
        {
        }

        void rhsEndAP_OnEnter(ActionPoint sender, Load load)
        {
        }

        void lhsStartAP_OnEnter (ActionPoint sender, Core.Loads.Load load)
        {
            if (!StartAP_OnEnter(load, Side.Right, leftToRightStartAP)) //If not switching then load has diverted
            {
                DivertedLeft(load);
            }
        }

        void rhsStartAP_OnEnter(ActionPoint sender, Core.Loads.Load load)
        {
            if (!StartAP_OnEnter(load, Side.Left, rightToLeftStartAP)) //If not switching then load has diverted
            {
                DivertedRight(load);
            }
        }

        private void DivertedRight(Load load)
        {
            if (ControlType == ControlTypes.Controller && OnArrivedAtTransferController != null)
            {
                if (OnDivertCompleteController != null)
                {
                    OnDivertCompleteController(this, new TransferDivertedArgs(Side.Right, load));
                }
            }
            else if (ControlType == ControlTypes.Project_Script && OnArrivedAtTransferRoutingScript != null)
            {
                if (OnDivertCompleteRoutingScript != null)
                {
                    OnDivertCompleteRoutingScript(this, new TransferDivertedArgs(Side.Right, load));
                }
            }
        }

        private void DivertedLeft(Load load)
        {
            if (ControlType == ControlTypes.Controller && OnArrivedAtTransferController != null)
            {
                if (OnDivertCompleteController != null)
                {
                    OnDivertCompleteController(this, new TransferDivertedArgs(Side.Left, load));
                }
            }
            else if (ControlType == ControlTypes.Project_Script && OnArrivedAtTransferRoutingScript != null)
            {
                if (OnDivertCompleteRoutingScript != null)
                {
                    OnDivertCompleteRoutingScript(this, new TransferDivertedArgs(Side.Left, load));
                }
            }
        }

        private bool StartAP_OnEnter(Core.Loads.Load load, Side side, ActionPoint ap)
        {
            Side? s = load.UserData as Side?;
            if (s != null)
            {
                if (s == side)
                {
                    if (transferInfo.type == TransferType.TwoWay)
                    {
                        load.Yaw = -(float)(Math.PI / 180 * (90));
                    }

                    load.Switch(ap, true);
                    return true;
                }
            }
            return false;
        }

        private void UpdateCrossoverSectionAngles()
        {
            if (transferInfo.type == TransferType.DHDM)
            {
                double adj   = DHDMLength - (crossoverOffset * 2);
                double opp   = DHDMWidth - lhsConveyor.Width;// convWidth;
                double theta = Math.Atan(opp / adj);
                double hyp   = adj / Math.Cos(theta);

                rightLeftCrossover.Length   = (float)hyp;
                leftRightCrossover.Length   = (float)hyp;
                leftRightCrossover.LocalYaw = (float)theta;
                rightLeftCrossover.LocalYaw = -(float)theta;

                rightToLeftEndAP.Distance = rightLeftCrossover.Length;
                leftToRightEndAP.Distance = leftRightCrossover.Length;
            }
            else if (transferInfo.type == TransferType.TwoWay)
            {
                rightLeftCrossover.Length = DHDMWidth - rhsConveyor.Width;
                leftRightCrossover.Length = DHDMWidth - rhsConveyor.Width;

                leftRightCrossover.Width  = DHDMLength;
                rightLeftCrossover.Width  = DHDMLength;

                rightToLeftEndAP.Distance = rightLeftCrossover.Length;
                leftToRightEndAP.Distance = leftRightCrossover.Length;
                 
                rightLeftCrossover.LocalYaw = -(float)((Math.PI / 180) * 90);
                leftRightCrossover.LocalYaw = (float)((Math.PI / 180) * 90);
            }
        }

        /// <summary>
        /// This will be set by setting the ControllerName in method StandardCase.SetMHEControl(commPointInfo, this) !!
        /// </summary>
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

        private void controller_OnControllerDeletedEvent(object sender, EventArgs e)
        {
            if (controller != null)
            {
                controller.OnControllerDeletedEvent -= controller_OnControllerDeletedEvent;
            }
            ControllerName = "No Controller";
            Controller = null;
            ControllerProperties = null;
        }

        void controller_OnControllerRenamedEvent(object sender, EventArgs e)
        {
            transferInfo.ControllerName = ((Assembly)sender).Name;
            Core.Environment.Properties.Refresh();
        }

        #region User Interface

        #region Standard Properties

        public override string Category
        {
            get { return "Assembly"; }
        }

        public override Image Image
        {
            get
            {
                if (transferInfo.type == TransferType.DHDM)
                {
                    return Common.Icons.Get("2Way");
                }

                return Common.Icons.Get("DHDM");
            }
        }


        #endregion

        #region User Interface Size and Speed

        [Category("Size and Speed")]
        [DisplayName("Length")]
        [Description("Length of the DHDM conveyor (meter)")]
        [TypeConverter()]
        [PropertyOrder(1)]        
        public float DHDMLength
        {
            get { return transferInfo.length; }
            set
            {
                if (value > 0)
                {
                    transferInfo.length = value;

                    rhsConveyor.Length  = value;
                    lhsConveyor.Length  = value;

                    rhsConveyor.LocalPosition = new Vector3(rhsConveyor.LocalPosition.X, rhsConveyor.LocalPosition.Y, rhsConveyor.LocalPosition.Z);
                    lhsConveyor.LocalPosition = new Vector3(lhsConveyor.LocalPosition.X, lhsConveyor.LocalPosition.Y, lhsConveyor.LocalPosition.Z);
                    
                    rhsEndAP.Distance = value - crossoverOffset;
                    lhsEndAP.Distance = value - crossoverOffset;
                    Core.Environment.Invoke(() => UpdateCrossoverSectionAngles());
                }
            }
        }

        [Category("Size and Speed")]
        [DisplayName("Width")]
        [Description("Width of the Transfer (meter)")]
        [TypeConverter()]
        [PropertyOrder(2)]        
        public float DHDMWidth
        {
            get { return transferInfo.width; }
            set
            {
                if (value > 0)
                {
                    transferInfo.width  = value;
                    rhsConveyor.LocalPosition = new Vector3(rhsConveyor.LocalPosition.X, rhsConveyor.LocalPosition.Y, (value / 2) - (rhsConveyor.Width / 2));
                    lhsConveyor.LocalPosition = new Vector3(lhsConveyor.LocalPosition.X, lhsConveyor.LocalPosition.Y, -((value / 2) - (lhsConveyor.Width / 2)));
                    Core.Environment.Invoke(() => UpdateCrossoverSectionAngles());
                    
                }
            }
        }

        [Category("Size and Speed")]
        [DisplayName("Internal Conv Width")]
        [Description("Width of the internal assembly conveyor based on standard Dematic case conveyor widths")]
        [TypeConverter()]
        [PropertyOrder(3)]
        public CaseConveyorWidth InternalConvWidth
        {
            get { return transferInfo.internalConvWidth; }
            set
            {
                if (value > 0)
                {
                    transferInfo.internalConvWidth = value;
                    lhsConveyor.ConveyorWidth = value;
                    rhsConveyor.ConveyorWidth = value;

                    float w = DHDMWidth;  //toggling the width for some reason !!
                    DHDMWidth = 1;
                    DHDMWidth = w;
                    Core.Environment.Invoke(() => UpdateCrossoverSectionAngles());
                }
            }
        } 

        [Category("Size and Speed")]
        [DisplayName("Diverter Speed")]
        [Description("Speed of divert transfer sections conveyors, the speed of the straight sections is the same speed as the connected conveyor")]
        [TypeConverter()]
        [PropertyAttributesProvider("TwoWayTypeSpeed")]
        [PropertyOrder(4)]        
        public float DivertSpeed
        {
            get { return transferInfo.DivertSpeed; }
            set
            {
                if (transferInfo.type == TransferType.TwoWay)
                {
                    leftRightCrossover.Route.Motor.Speed = value;
                    rightLeftCrossover.Route.Motor.Speed = value;
                }

                transferInfo.DivertSpeed = value;
                Core.Environment.Properties.Refresh();
            }
        }

        public void TwoWayTypeSpeed(Core.Properties.PropertyAttributes attributes)
        {
            attributes.IsBrowsable = transferInfo.type == TransferType.TwoWay;
        }

        [Category("Size and Speed")]
        [DisplayName("DHDM Speed")]
        [Description("Speed of DHDM Conveyors")]
        [TypeConverter()]
        [PropertyAttributesProvider("DHDMTypeSpeed")]
        [PropertyOrder(4)]
        public float DHDMSpeed
        {
            get { return transferInfo.DHDMSpeed; }
            set
            {
                if (transferInfo.type == TransferType.DHDM)
                {
                    leftRightCrossover.Route.Motor.Speed = value;
                    rightLeftCrossover.Route.Motor.Speed = value;
                    lhsConveyor.Speed = value;
                    rhsConveyor.Speed = value;
                }

                transferInfo.DHDMSpeed = value;
                Core.Environment.Properties.Refresh();
            }
        }

        public void DHDMTypeSpeed(Core.Properties.PropertyAttributes attributes)
        {
            attributes.IsBrowsable = transferInfo.type == TransferType.DHDM;
        }

        #endregion

        #region User Interface Routing

        [Category("Routing")]
        [DisplayName("Control Type")]
        [Description("Defines if the control is handled by a controller, by a routing script or uses only local control. ")]
        [PropertyOrder(1)]
        public ControlTypes ControlType
        {
            get
            {
                return transferInfo.ControlType;
            }
            set
            {
                transferInfo.ControlType = value;
                Core.Environment.Properties.Refresh();
            }
        }

        [Category("Routing")]
        [DisplayName("Controller")]
        [Description("Controller name that handles this conveyor")]
        [PropertyAttributesProvider("DynamicPropertyControllers")]
        [TypeConverter(typeof(CaseControllerConverter))]
        [PropertyOrder(2)]
        public string ControllerName
        {
            get
            {
                return transferInfo.ControllerName;
            }
            set
            {
                if (!value.Equals(transferInfo.ControllerName))
                {
                    ControllerProperties = null;
                    transferInfo.ProtocolInfo = null;
                    Controller = null;
                }

                transferInfo.ControllerName = value;
                if (value != null)
                {
                    //Note StandardCase.SetMHEControl will set the ControllerProperties and Controller properties!
                    ControllerProperties = StandardCase.SetMHEControl(transferInfo, this);
                    if (ControllerProperties == null)
                    {
                        transferInfo.ControllerName = "No Controller";
                    }
                }
            }
        }

        /// <summary>
        /// Generic property for a PLC of any type, DatCom, DCI etc it is set when the ControllerName is set
        /// </summary>
        [Category("Routing")]
        [DisplayName("Controller Setup")]
        [PropertyAttributesProvider("DynamicPropertyAssemblyPLCconfig")]
        [PropertyOrder(3)]
        public MHEControl ControllerProperties
        {
            get { return controllerProperties; }
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


        public void DynamicPropertyControllers(Core.Properties.PropertyAttributes attributes)
        {
            attributes.IsBrowsable = transferInfo.ControlType == ControlTypes.Controller;
        }

        public void DynamicPropertyAssemblyPLCconfig(Core.Properties.PropertyAttributes attributes)
        {
            attributes.IsBrowsable = Controller != null && transferInfo.ControlType == ControlTypes.Controller;
        }


        [Category("Routing")]
        [DisplayName("RHS Default Direction")]
        [Description("Default direction that will be used if no controller.")]
        [TypeConverter()]
        [PropertyOrder(4)]
        public Side RHSDefaultDirection
        {
            get { return transferInfo.rhsDefaultDirection; }
            set
            {
                transferInfo.rhsDefaultDirection = value;
            }
        }

        [Category("Routing")]
        [DisplayName("LHS Default Direction")]
        [Description("Default direction that will be used if no controller.")]
        [TypeConverter()]
        [PropertyOrder(5)]
        public Side LHSDefaultDirection
        {
            get { return transferInfo.lhsDefaultDirection; }
            set
            {
                transferInfo.lhsDefaultDirection = value;
            }
        }

        [Category("Routing")]
        [DisplayName("Release Time Straight (s)")]
        [Description("Time interval between loads after the load has released straight")]
        [PropertyOrder(6)]
        public float ReleaseDelayTimeStraight
        {
            get { return transferInfo.releaseDelayTimeStraight; }
            set
            {
                transferInfo.releaseDelayTimeStraight = value;
            }
        }

        [Category("Routing")]
        [DisplayName("Release Time Divert (s)")]
        [Description("Time interval between loads after the load has released to divert")]
        [PropertyOrder(7)]
        public float ReleaseDelayTimeDivert
        {
            get { return transferInfo.releaseDelayTimeDivert; }
            set
            {
                transferInfo.releaseDelayTimeDivert = value;
            }
        }


        #endregion

        #region User Interface Position

        [Category("Position")]
        [DisplayName("Height")]
        [Description("Height of the transfer (meter)")]
        [TypeConverter()]
        public float DHDMHeight
        {
            get { return Position.Y;}
            set
            {
                Position = new Vector3(Position.X, value, Position.Z);
                Core.Environment.Properties.Refresh();
            }
        }       
       
        #endregion

        [Browsable(false)]
        public override Core.Assemblies.EventCollection Events
        {
            get
            {
                return base.Events;
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

        [Browsable(true)]
        public override Color Color
        {
            get
            {
                return base.Color;
            }

            set
            {
                base.Color = value;
                lhsConveyor.Color = value;
                rhsConveyor.Color = value;
                leftRightCrossover.Color = value;
                rightLeftCrossover.Color = value;
            }
        }
        #endregion

    }

    [Serializable]
    [XmlInclude(typeof(TransferInfo))]
    public class TransferInfo : AssemblyInfo, IControllableInfo
    {
        public Side rhsDefaultDirection = Side.Right;
        public Side lhsDefaultDirection = Side.Left;
        public float releaseDelayTimeStraight = 1.8f;
        public float releaseDelayTimeDivert = 4;
        //public float speed = 0.7f; //No Longer Used
        public float DivertSpeed = 0.5f;
        public float DHDMSpeed = 1.2f;
        public ControlTypes ControlType;
        public TransferType type;
        public CaseConveyorWidth internalConvWidth = CaseConveyorWidth._500mm;

        #region Fields

        private static TransferInfo properties = new TransferInfo();

        #endregion

        #region Properties

        public static object Properties
        {
            get
            {
                properties.color = Experior.Core.Environment.Scene.DefaultColor;
                return properties;
            }
        }

        #endregion

        #region IControllableInfo

        private string controllerName = "No Controller";
        public string ControllerName
        {
            get { return controllerName; }
            set { controllerName = value; }
        }

        private ProtocolInfo protocolInfo;
        public ProtocolInfo ProtocolInfo
        {
            get { return protocolInfo; }
            set { protocolInfo = value; }
        }

        #endregion
    }

    public class TransferArrivalArgs : EventArgs
    {
        public readonly Side _fromSide;
        public readonly Load _load;
        public Side _defaultDirection;

        public TransferArrivalArgs(Side fromSide, Load load, Side defaultDirection)
        {
            _fromSide = fromSide;
            _load = load;
            _defaultDirection = defaultDirection;
        }
    }

    public class TransferDivertedArgs : EventArgs
    {
        public readonly Load _load;
        public Side _divertedRoute;

        public TransferDivertedArgs(Side divertedRoute, Load load)
        {
            _load = load;
            _divertedRoute = divertedRoute;
        }
    }



    //public static class DHDMorder
    //{
    //    public static void AddDHDMwaiting(this List<TransferWaiting> list, TransferWaiting item)
    //    {
    //        var v = list.Find(x => x.ConvWithLoad.Name == item.ConvWithLoad.Name);
    //        if (v == null)
    //        {
    //            list.Add(item);
    //        }
    //    }
    //}

    //public class TransferWaiting
    //{
    //    public StraightConveyor ConvWithLoad;
    //    public Side Direction;
    //    public Load LoadToDivert;

    //    public TransferWaiting(StraightConveyor convWithLoad, Side direction, Load load)
    //    {
    //        ConvWithLoad = convWithLoad;
    //        Direction    = direction;
    //        LoadToDivert = load;
    //    }

    //    /// <summary>
    //    /// Chooses the next conveyor that is avaiable
    //    /// </summary>
    //    /// <param name="lhsConveyor"></param>
    //    /// <param name="rhsConveyor"></param>
    //    /// <returns>Returns an array of 2 conveyors the first. If the DHDM is set to crossover then need to make one side avaiable but we have to check the other sides next route to see if it is avaiable 
    //    /// So first position is the DHDM conveyor and the second position is the next conveyor to check</returns>
    //    public static StraightConveyor[] GetNextConv(StraightConveyor lhsConveyor, StraightConveyor rhsConveyor, Transfer transfer) 
    //    {
    //        TransferWaiting conv = null;
    //        StraightConveyor[] returnList = new StraightConveyor[2];

    //        foreach (TransferWaiting nextConv in transfer.convsWithALoadToRelease)
    //        {
    //            if (lhsConveyor.NextRouteStatus != null && (nextConv.Direction == Side.Left && lhsConveyor.NextRouteStatus.Available == RouteStatuses.Available))
    //            {
    //                nextConv.LoadToDivert.UserData = nextConv.Direction; //tag load with direction
    //                conv                           = nextConv;
    //                returnList[0]                  = conv.ConvWithLoad;
    //                break;
    //            }
    //            else if (rhsConveyor.NextRouteStatus != null && (nextConv.Direction == Side.Right && rhsConveyor.NextRouteStatus.Available == RouteStatuses.Available ))
    //            {
    //                nextConv.LoadToDivert.UserData = nextConv.Direction; //tag load with direction
    //                conv                           = nextConv;
    //                returnList[0]                  = conv.ConvWithLoad;
    //                break;
    //            }
    //        }

    //        if (conv != null)
    //        {
    //            if (conv.ConvWithLoad.Name == "lhs" && conv.Direction == Side.Right)
    //            {
    //                returnList[1] = rhsConveyor;
    //            }
    //            else if (conv.ConvWithLoad.Name == "rhs" && conv.Direction == Side.Left)
    //            {
    //                returnList[1] = lhsConveyor;
    //            }
    //            else
    //            {
    //                returnList[1] = returnList[0];
    //            }

    //            transfer.convsWithALoadToRelease.Remove(conv);
    //            return returnList;
    //        }
    //        return null;
    //    }
    //}
}