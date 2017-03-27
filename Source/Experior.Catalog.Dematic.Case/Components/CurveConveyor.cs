using Experior.Catalog.Dematic.Case.Devices;
using Experior.Catalog.Logistic.Track;
using Experior.Core.Assemblies;
using Experior.Core.Loads;
using Experior.Core.Parts;
using Experior.Core.Properties;
using Experior.Dematic.Base;
using Experior.Dematic.Base.Devices;
using Experior.Dematic.Case.Devices;
using Microsoft.DirectX;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using System.Xml.Serialization;
using Environment = Experior.Core.Environment;

namespace Experior.Catalog.Dematic.Case.Components
{
    public abstract class CurveConveyor : Curve, IConstructDevice, IRouteStatus
    {
        private CurveConveyorInfo curveInfo;
        public ConstructDevice constructDevice;
        public IRouteStatus NextConveyor = null;
        public DematicArrow arrow;
        public Experior.Core.Parts.Cube startLine, endLine;
        public LoadWaitingStatus ThisLoadWaiting = new LoadWaitingStatus();

        //public RouteStatus ThisRouteStatus = new RouteStatus();
        //public RouteStatus NextRouteStatus = null;

        public event EventHandler<LoadWaitingChangedEventArgs> OnLoadWaitingChanged;
        public event EventHandler<SizeUpdateEventArgs> OnSizeUpdated;
        public void SizeUpdated(SizeUpdateEventArgs e)
        {
            if (OnSizeUpdated != null)
                OnSizeUpdated(this, e);
        }


        #region Constructors

        public CurveConveyor(CurveConveyorInfo info) : base(info)
        {
            curveInfo = info;
            Core.Environment.Scene.OnLoaded += Scene_OnLoaded;
            TransportSection.Route.OnArrived += Route_OnArrived;

            if (TransportSection.Route.Arrow != null)
            {
                TransportSection.Route.Arrow.Visible = false;
            }

            arrow = new DematicArrow(this, Width);
            UpdateArrowPosition();

            ThisRouteStatus.OnRouteStatusChanged += ThisRouteStatus_OnAvailableChanged;

            if (curveInfo.deviceInfos == null)
                curveInfo.deviceInfos = new List<DeviceInfo>();

            EndFixPoint.OnUnSnapped += EndFixPoint_OnUnSnapped;
            EndFixPoint.OnSnapped += EndFixPoint_OnSnapped;

            // constructDevice = new ConstructDevice(Name);
        }

        void ThisRouteStatus_OnAvailableChanged(object sender, RouteStatusChangedEventArgs e)
        {
            if (e._available == RouteStatuses.Available)
                arrow.Color = Color.Green;
            else
                arrow.Color = Color.Red;
        }

        void EndFixPoint_OnSnapped(FixPoint stranger, FixPoint.SnapEventArgs e)
        {
            NextConveyor = stranger.Parent as IRouteStatus;
            NextRouteStatus = NextConveyor.GetRouteStatus(stranger);
            NextRouteStatus.OnRouteStatusChanged += NextRouteStatus_OnAvailableChanged;
        }

        void EndFixPoint_OnUnSnapped(FixPoint stranger)
        {
            NextConveyor = null;
            NextRouteStatus.OnRouteStatusChanged -= NextRouteStatus_OnAvailableChanged;
            NextRouteStatus = null;
        }

        void Route_OnArrived(Core.Loads.Load load)
        {
            //throw new NotImplementedException();
        }

        public virtual void Scene_OnLoaded()
        {
            constructDevice = new ConstructDevice(Name);
            constructDevice.InsertDevices(this);
            Reset();
        }

        public override List<System.Windows.Forms.ToolStripItem> ShowContextMenu()
        {
            if (constructDevice == null) // The CommPointCreator type displays a context menu for adding Devices;
            {
                constructDevice = new ConstructDevice(Name);
                //Scene_OnLoaded();
            }
            return new List<ToolStripItem>(constructDevice.subMnu);
        }

