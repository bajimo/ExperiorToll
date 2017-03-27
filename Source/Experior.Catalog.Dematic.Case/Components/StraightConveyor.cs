using Experior.Catalog.Dematic.Case.Devices;
using Experior.Catalog.Logistic.Track;
using Experior.Core.Assemblies;
using Experior.Core.Loads;
using Experior.Core.Motors;
using Experior.Core.Parts;
//using Experior.Core.Primitives;
using Experior.Core.Properties;
using Experior.Core.Routes;
using Experior.Dematic.Base;
using Experior.Dematic.Base.Devices;
using Experior.Dematic.Case.Devices;
using Experior.Dematic.Devices;
using Microsoft.DirectX;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Serialization;


namespace Experior.Catalog.Dematic.Case.Components
{
    public class StraightConveyor : Straight, IConstructDevice, IRouteStatus
    {
        public StraightConveyorInfo straightinfo;
        public ActionPoint Entering, Leaving;
        public ConstructDevice constructDevice;

        public DematicArrow arrow;
        public Experior.Core.Parts.Cube startLine, endLine;
        
        private LoadWaitingStatus ThisLoadWaiting = new LoadWaitingStatus(); //used as return objects for IRouteStatus methods
        private RouteStatus _NextRouteStatus;                                //used as return objects for IRouteStatus methods

        public IRouteStatus NextConveyor = null;                             //This is set in EndFixPoint_OnSnapped
        public IRouteStatus PreviousConveyor = null;                         //This is set in other assemblies StartFixPoint_OnSnapped

        private RouteStatus _ThisRouteStatus = new RouteStatus();            //Wrapped by property ThisRouteStatus and is the return type for GetAvailableStatus which is a method in IRouteStatus
        private LoadWaitingStatus _PreviousLoadWaiting;                      //Wrapped by property PreviousLoadWaiting
        protected RouteStatuses _RouteAvailable = RouteStatuses.Available;
        private bool _LoadWaiting = false;

        public event EventHandler OnDevicesCreated;
        public event EventHandler<EventArgs> OnSpeedUpdated;


        public StraightConveyor(StraightConveyorInfo info): base(info)
        {
            try
            {
                straightinfo = info;
                TransportSection.Route.DragDropLoad = false;
                Entering = TransportSection.Route.InsertActionPoint(0);
                Leaving = TransportSection.Route.InsertActionPoint(TransportSection.Route.Length);
                Entering.OnEnter += entering_OnEnter;
               // Leaving.OnEnter += leaving_OnEnter;
                Core.Environment.Scene.OnLoaded += Scene_OnLoaded;
                
                Intersectable = false;

                if (TransportSection.Route.Arrow != null)
                {
                    TransportSection.Route.Arrow.Visible = false;
                }

                //Length does not update when dragging so disabled for now (Update on length is required to ensure that photocells are positioned correctly)
                StartFixPoint.Dragable = false;
                EndFixPoint.Dragable = false;

                arrow = new DematicArrow(this,Width);

                startLine = new Cube(Color.Black, Width + 0.005f, 0.055f, 0.004f);
                Add(startLine);
                startLine.LocalPosition = new Vector3(Length / 2 + 0.002f, 0, 0);
                startLine.Yaw = (float)Math.PI / 2;

                endLine = new Cube(Color.Black, Width + 0.005f, 0.055f, 0.004f);
                Add(endLine);
                endLine.LocalPosition = new Vector3(-Length / 2 - 0.002f, 0, 0);
                endLine.Yaw = (float)Math.PI / 2;

                EndFixPoint.OnUnSnapped += EndFixPoint_OnUnSnapped;
                EndFixPoint.OnSnapped += EndFixPoint_OnSnapped;

                StartFixPoint.OnUnSnapped += StartFixPoint_OnUnSnapped;
                StartFixPoint.OnSnapped += StartFixPoint_OnSnapped;

                ThisRouteStatus.OnRouteStatusChanged += ThisRouteStatus_OnAvailableChanged;
                TransportSection.Route.Motor.OnDirectionChanged += Motor_OnDirectionChanged;
            }
            catch (Exception ex)
            {
                Core.Environment.Log.Write(ex.Message);
            }
        }

        public override Image Image
        {
            get
            {
                return Common.Icons.Get("RaRoller");
            }
        }

        void Motor_OnDirectionChanged(Core.Motors.Motor sender)
        {
            arrow.Yaw = arrow.Yaw + (float)Math.PI;
        }

        public virtual void ThisRouteStatus_OnAvailableChanged(object sender, RouteStatusChangedEventArgs e)
        {
            if (e._available == RouteStatuses.Available)
                arrow.Color = Color.Green;
            else if (e._available == RouteStatuses.Request)
                arrow.Color = Color.Yellow;
            else
                arrow.Color = Color.Red;
        }

        /// <summary>
        /// The standard straight conveyor does not care what is waiting to be released into it, however if there is 
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

