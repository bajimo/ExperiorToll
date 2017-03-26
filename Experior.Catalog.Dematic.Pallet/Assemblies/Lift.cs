using Experior.Core.Assemblies;
using Experior.Core.Loads;
using Experior.Core.Properties;
using Experior.Core.Routes;
using Experior.Core.TransportSections;
using Experior.Dematic.Base;
using Experior.Dematic.Base.Devices;
using Microsoft.DirectX;
using System;
using System.Linq;
using System.ComponentModel;
using System.Drawing;
using System.Xml.Serialization;

namespace Experior.Catalog.Dematic.Pallet.Assemblies
{
    public class Lift : Assembly, IControllable
    {
        private LiftInfo liftInfo;
        private StraightTransportSection lift;
        private readonly Load liftLoad;
        private readonly ActionPoint liftStopPoint;
        private static Experior.Core.Timer delayTimer = new Experior.Core.Timer(2);

        public LiftStraight LiftConveyor;
        public delegate void OnLiftRaisedEvent(Lift sender, Load load);
        public OnLiftRaisedEvent OnLiftRaised;                           // This event can be subscribed to in the controller
        public static event OnLiftRaisedEvent OnLiftRaisedStatic;        // This is the event that can be subscribed to in the routing script

        public Lift(LiftInfo info) : base(info)
        {
            liftInfo = info;
            info.height = info.height * 2; // assembley is placed at height/2 so that it comes out at height ?!!??

            // Rail
            lift = new StraightTransportSection(Color.Gray, LiftHeight, 0.1f, 0.02f);
            Add(lift);
            lift.Route.Motor.Speed = 1; // m/s ?
            lift.LocalRoll = -(float)Math.PI / 2.0f;

            // Load Vehicle
            liftLoad = Core.Loads.Load.CreateBox(0.1f, 0.1f, 0.1f, Color.Red);
            liftLoad.Embedded = true;
            liftLoad.Deletable = false;
            lift.Route.Add(liftLoad);
            liftLoad.OnPositionChanged += Liftload_OnPositionChanged;
            liftLoad.Stop();
            liftLoad.Visible = false;

            // Action point for lift rail
            liftStopPoint = lift.Route.InsertActionPoint(LiftHeight);
            liftStopPoint.Visible = false;
            liftStopPoint.OnEnter += LiftStopPoint_OnEnter;

            // Conveyor
            LiftStraightInfo straightInfo = new LiftStraightInfo
            {
                ConveyorType = PalletConveyorType.Roller,
                thickness = 0.05f,
                spacing = 0.1f,
                width = liftInfo.ConveyorWidth,
                length = liftInfo.ConveyorLength,
                height = 0.7f,
                speed = 0.3f,
                color = liftInfo.color,
            };
            LiftConveyor = new LiftStraight(straightInfo);
            LiftConveyor.LineReleasePhotocell.OnPhotocellStatusChanged += LineReleasePhotocell_OnPhotocellStatusChanged;
            LiftConveyor.EndFixPoint.OnSnapped += EndFixPoint_OnSnapped;
            Add(LiftConveyor);

            var zposition = LiftConveyor.Width / 2.0f + 0.02f;
            lift.LocalPosition = new Vector3(0, LiftHeight / 2.0f, zposition);

            Reset();
        }

        private void EndFixPoint_OnSnapped(Core.Parts.FixPoint stranger, Core.Parts.FixPoint.SnapEventArgs e)
        {
            LiftConveyor.NextRouteStatus.OnRouteStatusChanged += NextRouteStatus_OnRouteStatusChanged;
        }

        private void NextRouteStatus_OnRouteStatusChanged(object sender, RouteStatusChangedEventArgs e)
        {
            var distanceRaised = liftLoad.Distance;
            var isLowered = distanceRaised.WithinRange(0.0f, 0.01f);
            if (isLowered
                && (LiftConveyor.NextConveyor != null && LiftConveyor.NextConveyor.LoadCount > 0) 
                && e._available == RouteStatuses.Available)
            {
                LiftConveyor.ThisRouteStatus.Available = RouteStatuses.Request;
            }
            if (LiftConveyor.LoadCount > 0 
                && !isLowered
                && e._available == RouteStatuses.Available)
            {
                LiftConveyor.ThisRouteStatus.Available = RouteStatuses.Blocked;
            }
        }

        private void Liftload_OnPositionChanged(Load load, Vector3 position)
        {
            // Simple way to make the platform move as the 'liftload' moves (we use the liftload to move the platform)
            LiftConveyor.LocalPosition = new Vector3(0, load.Distance, 0);
        }