        public virtual void NextRouteStatus_OnAvailableChanged(object sender, RouteStatusChangedEventArgs e) { }

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

        private void UpdateArrowPosition()
        {
            arrow.Width = Width / 2;
            float distance = Length / 2;

            double theta = distance / Radius;
            double x, z;

            double heightDiff = PositionEnd.Y - PositionStart.Y;
            float y = (distance / Length) * (PositionEnd.Y - PositionStart.Y);
            if (Revolution == Core.Environment.Revolution.Counterclockwise)
            {
                float startHeight = PositionStart.Y;
                float endHeight = PositionEnd.Y;

                x = Radius * (Math.Sin(theta));
                z = Radius * (Math.Cos(theta));
                arrow.LocalPosition = new Vector3(-(float)x, y, (float)z);
                arrow.LocalYaw = -(float)theta;
            }
            else
            {
                z = Radius * (Math.Cos(theta));
                x = Radius * (Math.Sin(theta));
                arrow.LocalPosition = new Vector3((float)x, y, (float)z);
                arrow.LocalYaw = (float)theta + (float)Math.PI;
            }
            arrow.LocalRoll = -(float)Math.Asin(heightDiff / Length);

        }

        public override void Dispose()
        {

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

        #endregion

        #region Doubleclick feeding

        public override void DoubleClick()
        {
            // CN : Overide double click so that loads are not added
        }

        #endregion

        #region IConstructDevice

        [Browsable(false)]
        public List<DeviceInfo> DeviceInfos
        {
            get { return curveInfo.deviceInfos; }
        }

        public void addAssembly(Core.Assemblies.Assembly assembly, Vector3 localPosition)
        {
            Add(assembly, localPosition);
            Core.Environment.SolutionExplorer.Update(this);
        }

        public void removeAssembly(Core.Assemblies.Assembly assembly)
        {
            RemoveAssembly(assembly);
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
                foreach (Assembly assembly in Assemblies)
                {
                    if (assembly.Name == assemblyName)
                        return true;
                }
            }
            return false;
        }


        //for Length - see User Interface
        #endregion

        #region User interface

        #region User Interface Position

        [Category("Position")]
        [DisplayName("Height")]
        [Description("Height of the conveyor (meter)")]
        [TypeConverter()]
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

        #region User Interface Size and Speed

        [Category("Size and Speed")]
        [DisplayName("Length")]
        [Description("Length of the conveyor (meter), this is for info only, the conveyor length is changed based on the radius and angle of the conveyor")]
        [TypeConverter()]
        [ReadOnly(true)]
        public float Length
        {
            get
            {
                return Radius * Angle;
            }
        }

        [Category("Size and Speed")]
        [DisplayName("Width")]
        [Description("Width of the conveyor based on standard Dematic case conveyor widths")]
        public CaseConveyorWidth ConveyorWidth
        {
            get { return curveInfo.conveyorWidth; }
            set
            {
                Width = (float)value / 1000;
                curveInfo.conveyorWidth = value;
            }
        }

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

        [Category("Size and Speed")]
        [DisplayName("Radius")]
        [Description("Radius of the conveyor (meter)")]
        [TypeConverter()]
        public override float Radius
        {
            get
            {
                return base.Radius;
            }
            set
            {
                base.Radius = value;
                if (OnSizeUpdated != null)
                {
                    OnSizeUpdated(this, new SizeUpdateEventArgs(null, null, value));
                    UpdateLength();
                }
            }
        }

        [Category("Size and Speed")]
        [DisplayName("Angle")]
        [Description("Angle of the conveyor (degrees)")]
        public override float Angle
        {
            get
            {
                return base.Angle;
            }
            set
            {
                base.Angle = value;
                UpdateLength();
            }
        }

        [Category("Size and Speed")]
        [PropertyOrder(10)]        
        public override Environment.Revolution Revolution
        {
            get
            {
                return base.Revolution;
            }
            set
            {
                base.Revolution = value;
                UpdateLength();
            }

        }

        #endregion

        #region User Interface Status

