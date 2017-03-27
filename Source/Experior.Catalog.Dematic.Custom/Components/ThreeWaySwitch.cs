using System;
using System.Drawing;
using System.Xml.Serialization;
using Experior.Core.Assemblies;
using Experior.Core.Routes;
using Microsoft.DirectX;
using Experior.Core.TransportSections;
using System.ComponentModel;
using Experior.Core.Properties;
using Experior.Dematic.Base;
using Experior.Core.Loads;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using Experior.Catalog.Dematic.Case.Components;
using Experior.Dematic.Base.Devices;
using Experior.Catalog.Dematic.Case;

namespace Experior.Catalog.Dematic.Custom.Components
{
    public class ThreeWaySwitch : Assembly, IControllable
    {
        public ThreeWaySwitchInfo threeWaySwitchInfo;
        public StraightThrough LeftConv, RightConv, CenterConv;
        public event EventHandler OnDimensionsChanged;    
        private Core.Timer ReleaseDelayTimer;
        List<StraightThrough> AllRoutes = new List<StraightThrough>();
        ObservableCollection<StraightThrough> TimerReleaseConvs = new ObservableCollection<StraightThrough>(); // A list of convs that the timer will release
        public float internalWidth;

        //Controller subscription Events
        public event EventHandler<ThreeWayArrivalArgs> OnArrivedAtTransferController; //Load has arrived at the infeed point
        public event EventHandler<EventArgs> OnTransferStatusChangedController; //One of the routes has become available
        public event EventHandler<ThreeWayDivertedArgs> OnDivertCompleteController;

        //Static Routing Script Events
        public static event EventHandler<ThreeWayArrivalArgs> OnArrivedAtTransferRoutingScript;
        public static event EventHandler<ThreeWayDivertedArgs> OnDivertCompleteRoutingScript;

        public ThreeWaySwitch(ThreeWaySwitchInfo info): base(info)
        {
            try
            {
                threeWaySwitchInfo = info;
                ControllerProperties = StandardCase.SetMHEControl(info, this);
                info.height = info.height * 2; // assembley is placed at height/2 so that it comes out at height ?!!??

                LeftConv = new StraightThrough(NewStraightInfo(info), this) { convPosition = ThreeWayRoutes.Left };
                RightConv = new StraightThrough(NewStraightInfo(info), this) { convPosition = ThreeWayRoutes.Right };
                CenterConv = new StraightThrough(NewStraightInfo(info), this) { convPosition = ThreeWayRoutes.Center };

                Add(LeftConv, new Vector3(0, 0, -info.width / 2));
                Add(RightConv, new Vector3(0, 0, info.width / 2));
                Add(CenterConv, new Vector3(0, 0, info.centerOffset));

                ReleaseDelayTimer = new Core.Timer(ReleaseDelayTimeStraight);  //loads will be only be released when the timer elapses the timer will start when a load transfers 
                ReleaseDelayTimer.OnElapsed += ReleaseDelayTimer_OnElapsed;
                ReleaseDelayTimer.AutoReset = false;
                TimerReleaseConvs.CollectionChanged += TimerReleaseConvs_CollectionChanged;

                ThreeWayLength         = info.threeWayLength;
                internalWidth                = info.threeWayWidth - ((float)info.internalConvWidth / 1000);
                CenterOffset           = info.centerOffset;
                LeftDefaultDirection   = info.leftDefaultDirection;
                RightDefaultDirection  = info.rightDefaultDirection;
                CenterDefaultDirection = info.centerDefaultDirection;
                InternalConvWidth      = info.internalConvWidth;
                Speed                  = info.speed;

                ReleaseDelayTimeStraight = info.releaseDelayTimeStraight;
                Core.Environment.Scene.OnLoaded += Scene_OnLoaded;
            }
            catch (Exception e)
            {
                Log.Write(e.Message);
            }
        }

        void Scene_OnLoaded()
        {
            if (ControllerProperties == null)
            {
                ControllerProperties = StandardCase.SetMHEControl(threeWaySwitchInfo, this);
            }
        }

