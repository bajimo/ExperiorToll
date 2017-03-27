using Experior.Catalog.Dematic.Pallet.Devices;
using Experior.Catalog.Devices;
using Experior.Catalog.Logistic.Track;
using Experior.Core.Assemblies;
using Experior.Core.Loads;
using Experior.Core.Parts;
using Experior.Core.Properties;
using Experior.Core.Routes;
using Experior.Dematic.Base;
using Experior.Dematic.Base.Devices;
using Experior.Dematic.Devices;
using Experior.Dematic.Pallet.Devices;
using Microsoft.DirectX;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Serialization;


namespace Experior.Catalog.Dematic.Pallet.Assemblies
{
    public class BaseStraight : Straight, IConstructDevice, IPalletRouteStatus
    {
        public const float ChainWidth = 0.05f;
        public BaseStraightInfo StraightInfo;
        public ActionPoint Entering, Leaving;
        public ConstructDevice ConstructDevice;

        public DematicArrow Arrow;
        public Cube StartLine1, EndLine1, StartLine2, EndLine2, ChainLeft, ChainRight;

        public IRouteStatus NextConveyor = null;                             //This is set in EndFixPoint_OnSnapped
        public IRouteStatus PreviousConveyor = null;                         //This is set in other assemblies StartFixPoint_OnSnapped

        private LoadWaitingStatus thisLoadWaiting = new LoadWaitingStatus(); //used as return objects for IRouteStatus methods
        private RouteStatus nextRouteStatus;                                //used as return objects for IRouteStatus methods
        private RouteStatus thisRouteStatus = new RouteStatus();            //Wrapped by property ThisRouteStatus and is the return type for GetAvailableStatus which is a method in IRouteStatus
        private LoadWaitingStatus previousLoadWaiting;                      //Wrapped by property PreviousLoadWaiting
        private bool loadWaiting = false;

        public event EventHandler OnDevicesCreated;
        public event EventHandler<EventArgs> OnSpeedUpdated;
        public event EventHandler<LoadArrivedEventArgs> OnLoadArrived;
        public event EventHandler<LoadArrivedEventArgs> OnLoadLeft;