        private bool _LoadWaiting = false;
        [Category("Status")]
        [DisplayName("LoadWaiting")]
        [Description("Is there a load waiting to release into the next conveyor")]
        [ReadOnly(true)]
        public virtual bool LoadWaiting
        {
            get { return _LoadWaiting; }
            set { _LoadWaiting = value; }
        }

        public RouteStatuses _RouteAvailable = RouteStatuses.Available;
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

        //This is not implemented due to time, it would be nice if the end fix points could be moved but we need to perform
        //some trig on this, also the vale is not being saved and the route is not being moved.


        //[Category("Fix Points")]
        //[DisplayName("Start Offset")]
        //[Description("Move the fix point position in the Z axis (meter)")]
        //[PropertyOrder(1)]
        //public virtual float StartOffset
        //{
        //    get { return curveInfo.startOffset; }
        //    set
        //    {
        //        curveInfo.startOffset = value;
        //        StartFixPoint.LocalPosition = new Vector3(StartFixPoint.LocalPosition.X, StartFixPoint.LocalPosition.Y, value);
                
        //        //SnapStartTransformation.Offset = new Vector3(0, 0, value);
        //    }
        //}

        //[Category("Fix Points")]
        //[DisplayName("End Offset")]
        //[Description("Move the fix point position in the Z axis (meter)")]
        //[PropertyOrder(2)]
        //public virtual float EndOffset
        //{
        //    get { return curveInfo.endOffset; }
        //    set
        //    {
        //        curveInfo.endOffset = value;
        //        SnapEndTransformation.Offset = new Vector3(0, 0, value);
        //    }
        //}


        //[DisplayName("End")]
        //[PropertyOrder(1)]
        //[Category("Fix Points")]
        //public virtual SnapProperties SnapEndTransformation
        //{
        //    get
        //    {
        //        return this.curveInfo.snapendtransformation;
        //    }
        //    set
        //    {
        //        this.curveInfo.snapendtransformation = value;
        //    }
        //}

        #endregion

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
        public override Core.Routes.Route.SpacingTypes SpacingType
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

        #endregion

        public override string Category
        {
            get { return "BK10 Curve"; }
        }

        private void UpdateLength()
        {
            Core.Environment.SolutionExplorer.Refresh();
            if (OnSizeUpdated != null)
            {
                OnSizeUpdated(this, new SizeUpdateEventArgs(Length, null, null));
            }
         
            UpdateArrowPosition();
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
                base.Width = value;
                Core.Environment.SolutionExplorer.Refresh();
                if (OnSizeUpdated != null)
                {
                    OnSizeUpdated(this, new SizeUpdateEventArgs(null, value, null));
                }

                UpdateArrowPosition();
            }
        }



        public void SetLoadWaiting(bool loadWaiting, bool loadDeleted, Load waitingLoad)
        {
            LoadWaiting = loadWaiting;
            ThisLoadWaiting.SetLoadWaiting(loadWaiting, loadDeleted, waitingLoad);
        }

        [Browsable(false)]
        public override Core.Assemblies.EventCollection Events
        {
            get
            {
                return base.Events;
            }
        }

        private RouteStatus _ThisRouteStatus = new RouteStatus();
        [Browsable(false)]
        public RouteStatus ThisRouteStatus
        {
            get { return _ThisRouteStatus; }
            set { _ThisRouteStatus = value; }
        }

        private RouteStatus _NextRouteStatus;
        [Browsable(false)]
        public RouteStatus NextRouteStatus
        {
            get { return _NextRouteStatus; }
            set { _NextRouteStatus = value; }
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

    }

    [Serializable]
    [XmlInclude(typeof(CurveConveyorInfo))]
    public class CurveConveyorInfo : Catalog.Logistic.Track.CurveInfo
    {
        public List<DeviceInfo> deviceInfos = new List<DeviceInfo>();
        public CaseConveyorWidth conveyorWidth = CaseConveyorWidth._500mm;

        public float startOffset = 0;  // fix point position in the Z axis
        public float endOffset = 0;    // fix point position in the Z axis

    }
}