        public event EventHandler<RouteStatusChangedEventArgs> OnNextRouteStatusAvailableChanged;
        public virtual void NextRouteStatus_OnAvailableChanged(object sender, RouteStatusChangedEventArgs e) 
        {
            if (OnNextRouteStatusAvailableChanged != null)
            {
                OnNextRouteStatusAvailableChanged(sender, e);
            }
        }

        public virtual RouteStatus GetRouteStatus(FixPoint startFixPoint)
        {
            return ThisRouteStatus;
        }

        public virtual LoadWaitingStatus GetLoadWaitingStatus(FixPoint endFixPoint)
        {
            return ThisLoadWaiting;
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
                constructDevice = new ConstructDevice(Name);
                constructDevice.InsertDevices(this);
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
            if (constructDevice == null)
            {
                constructDevice = new ConstructDevice(Name);
            }
            return new List<ToolStripItem>(constructDevice.subMnu);
        }

        void entering_OnEnter(ActionPoint sender, Load load)
        {
            OnConveyorEnterLoad(new ConveyorEnterLoadEventArgs(load));
        }

        //void leaving_OnEnter(ActionPoint sender, Load load)
        //{
        //    OnConveyorExitLoad(new ConveyorExitLoadEventArgs(load));
        //}

        #region Doubleclick feeding

        public override void DoubleClick()
        {
            if (InvokeRequired)
            {
                Core.Environment.Invoke(() => DoubleClick());
                return;
            }

            switch (LoadType)
            {
                case CaseLoadType.Tray:
                    OnConveyorEnterLoad(new ConveyorEnterLoadEventArgs(FeedLoad.FeedTrayLoad(TransportSection, 0, Case_Load.GetCaseControllerCaseData(), TrayStatus.Empty)));
                    break;
                case CaseLoadType.TrayLoaded:
                    OnConveyorEnterLoad(new ConveyorEnterLoadEventArgs(FeedLoad.FeedTrayLoad(TransportSection, 0, Case_Load.GetCaseControllerCaseData(), TrayStatus.Loaded)));
                    break;
                case CaseLoadType.TrayStack:
                    OnConveyorEnterLoad(new ConveyorEnterLoadEventArgs(FeedLoad.FeedTrayLoad(TransportSection, 0, Case_Load.GetCaseControllerCaseData(), TrayStatus.Stacked)));
                    break;
                case CaseLoadType.Auto:
                    // TODO : CN Not sure what expected behaviour should be in this instance (create case for now)
                    OnConveyorEnterLoad(new ConveyorEnterLoadEventArgs(FeedLoad.FeedCaseLoad(TransportSection, 0, Case_Load.GetCaseControllerCaseData())));
                    break;
                case CaseLoadType.Case:
                default:
                    OnConveyorEnterLoad(new ConveyorEnterLoadEventArgs(FeedLoad.FeedCaseLoad(TransportSection, 0, Case_Load.GetCaseControllerCaseData())));
                    break;
            }
        }

        #endregion

        #region Assembly methods Reset, Dispose

        public override void Reset()
        {
            base.Reset();
        }

        public override string Category
        {
            get { return "Case Straight"; }
        }

        public override void Dispose()
        {
            Core.Environment.Scene.OnLoaded -= Scene_OnLoaded;
            constructDevice = null;
            if (Leaving != null)
                Leaving.Dispose();
            if (Entering != null)
                Entering.Dispose();

            arrow.Dispose();

            if (Assemblies != null)
            {
                foreach (Assembly assembly in this.Assemblies)
                {
                    assembly.Dispose();
                }
            }

            base.Dispose();
        }

        public event EventHandler<ConveyorEnterLoadEventArgs> ConveyorEnterLoad;
        protected virtual void OnConveyorEnterLoad(ConveyorEnterLoadEventArgs e)
        {
            if (ConveyorEnterLoad != null) ConveyorEnterLoad(this, e);
        }

        //public event EventHandler<ConveyorExitLoadEventArgs> ConveyorExitLoad;
        //protected virtual void OnConveyorExitLoad(ConveyorExitLoadEventArgs e)
        //{
        //    if (ConveyorExitLoad != null) ConveyorExitLoad(this, e);
        //}

        #endregion

        #region IConstructDevice

        public event EventHandler<SizeUpdateEventArgs> OnSizeUpdated;

        [Browsable(false)]
        public List<DeviceInfo> DeviceInfos
        {
            get { return straightinfo.deviceInfos; }
        }