        /// <summary>
        /// Raised when the platform has reached the desired level
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="load"></param>
        private void LiftStopPoint_OnEnter(ActionPoint sender, Load load)
        {
            lift.Route.Motor.Stop();

            if (Raised)
            {
                if (ControlType == ControlTypes.Local)
                {
                    // Add delay then lower the lift
                    delayTimer.OnElapsed += LowerLift_OnElapsed;
                    delayTimer.Start();
                }
                else if (ControlType == ControlTypes.Project_Script)
                {
                    OnLiftRaisedStatic?.Invoke(this, load);
                }
                else if (ControlType == ControlTypes.Controller)
                {
                    if (Controller != null)
                    {
                        OnLiftRaised?.Invoke(this, load);
                    }
                }
            }
            else
            {
                Experior.Dematic.Base.EuroPallet pallet = GetLiftLoad(LiftConveyor);
                if (pallet != null && !pallet.LoadWaitingForWCS)
                {
                    LiftConveyor.SetLoadWaiting(true, false, null);
                    if (LiftConveyor.NextRouteStatus.Available == RouteStatuses.Available 
                        && LiftConveyor.ThisRouteStatus.Available == RouteStatuses.Blocked)
                    {
                        LiftConveyor.ThisRouteStatus.Available = RouteStatuses.Request;
                    }
                }
            }
        }

        private Experior.Dematic.Base.EuroPallet GetLiftLoad(PalletStraight straight)
        {
            Experior.Dematic.Base.EuroPallet palletLoad = null;
            if (straight.TransportSection.Route.Loads.Count > 0)
            {
                palletLoad = (Experior.Dematic.Base.EuroPallet)straight.TransportSection.Route.Loads.First();
            }
            return palletLoad;
        }

        void LowerLift_OnElapsed(Experior.Core.Timer sender)
        {
            delayTimer.OnElapsed -= LowerLift_OnElapsed;
            LowerLift();
        }

        void LineReleasePhotocell_OnPhotocellStatusChanged(object sender, PhotocellStatusChangedEventArgs e)
        {
            if (e._PhotocellStatus == PhotocellState.Blocked)
            {
                e._Load.UserDeletable = false;
                LiftConveyor.ThisRouteStatus.Available = RouteStatuses.Blocked;

                lift.Route.Motor.Stop();
                if (!Raised)
                {
                    RaiseLift();
                }
            }
            else if (e._PhotocellStatus == PhotocellState.Clear)
            {
                if (e._Load != null)
                {
                    e._Load.UserDeletable = true;
                }
                LiftConveyor.SetLoadWaiting(false, false, e._Load);

                //If the load was deleted then, it does not need to wait to transfer to the next conveyor
                if (e._LoadDeleted)
                {
                    LiftConveyor.ThisRouteStatus.Available = RouteStatuses.Available;
                }
            }
        }

        private void RaiseLift()
        {
            if (Running || Raised) return;

            liftStopPoint.Distance = LiftHeight;

            lift.Route.Motor.Forward();
            lift.Route.Motor.Start();

            liftLoad.Release();
        }

        public void LowerLift()
        {
            if (Running || !Raised) return;

            liftStopPoint.Distance = 0;

            lift.Route.Motor.Backward();
            lift.Route.Motor.Start();

            liftLoad.Release();
        }

        private void SetupConveyor()
        {
            LiftConveyor.Width = liftInfo.ConveyorWidth;
            LiftConveyor.Length = liftInfo.ConveyorLength;
            var zposition = LiftConveyor.Width / 2.0f + 0.02f;
            LiftConveyor.LocalPosition = new Vector3(0, 0, 0);
            lift.LocalPosition = new Vector3(0, LiftHeight / 2.0f, zposition);
        }

        public override void Reset()
        {
            base.Reset();
            delayTimer.Reset();
            lift.Route.Motor.Forward();
            lift.Route.Motor.Stop();
            liftStopPoint.Distance = LiftHeight;
            liftLoad.Distance = 0;
            LiftConveyor.Reset();
            LiftConveyor.LocalPosition = new Vector3(0, 0, 0); // Move the platform to the default position (instant, not using timers)
            LiftConveyor.TransportSection.Route.ClearLoads();
            LiftConveyor.ThisRouteStatus.Available = RouteStatuses.Available;
        }

        public override void Dispose()
        {
            liftLoad.Dispose();
            delayTimer.Dispose();
            if (LiftConveyor != null)
                LiftConveyor.Dispose();
            if (liftStopPoint != null)
                liftStopPoint.Dispose();
            if (liftLoad != null)
                liftLoad.Dispose();
            if (lift != null)
                lift.Dispose();
            if (Assemblies != null)
            {
                foreach (Assembly assembly in this.Assemblies)
                {
                    assembly.Dispose();
                }
            }
            base.Dispose();
        }