        public override void Reset()
        {
            AllRoutes.ForEach(x => x.WaitingConveyors.Clear());
            TimerReleaseConvs.Clear();
            base.Reset();
        }
       
        /// <summary>
        /// Conveyors will be added to TimerReleaseConvs when they can be released
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void TimerReleaseConvs_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                TryRelease();
                return;
            }
        }

        void ReleaseDelayTimer_OnElapsed(Core.Timer sender)
        {
            TryRelease();
        }

        private void TryRelease()
        {
            if (!ReleaseDelayTimer.Running) //Assume that if the timer is not running then must be free to release a load
            {
                if (TimerReleaseConvs.Any())
                {
                    StraightThrough convToRelease = TimerReleaseConvs.First();
                    TimerReleaseConvs.Remove(convToRelease);
                    convToRelease.RouteAvailable = RouteStatuses.Available;
                    ReleaseDelayTimer.Start();
                }
            }
        }

        private StraightConveyorInfo NewStraightInfo(ThreeWaySwitchInfo info)
        {
            StraightConveyorInfo straightInfo = new StraightConveyorInfo();
            //straightInfo.Length = info.length;
            straightInfo.Length = info.threeWayLength;

            //straightInfo.Speed                = info.speed;
            straightInfo.thickness = 0.05f;
            straightInfo.color = Core.Environment.Scene.DefaultColor;
            straightInfo.conveyorWidth = CaseConveyorWidth._500mm;
            return straightInfo;
        }


        #region IControllable
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
                controller = value;
                if (controller != null)
                {   //If the PLC is deleted then any conveyor referencing the PLC will need to remove references to the deleted PLC.
                    controller = value;
                    controller.OnControllerDeletedEvent += controller_OnControllerDeletedEvent;
                    controller.OnControllerRenamedEvent += controller_OnControllerRenamedEvent;
                }
                else if (controller != null && value == null)
                {
                    controller.OnControllerDeletedEvent -= controller_OnControllerDeletedEvent;
                    controller.OnControllerRenamedEvent -= controller_OnControllerRenamedEvent;
                    controller = value;
                }
                Core.Environment.Properties.Refresh();
            }
        }

        [Category("Routing")]
        [DisplayName("Control")]
        [Description("Embedded routing control with protocol and routing specific configuration")]
        [PropertyOrder(21)]
        [PropertyAttributesProvider("DynamicPropertyAssemblyPLCconfig")]
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

        [Category("Routing")]
        [DisplayName("Controller")]
        [Description("Controller name that handles this conveyor")]
        [PropertyAttributesProvider("DynamicPropertyControllers")]
        [PropertyOrder(2)]
        [TypeConverter(typeof(CaseControllerConverter))]
        public string ControllerName
        {
            get
            {
                return threeWaySwitchInfo.ControllerName;
            }
            set
            {
                if (!value.Equals(threeWaySwitchInfo.ControllerName))
                {
                    ControllerProperties = null;
                    threeWaySwitchInfo.ProtocolInfo = null;
                    Controller = null;
                }

                threeWaySwitchInfo.ControllerName = value;
                if (value != null)
                {
                    //Note StandardCase.SetMHEControl will set the ControllerProperties and Controller properties!
                    ControllerProperties = StandardCase.SetMHEControl(threeWaySwitchInfo, this);
                    if (ControllerProperties == null)
                    {
                        threeWaySwitchInfo.ControllerName = "No Controller";
                    }
                }
            }
        }

        #endregion

        public void controller_OnControllerDeletedEvent(object sender, EventArgs e)
        {
            controller.OnControllerDeletedEvent -= controller_OnControllerDeletedEvent;
            ControllerName = "No Controller";
            Controller = null;
            threeWaySwitchInfo.ProtocolInfo = null;
        }

        void controller_OnControllerRenamedEvent(object sender, EventArgs e)
        {
            ControllerName = ((Experior.Core.Assemblies.Assembly)sender).Name;
        }

        public void DynamicPropertyAssemblyPLCconfig(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = Controller != null && threeWaySwitchInfo.controlType == ControlTypes.Controller;
        }

        public void DynamicPropertyControllers(Core.Properties.PropertyAttributes attributes)
        {
            attributes.IsBrowsable = threeWaySwitchInfo.controlType == ControlTypes.Controller;
        }

        [Category("Size and Speed")]
        [DisplayName("Length (m)")]
        [Description("the length in meters.")]
        [PropertyOrder(1)]
        public float ThreeWayLength
        {
            get { return threeWaySwitchInfo.threeWayLength; }
            set
            {
                threeWaySwitchInfo.threeWayLength = value;
                OnDimensionsChanged(this, new EventArgs());
            }
        }

        [Category("Size and Speed")]
        [DisplayName("Width (m)")]
        [Description("The overall width.")]
        [PropertyOrder(2)]
        public float ThreeWayWidth
        {
            get 
            {
                return threeWaySwitchInfo.threeWayWidth;

            }
            set
            {
                threeWaySwitchInfo.threeWayWidth = value;
                internalWidth = value - ((float)InternalConvWidth / 1000);

                //Core.Environment.InvokeEvent(new Action(() => OnDimensionsChanged(this, new EventArgs())));                
                OnDimensionsChanged(this, new EventArgs());
            }
        }

        [Category("Size and Speed")]
        [DisplayName("Center Offset")]
        [Description("The amount that the center is offset from the center ")]
        [PropertyOrder(3)]
        public float CenterOffset
        {
            get { return threeWaySwitchInfo.centerOffset; }
            set
            {
                threeWaySwitchInfo.centerOffset = value;
                OnDimensionsChanged(this, new EventArgs());
            }
        }

        [Category("Size and Speed")]
        [DisplayName("Internal Conv Width")]
        [Description("Width of the internal assembly conveyor based on standard Dematic case conveyor widths")]
        [TypeConverter()]
        [PropertyOrder(4)]
        public CaseConveyorWidth InternalConvWidth
        {
            get { return threeWaySwitchInfo.internalConvWidth; }
            set
            {
                if (value > 0)
                {
                    threeWaySwitchInfo.internalConvWidth = value;
                    AllRoutes.ForEach(x => x.ConveyorWidth = value);

                   // Core.Environment.InvokeEvent(new Action(UpdateCrossoverSectionAngles));
                }
            }
        } 

        [Category("Size and Speed")]
        [DisplayName("speed")]
        [Description("The amount that the center is offset from the center ")]
        [PropertyOrder(5)]
        public float Speed
        {
            get { return threeWaySwitchInfo.speed; }
            set
            {
                threeWaySwitchInfo.speed = value;
                AllRoutes.ForEach(x => x.Speed = value);
            }
        }

        [Category("Routing")]
        [DisplayName("Release Time Straight (s)")]
        [Description("Time interval between loads after the load has released straight")]
        [PropertyOrder(6)]
        public float ReleaseDelayTimeStraight
        {
            get { return threeWaySwitchInfo.releaseDelayTimeStraight; }
            set
            {
                threeWaySwitchInfo.releaseDelayTimeStraight = value;
                ReleaseDelayTimer.Timeout = value;
            }
        }

        [Category("Routing")]
        [DisplayName("Right Default Direction")]
        [Description("Default direction that will be used if no controller.")]
        [TypeConverter()]
        [PropertyOrder(4)]
        public ThreeWayRoutes RightDefaultDirection
        {
            get { return threeWaySwitchInfo.rightDefaultDirection; }
            set
            {
                threeWaySwitchInfo.rightDefaultDirection = value;
                RightConv.DefaultRoute = value;
            }
        }

        [Category("Routing")]
        [DisplayName("Left Default Direction")]
        [Description("Default direction that will be used if no controller.")]
        [TypeConverter()]
        [PropertyOrder(5)]
        public ThreeWayRoutes LeftDefaultDirection
        {
            get { return threeWaySwitchInfo.leftDefaultDirection; }
            set
            {
                threeWaySwitchInfo.leftDefaultDirection = value;
                LeftConv.DefaultRoute = value;
            }
        }

        [Category("Routing")]
        [DisplayName("Center Default Direction")]
        [Description("Default direction that will be used if no controller.")]
        [TypeConverter()]
        [PropertyOrder(5)]
        public ThreeWayRoutes CenterDefaultDirection
        {
            get { return threeWaySwitchInfo.centerDefaultDirection; }
            set
            {
                threeWaySwitchInfo.centerDefaultDirection = value;
                CenterConv.DefaultRoute = value;
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
                return threeWaySwitchInfo.controlType;
            }
            set
            {
                threeWaySwitchInfo.controlType = value;
                Core.Environment.Properties.Refresh();
            }
        }

        public override string Category
        {
            get { return "Assembly"; }
        }

        public override Image Image
        {
            get { return Common.Icons.Get("3way"); }
        }

        #region Straight Through Section and crossover sections

        public class StraightThrough : StraightConveyor
        {
            public ActionPoint startAP, endAP;
            private StraightConveyorInfo straightConveyorInfo;
            public CrossOver crossOverX, crossOverY;
            public ThreeWaySwitch threeWaySwitch;
            public ThreeWayRoutes convPosition; //defines which position the conveyor is in
            public ThreeWayRoutes DefaultRoute;

            public List<StraightThrough> waitingForLeft = new List<StraightThrough>(), waitingForRight = new List<StraightThrough>(), waitingForCenter = new List<StraightThrough>();
            /// <summary>
            /// This is a list that is added to when a route is not avaiable. e.g going from left to right and right is not avaiable then the left conveyor is added to the rights list
            /// </summary>
            public Dictionary<ThreeWayRoutes, List<StraightThrough>> WaitingConveyors = new Dictionary<ThreeWayRoutes, List<StraightThrough>>();

            public StraightThrough(StraightConveyorInfo info, ThreeWaySwitch _threeWaySwitch): base(info)
            {
                straightConveyorInfo = info;
                threeWaySwitch = _threeWaySwitch;

                _threeWaySwitch.AllRoutes.Add(this);
                WaitingConveyors.Add(ThreeWayRoutes.Left, waitingForLeft);
                WaitingConveyors.Add(ThreeWayRoutes.Right, waitingForRight);
                WaitingConveyors.Add(ThreeWayRoutes.Center, waitingForCenter);

                Length = threeWaySwitch.threeWaySwitchInfo.threeWayLength;
                endAP   = TransportSection.Route.InsertActionPoint(Length - 0.3f);
                startAP = TransportSection.Route.InsertActionPoint(0.3f);
                startAP.OnEnter += startAP_OnEnter;
                endAP.OnEnter   += endAP_OnEnter;

                crossOverX = new CrossOver(Core.Environment.Scene.DefaultColor, 1, 0.05f, 0.3f, _threeWaySwitch) { straightThrough = this };
                crossOverY = new CrossOver(Core.Environment.Scene.DefaultColor, 1, 0.05f, 0.3f, _threeWaySwitch) { straightThrough = this };

                Add(crossOverX);
                Add(crossOverY);

                RouteAvailable = RouteStatuses.Request;//3 way will control the release of loads onto the transfer
                _threeWaySwitch.OnDimensionsChanged += threeWaySwitch_OnDimensionsChanged;
                StartFixPoint.OnSnapped             += StraightStartFixPoint_OnSnapped;
                StartFixPoint.OnUnSnapped           += StraightStartFixPoint_OnUnSnapped;
                OnNextRouteStatusAvailableChanged   += StraightThrough_OnNextRouteStatusAvailableChanged;
            }

            void endAP_OnEnter(ActionPoint sender, Load load)
            {
                if (threeWaySwitch.ControlType == ControlTypes.Controller && threeWaySwitch.OnArrivedAtTransferController != null)
                {
                    if (threeWaySwitch.OnDivertCompleteController != null)
                    {
                        threeWaySwitch.OnDivertCompleteController(this, new ThreeWayDivertedArgs(this.convPosition, load));
                    }
                }
                else if (threeWaySwitch.ControlType == ControlTypes.Project_Script && OnArrivedAtTransferRoutingScript != null)
                {
                    if (OnDivertCompleteRoutingScript != null)
                    {
                        OnDivertCompleteRoutingScript(this, new ThreeWayDivertedArgs(this.convPosition, load));
                    }
                }
            }

            public override float Speed
            {
                get { return base.Speed; }
                set
                {
                    crossOverX.Route.Motor.Speed = value;
                    crossOverY.Route.Motor.Speed = value;
                    base.Speed = value;
                }
            }

            void startAP_OnEnter(ActionPoint sender, Core.Loads.Load load)
            {
                //if (threeWaySwitch.ControlType == ControlTypes.Local)
                //{
                //    if (convPosition == ThreeWayRoutes.Right)
                //    {
                //        if (threeWaySwitch.RightDefaultDirection == ThreeWayRoutes.Left)
                //        {
                //            load.Switch(crossOverY.startAP);
                //        }
                //        else if (threeWaySwitch.RightDefaultDirection == ThreeWayRoutes.Center)
                //        {
                //            load.Switch(crossOverX.startAP);
                //        }
                //    }
                //    else if (convPosition == ThreeWayRoutes.Left)
                //    {
                //        if (threeWaySwitch.LeftDefaultDirection == ThreeWayRoutes.Right)
                //        {
                //            load.Switch(crossOverY.startAP);
                //        }
                //        else if (threeWaySwitch.LeftDefaultDirection == ThreeWayRoutes.Center)
                //        {
                //            load.Switch(crossOverX.startAP);
                //        }
                //    }
                //    else if (convPosition == ThreeWayRoutes.Center)
                //    {
                //        if (threeWaySwitch.CenterDefaultDirection == ThreeWayRoutes.Left)
                //        {
                //            load.Switch(crossOverX.startAP);
                //        }
                //        else if (threeWaySwitch.CenterDefaultDirection == ThreeWayRoutes.Right)
                //        {
                //            load.Switch(crossOverY.startAP);
                //        }
                //    }
                //}
                //else if(threeWaySwitch.ControlType == ControlTypes.Controller)
                //{
                    ThreeWayRoutes? exitRoute = null;
                    StraightThrough thisConv = load.UserData as StraightThrough;

                    if (thisConv != null)
                    {
                        exitRoute = thisConv.convPosition;
                    }

                    if(exitRoute != null)
                    {
                        if (convPosition == ThreeWayRoutes.Right)
                        {
                            if (exitRoute == ThreeWayRoutes.Left)
                            {
                                load.Switch(crossOverY.startAP);
                            }
                            else if (exitRoute == ThreeWayRoutes.Center)
                            {
                                load.Switch(crossOverX.startAP);
                            }
                        }
                        else if (convPosition == ThreeWayRoutes.Left)
                        {
                            if (exitRoute == ThreeWayRoutes.Right)
                            {
                                load.Switch(crossOverY.startAP);
                            }
                            else if (exitRoute == ThreeWayRoutes.Center)
                            {
                                load.Switch(crossOverX.startAP);
                            }
                        }
                        else if (convPosition == ThreeWayRoutes.Center)
                        {
                            if (exitRoute == ThreeWayRoutes.Left)
                            {
                                load.Switch(crossOverX.startAP);
                            }
                            else if (exitRoute == ThreeWayRoutes.Right)
                            {
                                load.Switch(crossOverY.startAP);
                            }
                        }
                    }
                //}
            }

            void StraightThrough_OnNextRouteStatusAvailableChanged(object sender, RouteStatusChangedEventArgs e)
            {
                if (e._available == RouteStatuses.Available)
                {
                    if (WaitingConveyors[convPosition].Any())
                    {
                        //Has the conveyor route that has just become avaiable have any loads that want to transfer to it.
                        if (WaitingConveyors[convPosition] != null) //only get on WaitingConveyors from RouteLoad(..)
                        {
                            StraightThrough waitingConv = WaitingConveyors[convPosition].First();
                            WaitingConveyors[convPosition].Remove(waitingConv);
                            threeWaySwitch.TimerReleaseConvs.Add(waitingConv); //simply adding to this list will when the timer stops release it
                        }

                        if (threeWaySwitch.OnTransferStatusChangedController != null)
                        {
                            threeWaySwitch.OnTransferStatusChangedController(this, new EventArgs());
                        }
                    }
                }
            }

            void StraightStartFixPoint_OnUnSnapped(Core.Parts.FixPoint stranger)
            {
                PreviousConveyor = null;
                PreviousLoadWaiting = null;
                PreviousLoadWaiting.OnLoadWaitingChanged -= PreviousLoadWaiting_OnLoadWaitingChanged;
            }

            void StraightStartFixPoint_OnSnapped(Core.Parts.FixPoint stranger, Core.Parts.FixPoint.SnapEventArgs e)
            {
                PreviousConveyor = stranger.Parent as IRouteStatus;
                PreviousLoadWaiting = PreviousConveyor.GetLoadWaitingStatus(stranger);
                PreviousLoadWaiting.OnLoadWaitingChanged += PreviousLoadWaiting_OnLoadWaitingChanged;
            }

            void PreviousLoadWaiting_OnLoadWaitingChanged(object sender, LoadWaitingChangedEventArgs e)
            {
                if (e._loadWaiting)
                {
                    ThreeWayArrivalArgs transferArrivalArgs = new ThreeWayArrivalArgs(this.convPosition, e._waitingLoad, this.DefaultRoute);
                    if (threeWaySwitch.ControlType == ControlTypes.Controller && threeWaySwitch.OnArrivedAtTransferController != null)
                    {
                        threeWaySwitch.OnArrivedAtTransferController(this, transferArrivalArgs);
                    }
                    else if (threeWaySwitch.ControlType == ControlTypes.Project_Script && OnArrivedAtTransferRoutingScript != null)
                    {
                        OnArrivedAtTransferRoutingScript(this, transferArrivalArgs);
                    }
                    else if (threeWaySwitch.ControlType == ControlTypes.Local)
                    {
                        RouteLoad(this, threeWaySwitch.AllRoutes.Find(x => x.convPosition == DefaultRoute), e._waitingLoad);
                    }
                }
                else
                {
                    this.RouteAvailable = RouteStatuses.Request;
                }
            }

            void threeWaySwitch_OnDimensionsChanged(object sender, EventArgs e)
            {
                if (Core.Environment.InvokeRequired)
                {
                    Core.Environment.Invoke(() => threeWaySwitch_OnDimensionsChanged(sender, e));
                    return;
                }

                if (convPosition == ThreeWayRoutes.Left)
                {
                    LocalPosition = new Vector3(0, 0, -threeWaySwitch.internalWidth/ 2);
                }
                else if (convPosition == ThreeWayRoutes.Right)
                {
                    LocalPosition = new Vector3(0, 0, threeWaySwitch.internalWidth / 2);
                }
                else if (convPosition == ThreeWayRoutes.Center)
                {
                    LocalPosition = new Vector3(0, 0, threeWaySwitch.CenterOffset);
                }
                Length = threeWaySwitch.ThreeWayLength;
                endAP.Distance = threeWaySwitch.ThreeWayLength - 0.3f;
            }

            /// <summary>
            /// Release the load from infeed point into the transfer
            /// </summary>
            /// <param name="entrySide">Side of the transfer to release</param>
            /// <param name="exitConv">Route the load will take on the transfer</param>
            /// <param name="load">The load to be routed</param>
            public void RouteLoad(StraightThrough entrySide, StraightThrough exitConv, Load load)
            {
                load.UserData = exitConv;

                if (exitConv.NextRouteStatus.Available == RouteStatuses.Available)
                {
                    threeWaySwitch.TimerReleaseConvs.Add(this);
                }
                else
                {
                    exitConv.WaitingConveyors[exitConv.convPosition].Add(entrySide);
                }

                threeWaySwitch.TryRelease();
            }
        }

        public class CrossOver : StraightTransportSection
        {
            public ActionPoint startAP, endAP;
            private ThreeWaySwitch threeWaySwitch;
            public StraightThrough straightThrough;

            public CrossOver(Color color, float length, float height, float width, ThreeWaySwitch _threeWaySwitch) : base(color, length, height, width)
            {
                threeWaySwitch = _threeWaySwitch;
                startAP = Route.InsertActionPoint(0);
                endAP = Route.InsertActionPoint(length);
                endAP.OnEnter += endAP_OnEnter;
                threeWaySwitch.OnDimensionsChanged += threeWaySwitch_OnDimensionsChanged;
                Route.Arrow.Visible = false;
            }

            /// <summary>
            /// Controls switching the load back to the correct StraightThrough conveyor
            /// </summary>
            void endAP_OnEnter(ActionPoint sender, Core.Loads.Load load)
            {

                ThreeWayRoutes? exitRoute = null;
                StraightThrough thisConv = load.UserData as StraightThrough;

                if (thisConv != null)
                {
                    exitRoute = thisConv.convPosition;
                }

                //if (threeWaySwitch.ControlType == ControlTypes.Local)
                //{
                //    if (straightThrough.convPosition == ThreeWayRoutes.Right)
                //    {
                //        if (threeWaySwitch.RightDefaultDirection == ThreeWayRoutes.Left)
                //        {
                //            load.Switch(threeWaySwitch.LeftConv.endAP);
                //        }
                //        else if (threeWaySwitch.RightDefaultDirection == ThreeWayRoutes.Center)
                //        {
                //            load.Switch(threeWaySwitch.CenterConv.endAP);
                //        }
                //    }
                //    else if (straightThrough.convPosition == ThreeWayRoutes.Left)
                //    {
                //        if (threeWaySwitch.LeftDefaultDirection == ThreeWayRoutes.Right)
                //        {
                //            load.Switch(threeWaySwitch.RightConv.endAP);
                //        }
                //        else if (threeWaySwitch.LeftDefaultDirection == ThreeWayRoutes.Center)
                //        {
                //            load.Switch(threeWaySwitch.CenterConv.endAP);
                //        }
                //    }
                //    else if (straightThrough.convPosition == ThreeWayRoutes.Center)
                //    {
                //        if (threeWaySwitch.CenterDefaultDirection == ThreeWayRoutes.Right)
                //        {
                //            load.Switch(threeWaySwitch.RightConv.endAP);
                //        }
                //        else if (threeWaySwitch.CenterDefaultDirection == ThreeWayRoutes.Left)
                //        {
                //            load.Switch(threeWaySwitch.LeftConv.endAP);
                //        }
                //    }
                //}
                //else if (threeWaySwitch.ControlType == ControlTypes.Controller)
                //{
                    if (straightThrough.convPosition == ThreeWayRoutes.Right)
                    {
                        if (exitRoute == ThreeWayRoutes.Left)
                        {
                            load.Switch(threeWaySwitch.LeftConv.endAP);
                        }
                        else if (exitRoute  == ThreeWayRoutes.Center)
                        {
                            load.Switch(threeWaySwitch.CenterConv.endAP);
                        }
                    }
                    else if (straightThrough.convPosition == ThreeWayRoutes.Left)
                    {
                        if (exitRoute == ThreeWayRoutes.Right)
                        {
                            load.Switch(threeWaySwitch.RightConv.endAP);
                        }
                        else if (exitRoute == ThreeWayRoutes.Center)
                        {
                            load.Switch(threeWaySwitch.CenterConv.endAP);
                        }
                    }
                    else if (straightThrough.convPosition == ThreeWayRoutes.Center)
                    {
                        if (exitRoute == ThreeWayRoutes.Right)
                        {
                            load.Switch(threeWaySwitch.RightConv.endAP);
                        }
                        else if (exitRoute == ThreeWayRoutes.Left)
                        {
                            load.Switch(threeWaySwitch.LeftConv.endAP);
                        }
                    }
                //}
            }

            void threeWaySwitch_OnDimensionsChanged(object sender, EventArgs e)
            {
                double numerator = this == straightThrough.crossOverX ? 2 : 1;
                double offset = this == straightThrough.crossOverX ? threeWaySwitch.CenterOffset : 0;
                double adj = threeWaySwitch.ThreeWayLength - 0.6f; // APs on the straight through convs are off set by 0.3 each end and this is constant

                if (straightThrough.convPosition == ThreeWayRoutes.Left)
                {
                    double opp = (threeWaySwitch.internalWidth / numerator) + offset;
                    double hyp = Math.Sqrt((opp * opp) + (adj * adj));
                    Length = (float)hyp;
                    Yaw = (float)Math.Asin(opp / hyp);
                    LocalPosition = new Vector3(0, 0, (float)opp / 2);
                }
                else if (straightThrough.convPosition == ThreeWayRoutes.Right)
                {
                    double opp = (threeWaySwitch.internalWidth / numerator) - offset;
                    double hyp = Math.Sqrt((opp * opp) + (adj * adj));
                    Length = (float)hyp;
                    Yaw = -(float)Math.Asin(opp / hyp);
                    LocalPosition = new Vector3(0, 0, -(float)opp / 2);
                }
                else if (straightThrough.convPosition == ThreeWayRoutes.Center)
                {
                    float side1 = this == straightThrough.crossOverX ? 1 : -1;
                    double opp = (threeWaySwitch.internalWidth / 2) + threeWaySwitch.CenterOffset * side1;
                    double hyp = Math.Sqrt((opp * opp) + (adj * adj));
                    Length = (float)hyp;
                    float side = this == straightThrough.crossOverX ? -1 : 1;
                    Yaw = side * (float)Math.Asin(opp / hyp);
                    LocalPosition = new Vector3(0, 0, side * (float)opp / 2);
                }
                endAP.Distance = Length;
            }
        }

        #endregion

    }

    [Serializable]
    [XmlInclude(typeof(ThreeWaySwitchInfo))]
    public class ThreeWaySwitchInfo : AssemblyInfo, IControllableInfo
    {
        public float centerOffset = 0;
        public float threeWayLength = 2;
        public float threeWayWidth = 1.5f;
        public float releaseDelayTimeStraight;
        public ThreeWayRoutes rightDefaultDirection;
        public ThreeWayRoutes leftDefaultDirection;
        public ThreeWayRoutes centerDefaultDirection;
        public ControlTypes controlType = ControlTypes.Local;
        public float speed = 1;
        public CaseConveyorWidth internalConvWidth = CaseConveyorWidth._500mm;

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

        #region Fields

        private static ThreeWaySwitchInfo properties = new ThreeWaySwitchInfo();

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
    }

    public enum ThreeWayRoutes { NA, Left, Right, Center };    

    public class ThreeWayArrivalArgs : EventArgs
    {
        public readonly ThreeWayRoutes _fromSide;
        public readonly Load _load;
        public ThreeWayRoutes _defaultDirection;

        public ThreeWayArrivalArgs(ThreeWayRoutes fromSide, Load load, ThreeWayRoutes defaultDirection)
        {
            _fromSide = fromSide;
            _load = load;
            _defaultDirection = defaultDirection;
        }
    }

    public class ThreeWayDivertedArgs : EventArgs
    {
        public readonly Load _load;
        public ThreeWayRoutes _divertedRoute;

        public ThreeWayDivertedArgs(ThreeWayRoutes divertedRoute, Load load)
        {
            _load = load;
            _divertedRoute = divertedRoute;
        }
    }

}