        public void removeAssembly(Assembly assembly)
        {
            Remove(assembly);
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
                arrow.Width = value / 2;
                Core.Environment.SolutionExplorer.Refresh();

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

        [Category("Size and Speed")]
        [DisplayName("Width")]
        [Description("Width of the conveyor based on standard Dematic case conveyor widths")]
        [PropertyOrder(2)]
        public virtual CaseConveyorWidth ConveyorWidth
        {
            get { return straightinfo.conveyorWidth; }
            set
            {
                straightinfo.conveyorWidth = value;
                if (value != CaseConveyorWidth._Custom)
                {
                    Width = (float)value / 1000;
                }
                else
                {
                    Width = CustomWidth;
                }
            }
        }

        [Category("Size and Speed")]
        [DisplayName("Custom Width")]
        [Description("Custom conveyor width")]
        [PropertyOrder(3)]
        [PropertyAttributesProvider("DynamicPropertyCustomWidth")]
        public virtual float CustomWidth
        {
            get { return straightinfo.CustomWidth; }
            set
            {
                straightinfo.CustomWidth = value;
                Width = value;
            }
        }
        
        public void DynamicPropertyCustomWidth(Core.Properties.PropertyAttributes attributes)
        {
            attributes.IsBrowsable = ConveyorWidth == CaseConveyorWidth._Custom;
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
                    Invoke((Action)(() => UpdateLength(value)));
                }
                else
                { 
                    UpdateLength(value);
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
            get { return _LoadWaiting; }
            set { _LoadWaiting = value; }
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
            get { return _RouteAvailable; }
            set
            {
                if (value != _RouteAvailable)
                {
                    _RouteAvailable = value;

                    if (ThisRouteStatus != null)
                        ThisRouteStatus.Available = value;
                }
            }
        }

        #endregion

        #region User interface Fix Points

        [Category("Fix Points")]
        [DisplayName("Start Offset")]
        [Description("Move the fix point position in the Z axis (meter)")]
        [PropertyOrder(1)]
        public virtual float StartOffset
        {
            get { return straightinfo.startOffset; }
            set
            {
                straightinfo.startOffset = value;
                SnapStartTransformation.Offset = new Vector3(0, 0, value);
            }
        }

        [Category("Fix Points")]
        [DisplayName("End Offset")]
        [Description("Move the fix point position in the Z axis (meter)")]
        [PropertyOrder(2)]
        public virtual float EndOffset
        {
            get { return straightinfo.endOffset; }
            set
            {
                straightinfo.endOffset = value;
                SnapEndTransformation.Offset = new Vector3(0, 0, value);
            }
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

        [Category("Control")]
        [DisplayName("Load Orientation")]
        [Description("When a load is created on this location set what the orientation of the load should be, If set to Auto the EMU will try and add the load so that it fits the width of the conveyor")]
        public CaseOrientation CaseOrientation
        {
            get
            {
                return straightinfo.caseOrientation;
            }
            set
            {
                straightinfo.caseOrientation = value;
            }
        }

        /// <summary>
        /// Use this to change the route status of the current conveyor
        /// A load will only transfer on when the route status is available
        /// </summary>
        [Category("Control")]
        [DisplayName("Load Type")]
        [Description("Type of loads created on this conveyor (eg. Tray, Tray Loaded, Tray Stack, Case or Auto)")]
        public virtual CaseLoadType LoadType
        {
            get { return straightinfo.loadType; }
            set { straightinfo.loadType = value; }
        }

        #endregion

        public virtual void UpdateLength(float length)
        {
            Core.Environment.SolutionExplorer.Refresh();
            if (OnSizeUpdated != null)
            {
                OnSizeUpdated(this, new SizeUpdateEventArgs(length, null, null));
            }

            Leaving.Distance = length;
            endLine.LocalPosition = new Vector3(-length / 2 - 0.002f, 0, 0);
            startLine.LocalPosition = new Vector3(length / 2 + 0.002f, 0, 0);
        }

        public virtual void UpdateWidth()
        {
            if (OnSizeUpdated != null)
            {
                OnSizeUpdated(this, new SizeUpdateEventArgs(null, Width, null));
            }
            endLine.Length = Width + 0.005f;
            startLine.Length = Width + 0.005f;
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
                UpdateLength(Length);
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
                UpdateLength(Length);
            }
        }

        public virtual void NextAvailableChanged() { }

        public void SetLoadWaiting(bool loadWaiting, bool loadDeleted, Load waitingLoad)
        {
            LoadWaiting = loadWaiting;
            ThisLoadWaiting.SetLoadWaiting(loadWaiting, loadDeleted, waitingLoad);
        }

        [Browsable(false)]
        public RouteStatus ThisRouteStatus
        {
            get { return _ThisRouteStatus; }
            set { _ThisRouteStatus = value; }
        }

        [Browsable(false)]
        public RouteStatus NextRouteStatus
        {
            get { return _NextRouteStatus; }
            set { _NextRouteStatus = value; }
        }

        [Browsable(false)]
        public LoadWaitingStatus PreviousLoadWaiting
        {
            get { return _PreviousLoadWaiting; }
            set { _PreviousLoadWaiting = value; }
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
    [XmlInclude(typeof(StraightConveyorInfo))]
    public class StraightConveyorInfo : StraightInfo
    {
        public List<DeviceInfo> deviceInfos = new List<DeviceInfo>();
        public float startOffset = 0;  // fix point position in the Z axis
        public float endOffset = 0;    // fix point position in the Z axis
        public CaseConveyorWidth conveyorWidth = CaseConveyorWidth._500mm;
        public float CustomWidth = 0.5f;
        public CaseOrientation caseOrientation = CaseOrientation.Auto;
        public CaseLoadType loadType = CaseLoadType.Auto;
        public bool createTempAP = false;
    }

}