        public virtual void LoadArrived(LoadArrivedEventArgs e)
        {
            EventHandler<LoadArrivedEventArgs> handler = OnLoadArrived;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        public virtual void LoadLeft(LoadArrivedEventArgs e)
        {
            EventHandler<LoadArrivedEventArgs> handler = OnLoadLeft;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        public BaseStraight(BaseStraightInfo info) : base(info)
        {
            try
            {
                StraightInfo = info;
                TransportSection.Route.DragDropLoad = false;
                Entering = TransportSection.Route.InsertActionPoint(0);
                Leaving = TransportSection.Route.InsertActionPoint(TransportSection.Route.Length);
                Entering.OnEnter += entering_OnEnter;
                // Leaving.OnEnter += leaving_OnEnter;
                Core.Environment.Scene.OnLoaded += Scene_OnLoaded;
                Experior.Dematic.Base.EuroPallet.PalletLoadDisposed += Pallet_Load_PalletLoadDisposed;

                Intersectable = false;

                if (TransportSection.Route.Arrow != null)
                {
                    TransportSection.Route.Arrow.Visible = false;
                }

                //Length does not update when dragging so disabled for now (Update on length is required to ensure that photocells are positioned correctly)
                StartFixPoint.Dragable = false;
                EndFixPoint.Dragable = false;

                Arrow = new DematicArrow(this, Width);

                if (StraightInfo.ConveyorType == PalletConveyorType.Chain)
                {
                    var chainLength = TransportSection.Route.Length;
                    var chainHeight = TransportSection.Height;
                    var offCentre = Width / 2 - ChainWidth;

                    StartLine1 = new Cube(Color.Black, ChainWidth + 0.005f, 0.055f, 0.004f);
                    Add(StartLine1);
                    StartLine1.LocalPosition = new Vector3(Length / 2 + 0.002f, 0, offCentre);
                    StartLine1.Yaw = (float)Math.PI / 2;

                    StartLine2 = new Cube(Color.Black, ChainWidth + 0.005f, 0.055f, 0.004f);
                    Add(StartLine2);
                    StartLine2.LocalPosition = new Vector3(Length / 2 + 0.002f, 0, -offCentre);
                    StartLine2.Yaw = (float)Math.PI / 2;

                    EndLine1 = new Cube(Color.Black, ChainWidth + 0.005f, 0.055f, 0.004f);
                    Add(EndLine1);
                    EndLine1.LocalPosition = new Vector3(-Length / 2 - 0.002f, 0, offCentre);
                    EndLine1.Yaw = (float)Math.PI / 2;

                    EndLine2 = new Cube(Color.Black, ChainWidth + 0.005f, 0.055f, 0.004f);
                    Add(EndLine2);
                    EndLine2.LocalPosition = new Vector3(-Length / 2 - 0.002f, 0, -offCentre);
                    EndLine2.Yaw = (float)Math.PI / 2;

                    ChainLeft = new Cube(Color.Yellow, chainLength, chainHeight, ChainWidth);
                    Add(ChainLeft);
                    ChainLeft.LocalPosition = new Vector3(0, 0, offCentre);

                    ChainRight = new Cube(Color.Yellow, chainLength, chainHeight, ChainWidth);
                    Add(ChainRight);
                    ChainRight.LocalPosition = new Vector3(0, 0, -offCentre);

                    TransportSection.Visible = false;
                }
                else
                {
                    StartLine1 = new Cube(Color.Black, Width + 0.005f, 0.055f, 0.004f);
                    Add(StartLine1);
                    StartLine1.LocalPosition = new Vector3(Length / 2 + 0.002f, 0, 0);
                    StartLine1.Yaw = (float)Math.PI / 2;

                    EndLine1 = new Cube(Color.Black, Width + 0.005f, 0.055f, 0.004f);
                    Add(EndLine1);
                    EndLine1.LocalPosition = new Vector3(-Length / 2 - 0.002f, 0, 0);
                    EndLine1.Yaw = (float)Math.PI / 2;
                }

                EndFixPoint.OnUnSnapped += EndFixPoint_OnUnSnapped;
                EndFixPoint.OnSnapped += EndFixPoint_OnSnapped;

                StartFixPoint.OnUnSnapped += StartFixPoint_OnUnSnapped;
                StartFixPoint.OnSnapped += StartFixPoint_OnSnapped;

                ThisRouteStatus.OnRouteStatusChanged += ThisRouteStatus_OnAvailableChanged;
                TransportSection.Route.Motor.OnDirectionChanged += Motor_OnDirectionChanged;

                ThisRouteStatus.Available = RouteStatuses.Available;
                TransportSection.Route.OnLoadRemoved += Route_OnLoadRemoved;
            }
            catch (Exception ex)
            {
                Core.Environment.Log.Write(ex.Message);
            }
        }

        private void Route_OnLoadRemoved(Core.Routes.Route sender, Load load)
        {
            if (load.StartDisposing)
            {
                if (ThisRouteStatus.Available == RouteStatuses.Blocked)
                {
                    ThisRouteStatus.Available = RouteStatuses.Available;
                }
                else
                {
                    ThisRouteStatus.Available = RouteStatuses.Blocked;
                    ThisRouteStatus.Available = RouteStatuses.Available;
                }
            }
        }

        [Browsable(false)]
        public bool ScriptRelease { get; set; }

        public override Image Image
        {
            get
            {
                return Common.Icons.Get("PalletStraight");
            }
        }
        void Motor_OnDirectionChanged(Core.Motors.Motor sender)
        {
            Arrow.Yaw = Arrow.Yaw + (float)Math.PI;
        }

        /// <summary>
        /// Set the motor status whenever this route status changes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public virtual void ThisRouteStatus_OnAvailableChanged(object sender, RouteStatusChangedEventArgs e)
        {
            if (e._available == RouteStatuses.Available)
                Arrow.Color = Color.Green;
            else if (e._available == RouteStatuses.Request)
                Arrow.Color = Color.Yellow;
            else
                Arrow.Color = Color.Red;

            if (e._available == RouteStatuses.Available || e._available == RouteStatuses.Request)
            {
                Motor = MotorStatus.Running;
            }
            else if (e._available == RouteStatuses.Blocked)
            {
                Motor = MotorStatus.Stopped;
            }
        }

        /// <summary>
        /// Motor is running if in request or available
        /// Request = Transferring Load Out (Running)
        /// Available = Transferring Load In (Running)
        /// Blocked = Cannot release to next conveyor (Stopped)
        /// </summary>
        private MotorStatus _Motor = MotorStatus.Stopped;
        [Browsable(false)]
        public MotorStatus Motor
        {
            get { return _Motor; }
            set
            {
                if (value == MotorStatus.Running)
                {
                    TransportSection.Route.Motor.Start();

                }
                else if (value == MotorStatus.Stopped)
                {
                    TransportSection.Route.Motor.Stop();
                }
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

        public enum MotorStatus
        {
            Running,
            Stopped
        }

        /// <summary>
        /// The standard straight conveyor does not care what is waiting to be relesaed into it, however if there is 
        /// routing control on the receiving conveyor then this should be overridden to get the Load Waiting status of
        /// the previous conveyor.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public virtual void StartFixPoint_OnSnapped(FixPoint stranger, FixPoint.SnapEventArgs e)
        {
            if (stranger.Parent is IRouteStatus)
            {
                PreviousConveyor = stranger.Parent as IRouteStatus;
            }
        }

        public virtual void StartFixPoint_OnUnSnapped(FixPoint stranger) { }

        public virtual void EndFixPoint_OnSnapped(Core.Parts.FixPoint stranger, Core.Parts.FixPoint.SnapEventArgs e)
        {
            NextConveyor = stranger.Parent as IRouteStatus;
            if (NextConveyor != null)
            {
                NextRouteStatus = NextConveyor.GetRouteStatus(stranger);
                if (NextRouteStatus != null)
                {
                    NextRouteStatus.OnRouteStatusChanged += NextRouteStatus_OnAvailableChanged;
                }
            }
        }

        public virtual void EndFixPoint_OnUnSnapped(Core.Parts.FixPoint stranger)
        {
            if (stranger.Parent is BaseTrack)
            {
                NextConveyor = null;
                if (NextRouteStatus != null)
                {
                    NextRouteStatus.OnRouteStatusChanged -= NextRouteStatus_OnAvailableChanged;
                    NextRouteStatus = null;
                }
            }
        }

        public virtual void NextRouteStatus_OnAvailableChanged(object sender, RouteStatusChangedEventArgs e)
        {
            if (e._available == RouteStatuses.Available && ThisRouteStatus.Available == RouteStatuses.Blocked)
            {
                ThisRouteStatus.Available = RouteStatuses.Request;
            }
            else if (e._available == RouteStatuses.Blocked && ThisRouteStatus.Available != RouteStatuses.Blocked)
            {
                ThisRouteStatus.Available = RouteStatuses.Available;
            }
        }

        public virtual RouteStatus GetRouteStatus(FixPoint startFixPoint)
        {
            return ThisRouteStatus;
        }

        public virtual LoadWaitingStatus GetLoadWaitingStatus(FixPoint endFixPoint)
        {
            return thisLoadWaiting;
        }

        [DisplayName("Load Count")]
        [Category("Status")]
        [Description("Number of loads on this transport section")]
        public int LoadCount
        {
            get { return TransportSection.Route.Loads.Count; }
        }

        /// <summary>
        ///Conveyor needs to be created before the commpoint creator is made as it contains a reference to the conveyor.
        ///Also called from ShowContextMenu
        /// </summary>
        public virtual void Scene_OnLoaded()
        {
            if (TransportSection.Route.Arrow != null)
            {
                TransportSection.Route.Arrow.Visible = false;
            }
            try
            {
                ConstructDevice = new ConstructDevice(Name);
                ConstructDevice.InsertDevices(this);
            }
            catch (Exception ex)
            {
                Experior.Core.Environment.Log.Write(ex.Message);
            }

            if (OnDevicesCreated != null)
            {
                OnDevicesCreated(this, new EventArgs());
            }
        }

        public override List<System.Windows.Forms.ToolStripItem> ShowContextMenu()
        {
            if (ConstructDevice == null ||
                ConstructDevice != null && ConstructDevice.conveyor == null)
            {
                ConstructDevice = new ConstructDevice(Name);
            }
            return new List<ToolStripItem>(ConstructDevice.subMnu);
        }

        void entering_OnEnter(ActionPoint sender, Load load)
        {
            OnConveyorEnterLoad(new ConveyorEnterLoadEventArgs(load));
        }

        #region Doubleclick feeding

        public override void DoubleClick()
        {
            if (InvokeRequired)
            {
                Core.Environment.Invoke(() => DoubleClick());
                return;
            }
            OnConveyorEnterLoad(new ConveyorEnterLoadEventArgs(FeedLoad.FeedEuroPallet(TransportSection, 0, Experior.Dematic.Base.EuroPallet.GetPalletControllerPalletData(), StraightInfo.PalletStatus)));
        }

        #endregion

        #region Assembly methods Reset, Dispose

        public override void Reset()
        {
            base.Reset();
            ThisRouteStatus.Available = RouteStatuses.Available;
        }

        public override string Category
        {
            get { return "Pallet Straight"; }
        }

        public override void Dispose()
        {
            Core.Environment.Scene.OnLoaded -= Scene_OnLoaded;
            ConstructDevice = null;
            if (Leaving != null)
                Leaving.Dispose();
            if (Entering != null)
                Entering.Dispose();

            Arrow.Dispose();

            if (Assemblies != null)
            {
                foreach (Assembly assembly in this.Assemblies)
                {
                    assembly.Dispose();
                }
            }

            base.Dispose();
        }

        public void Pallet_Load_PalletLoadDisposed(Experior.Dematic.Base.EuroPallet Palletload)
        {
            if (TransportSection.Route.Loads.Contains(Palletload))
            {
                //     OnConveyorExitLoad(new ConveyorExitLoadEventArgs(Palletload as Load));
            }
        }

        public event EventHandler<ConveyorEnterLoadEventArgs> ConveyorEnterLoad;
        protected virtual void OnConveyorEnterLoad(ConveyorEnterLoadEventArgs e)
        {
            if (ConveyorEnterLoad != null) ConveyorEnterLoad(this, e);
        }

        #endregion

        #region IConstructDevice

        public event EventHandler<SizeUpdateEventArgs> OnSizeUpdated;
        
        [Browsable(false)]
        public List<DeviceInfo> DeviceInfos
        {
            get { return StraightInfo.DeviceInfos; }
        }

        public void removeAssembly(Assembly assembly)
        {
            RemoveAssembly(assembly);
            Core.Environment.SolutionExplorer.Update(this);
        }

        public void addAssembly(Assembly assembly, Vector3 localPosition)
        {
            Add(assembly, localPosition);
            Core.Environment.SolutionExplorer.Update(this);
        }
        
        /// <summary>
        /// Check if assembly already exists on conveyor
        /// </summary>
        /// <param name="assemblyName"></param>
        /// <returns></returns>
        public bool containsAssembly(string assemblyName)
        {
            if (Assemblies != null)
            {
                return Assemblies.Any(i => i.Name == assemblyName);
            }
            return false;
        }
        
        #endregion

        #region User Interface

        #region User Interface Size and Speed

        [TypeConverter]
        [Browsable(false)]
        public override float Width
        {
            get
            {
                return base.Width;
            }
            set
            {
                base.Width = value;
                Arrow.Width = value / 2;

                if (InvokeRequired)
                {
                    Invoke((Action)(() => UpdateWidth()));
                }
                else
                {
                    UpdateWidth();
                }
                Core.Environment.Invoke(() => UpdateConveyor());
            }
        }

        public virtual void UpdateWidth()
        {
            if (OnSizeUpdated != null)
            {
                OnSizeUpdated(this, new SizeUpdateEventArgs(null, Width, null));
            }

            var offCentre = Width / 2 - ChainWidth;
            if (StraightInfo.ConveyorType == PalletConveyorType.Chain)
            {
                ChainRight.LocalPosition = new Vector3(0, 0, offCentre);
                StartLine1.LocalPosition = new Vector3(Length / 2 + 0.002f, 0, offCentre);
                StartLine2.LocalPosition = new Vector3(Length / 2 + 0.002f, 0, -offCentre);
                ChainLeft.LocalPosition = new Vector3(0, 0, -offCentre);
                EndLine1.LocalPosition = new Vector3(-Length / 2 + 0.002f, 0, offCentre);
                EndLine2.LocalPosition = new Vector3(-Length / 2 + 0.002f, 0, -offCentre);
            }
            else
            {
                StartLine1.Length = Width + 0.005f;
                EndLine1.Length = Width + 0.005f;
            }
        }

        [Category("Size and Speed")]
        [DisplayName("Conveyor Type")]
        [Description("The conveyor type")]
        [PropertyOrder(1)]
        [ReadOnly(true)]
        public virtual PalletConveyorType ConveyorType
        {
            get { return StraightInfo.ConveyorType; }
            set
            {
                StraightInfo.ConveyorType = value;
            }
        }

        [Category("Size and Speed")]
        [DisplayName("Roller Conveyor Width")]
        [Description("Width of the conveyor based on standard Dematic Pallet conveyor widths")]
        [PropertyOrder(2)]
        [PropertyAttributesProvider("DynamicPropertyRollerConveyorWidth")]
        public virtual RollerConveyorWidth RollerConveyorWidth
        {
            get { return StraightInfo.RollerConveyorWidth; }
            set
            {
                StraightInfo.RollerConveyorWidth = value;
                if (value != RollerConveyorWidth._Custom)
                {
                    Width = (float)value / 1000;
                }
                else
                {
                    Width = CustomRollerConveyorWidth;
                }
            }
        }

        public void DynamicPropertyRollerConveyorWidth(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = ConveyorType == PalletConveyorType.Roller;
        }

        [Category("Size and Speed")]
        [DisplayName("Custom Roller Conveyor Width")]
        [Description("Custom conveyor width")]
        [PropertyOrder(3)]
        [PropertyAttributesProvider("DynamicPropertyCustomRollerConveyorWidth")]
        public virtual float CustomRollerConveyorWidth
        {
            get { return StraightInfo.CustomRollerConveyorWidth; }
            set
            {
                StraightInfo.CustomRollerConveyorWidth = value;
                Width = value;
            }
        }

        public void DynamicPropertyCustomRollerConveyorWidth(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = ConveyorType == PalletConveyorType.Roller
                                        && RollerConveyorWidth == RollerConveyorWidth._Custom;
        }

        [Category("Size and Speed")]
        [DisplayName("Chain Conveyor Width")]
        [Description("Width of the conveyor based on standard Dematic Pallet conveyor widths")]
        [PropertyOrder(2)]
        [PropertyAttributesProvider("DynamicPropertyChainConveyorWidth")]
        public virtual ChainConveyorWidth ChainConveyorWidth
        {
            get { return StraightInfo.ChainConveyorWidth; }
            set
            {
                StraightInfo.ChainConveyorWidth = value;
                if (value != ChainConveyorWidth._Custom)
                {
                    Width = (float)value / 1000;
                }
                else
                {
                    Width = CustomRollerConveyorWidth;
                }
                // Reposition chains for new width
                var offCentre = Width / 2 - ChainWidth;
                ChainLeft.LocalPosition = new Vector3(0, 0, offCentre);
                ChainRight.LocalPosition = new Vector3(0, 0, -offCentre);
            }
        }

        public void DynamicPropertyChainConveyorWidth(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = ConveyorType == PalletConveyorType.Chain;
        }

        [Category("Size and Speed")]
        [DisplayName("Custom Chain Conveyor Width")]
        [Description("Custom conveyor width")]
        [PropertyOrder(3)]
        [PropertyAttributesProvider("DynamicPropertyCustomChainConveyorWidth")]
        public virtual float CustomChainConveyorWidth
        {
            get { return StraightInfo.CustomChainConveyorWidth; }
            set
            {
                StraightInfo.CustomChainConveyorWidth = value;
                Width = value;
            }
        }

        public void DynamicPropertyCustomChainConveyorWidth(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = ConveyorType == PalletConveyorType.Chain
                                        && ChainConveyorWidth == ChainConveyorWidth._Custom;
        }


        [PropertyOrder(4)]
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

                if (OnSpeedUpdated != null)
                {
                    OnSpeedUpdated(this, new EventArgs());
                }
            }
        }

        [Category("Size and Speed")]
        [DisplayName("Length")]
        [Description("Length of the conveyor (meter)")]
        [PropertyOrder(1)]
        [TypeConverter]
        public override float Length
        {
            get
            {
                return base.Length;
            }
            set
            {
                base.Length = value;
                if (InvokeRequired)
                {
                    Invoke((Action)(() => UpdateLength()));
                }
                else
                {
                    UpdateLength();
                }
                UpdateConveyor();
            }
        }

        #endregion

        #region User Interface Status

        [Category("Status")]
        [DisplayName("Load Waiting")]
        [Description("Is there a load waiting to release into the next conveyor")]
        [ReadOnly(true)]
        public bool LoadWaiting
        {
            get { return loadWaiting; }
            set { loadWaiting = value; }
        }

        /// <summary>
        /// Use this to change the route status of the current conveyor
        /// A load will only transfer on when the route status is available
        /// </summary>
        [Category("Status")]
        [DisplayName("Available")]
        [Description("Is this conveyor route available to be released into")]
        [ReadOnly(true)]
        public virtual RouteStatuses RouteAvailable
        {
            get { return ThisRouteStatus.Available; }
            set
            {
                if (ThisRouteStatus != null)
                    ThisRouteStatus.Available = value;

                SetRouteAvailable(value);
            }
        }

        /// <summary>
        /// Use this to change the route status of the current conveyor
        /// A load will only transfer on when the route status is available
        /// </summary>
        [Category("Status")]
        [DisplayName("Pallet Status")]
        [Description("Status of loads created on this conveyor")]
        public virtual PalletStatus PalletStatus
        {
            get { return StraightInfo.PalletStatus; }
            set { StraightInfo.PalletStatus = value; }
        }

        public void SetRouteAvailable(RouteStatuses value)
        {
            if (value == RouteStatuses.Available)
            {
                TransportSection.Route.Motor.Start();
            }
            else
            {
                TransportSection.Route.Motor.Stop();
            }

            if (ThisRouteStatus != null)
                ThisRouteStatus.Available = value;
        }

        #endregion

        #region User Interface Position

        [Category("Position")]
        [DisplayName("Height")]
        [Description("Height of the conveyor (meter)")]
        [TypeConverter]
        public override float Height
        {
            get
            {
                return base.Position.Y;
            }
            set
            {
                base.Position = new Microsoft.DirectX.Vector3(base.Position.X, value, base.Position.Z);
                Core.Environment.Properties.Refresh();
            }
        }

        #endregion

        public virtual void UpdateLength()
        {
            Core.Environment.SolutionExplorer.Refresh();
            if (OnSizeUpdated != null)
            {
                OnSizeUpdated(this, new SizeUpdateEventArgs(Length, null, null));
            }
            Leaving.Distance = Length;
            if (StraightInfo.ConveyorType == PalletConveyorType.Chain)
            {
                var offCentre = Width / 2 - ChainWidth;
                ChainLeft.Length = Length;
                ChainRight.Length = Length;
                EndLine1.LocalPosition = new Vector3(Length / 2 - 0.002f, 0, offCentre);
                StartLine1.LocalPosition = new Vector3(Length / 2 + 0.002f, 0, -offCentre);
                EndLine2.LocalPosition = new Vector3(-Length / 2 - 0.002f, 0, offCentre);
                StartLine2.LocalPosition = new Vector3(-Length / 2 + 0.002f, 0, -offCentre);
            }
            else
            {
                EndLine1.LocalPosition = new Vector3(-Length / 2 - 0.002f, 0, 0);
                StartLine1.LocalPosition = new Vector3(Length / 2 + 0.002f, 0, 0);
            }
        }

        public override Vector3 PositionEnd
        {
            get
            {
                return base.PositionEnd;
            }
            set
            {
                base.PositionEnd = value;
                UpdateLength();
            }
        }

        public override Vector3 PositionStart
        {
            get
            {
                return base.PositionStart;
            }
            set
            {
                base.PositionStart = value;
                UpdateLength();
            }
        }

        public virtual void NextAvailableChanged() { }

        public void SetLoadWaiting(bool loadWaiting, bool loadDeleted, Load waitingLoad)
        {
            LoadWaiting = loadWaiting;
            thisLoadWaiting.SetLoadWaiting(loadWaiting, loadDeleted, waitingLoad);
        }

        [Browsable(false)]
        public RouteStatus ThisRouteStatus
        {
            get { return thisRouteStatus; }
            set { thisRouteStatus = value; }
        }

        [Browsable(false)]
        public RouteStatus NextRouteStatus
        {
            get { return nextRouteStatus; }
            set { nextRouteStatus = value; }
        }

        [Browsable(false)]
        public LoadWaitingStatus PreviousLoadWaiting
        {
            get { return previousLoadWaiting; }
            set { previousLoadWaiting = value; }
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

        public virtual void UpdateConveyor() { }
        #endregion

    }

    [Serializable]
    [XmlInclude(typeof(BaseStraightInfo))]
    public class BaseStraightInfo : StraightInfo
    {
        public List<DeviceInfo> DeviceInfos = new List<DeviceInfo>();
        public float StartOffset = 0;  // fix point position in the Z axis
        public float EndOffset = 0;    // fix point position in the Z axis
        public PalletConveyorType ConveyorType = PalletConveyorType.Chain;
        public RollerConveyorWidth RollerConveyorWidth = RollerConveyorWidth._978mm;
        public ChainConveyorWidth ChainConveyorWidth = ChainConveyorWidth._1260mm;
        public float CustomRollerConveyorWidth = 1.0f;
        public float CustomChainConveyorWidth = 1.0f;
        public PalletStatus PalletStatus = PalletStatus.Loaded;
    }

}