        #region Properties

        [Browsable(true)]
        public override Color Color
        {
            get { return base.Color; }
            set
            {
                base.Color = value;
                LiftConveyor.Color = value;
            }
        }

        public bool Running
        {
            get { return lift.Route.Motor.Running; }
        }

        public bool Raised
        {
            get { return Math.Abs(liftLoad.Distance) > 0.0005; }
        }

        [Category("Lift Configuration"), DisplayName(@"Lift height"), PropertyOrder(1), TypeConverter]
        public float LiftHeight
        {
            get { return liftInfo.LiftHeight; }
            set
            {
                if (value >= 0.01f && !liftInfo.LiftHeight.Equals(value))
                {
                    lift.Length = value;
                    liftInfo.LiftHeight = value;
                }
                lift.LocalPosition = new Vector3(0, LiftHeight / 2.0f, LiftConveyor.Width / 2.0f + 0.02f);
            }
        }

        [Category("Lift Configuration")]
        [DisplayName("Conveyor Length")]
        [Description("Length of the conveyor based on standard Dematic merge divert lengths")]
        [PropertyOrder(2)]
        public float ConveyorLength
        {
            get { return liftInfo.ConveyorLength; }
            set
            {
                liftInfo.ConveyorLength = value;
                Core.Environment.Invoke(() => SetupConveyor());
            }
        }

        [Category("Lift Configuration")]
        [DisplayName("Conveyor Width")]
        [Description("Width of the conveyor based on standard Dematic conveyor widths")]
        [PropertyOrder(3)]
        public float ConveyorWidth
        {
            get { return liftInfo.ConveyorWidth; }
            set
            {
                liftInfo.ConveyorWidth = value;
                Core.Environment.Invoke(() => SetupConveyor());
            }
        }

        [Category("Position")]
        [DisplayName("Height")]
        [Description("Height of the lift (meter)")]
        [TypeConverter]
        public float Height
        {
            get
            {
                return Position.Y;
            }
            set
            {
                Position = new Microsoft.DirectX.Vector3(Position.X, value, Position.Z);
                Core.Environment.Properties.Refresh();
            }
        }

        #endregion

        #region Implement Assembly Properties

        public override string Category
        {
            get { return "Lift"; }
        }

        public override Image Image
        {
            get { return Common.Icons.Get("Lift"); }
        }

        #endregion

        #region Implement IControllable

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

        public void controller_OnControllerDeletedEvent(object sender, EventArgs e)
        {
            controller.OnControllerDeletedEvent -= controller_OnControllerDeletedEvent;
            ControllerName = "No Controller";
            //ControllerName = null;
            Controller = null;
            ControllerProperties = null;
            liftInfo.ProtocolInfo = null;
        }

        void controller_OnControllerRenamedEvent(object sender, EventArgs e)
        {
            liftInfo.ControllerName = ((Assembly)sender).Name;
            Core.Environment.Properties.Refresh();
        }

        [Category("Routing")]
        [DisplayName("Control")]
        [Description("Embedded routing control with protocol and routing specific configuration")]
        [PropertyOrder(3)]
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
        [DisplayName("Control Type")]
        [Description("Defines if the control is handled by a controller, by a routing script or uses only local control. ")]
        [PropertyOrder(1)]
        public ControlTypes ControlType
        {
            get
            {
                return liftInfo.ControlType;
            }
            set
            {
                liftInfo.ControlType = value;
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
                return liftInfo.ControllerName;
            }
            set
            {
                if (!value.Equals(liftInfo.ControllerName))
                {
                    ControllerProperties = null;
                    liftInfo.ProtocolInfo = null;
                    Controller = null;
                }

                liftInfo.ControllerName = value;
                if (value != null)
                {
                    //Note StandardCase.SetMHEControl will set the ControllerProperties and Controller properties!
                    ControllerProperties = StandardCase.SetMHEControl(liftInfo, this);
                    if (ControllerProperties == null)
                    {
                        liftInfo.ControllerName = "No Controller";
                    }
                }
            }
        }

        public void DynamicPropertyControllers(Core.Properties.PropertyAttributes attributes)
        {
            attributes.IsBrowsable = liftInfo.ControlType == ControlTypes.Controller;
        }

        public void DynamicPropertyAssemblyPLCconfig(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = Controller != null;
        }

        #endregion
    }

    [Serializable]
    [XmlInclude(typeof(LiftInfo))]
    public class LiftInfo : AssemblyInfo, IControllableInfo
    {
        public float LiftHeight;
        public float ConveyorWidth = 0.972f;
        public float ConveyorLength = 1.4f;
        public ControlTypes ControlType;

        #region Implement IControllableInfo

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
}
