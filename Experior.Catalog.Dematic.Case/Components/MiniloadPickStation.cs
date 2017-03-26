using Experior.Catalog.Dematic.Case.Devices;
using Experior.Catalog.Logistic.Track;
using Experior.Core;
using Experior.Core.Assemblies;
using Experior.Core.Loads;
using Experior.Core.Parts;
using Experior.Core.Properties;
using Experior.Core.Properties.TypeConverter;
using Experior.Core.Routes;
using Experior.Dematic.Base;
using Experior.Dematic.Base.Devices;
using Microsoft.DirectX;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Xml.Serialization;

namespace Experior.Catalog.Dematic.Case.Components
{
    //apEnter <------------------ apHold
    //                              ^
    //                              |
    //apRevStart -------------> apRevEnd
    //    ^
    //    |
    // apFeed

    public class MiniloadPickStation : StraightConveyor, IControllable
    {
        MiniloadPickStationInfo miniloadPickStationInfo;
        StraightConveyor feedSection;
        StraightConveyor reverseSection;
        ActionPoint apEnter = new ActionPoint();
        ActionPoint apFeed = new ActionPoint();
        ActionPoint apHold = new ActionPoint();
        ActionPoint apRevStart = new ActionPoint();
        ActionPoint apRevEnd = new ActionPoint();
        Timer SingleTimer;
        bool LoadTransferringIn = false;
        bool SingleLoadTransfer = false;

        public event EventHandler<PickStationStatusUpdateEventArgs> OnPickStationStatusUpdate;

        public virtual void PickStationStatusUpdate(PickStationStatusUpdateEventArgs eventArgs)
        {
            if (OnPickStationStatusUpdate != null)
            {
                OnPickStationStatusUpdate(this, eventArgs);
            }
        }
        
        public MiniloadPickStation(MiniloadPickStationInfo info):base(info)
        {

            miniloadPickStationInfo = info;

            StraightConveyorInfo feedSectionInfo = new StraightConveyorInfo()
            {
                Length    = Width / 2,
                thickness = info.thickness,
                Width     = info.width,
                Speed     = info.Speed
            };

            feedSection = new StraightConveyor(feedSectionInfo);
            feedSection.endLine.Visible     = false;
            feedSection.EndFixPoint.Enabled = false;
            feedSection.EndFixPoint.Visible = false;
            feedSection.Color = this.Color;
            feedSection.TransportSection.Visible = false;

            StraightConveyorInfo reverseSectionInfo = new StraightConveyorInfo()
            {
                Length = Width / 2,
                thickness = info.thickness,
                Width = info.width,
                Speed = info.Speed
            };

            reverseSection = new StraightConveyor(reverseSectionInfo);
            reverseSection.endLine.Visible = false;
            reverseSection.startLine.Visible = false;
            reverseSection.EndFixPoint.Enabled = false;
            reverseSection.EndFixPoint.Visible = false;
            reverseSection.StartFixPoint.Enabled = false;
            reverseSection.StartFixPoint.Visible = false;
            reverseSection.Color = this.Color;
            reverseSection.TransportSection.Visible = false;
            reverseSection.arrow.Visible = false;

            Add(feedSection);
            Add(reverseSection);

            TransportSection.Route.InsertActionPoint(apEnter);
            TransportSection.Route.InsertActionPoint(apHold, 0);

            feedSection.TransportSection.Route.InsertActionPoint(apFeed, feedSection.Length);
            reverseSection.TransportSection.Route.InsertActionPoint(apRevStart, 0);
            reverseSection.TransportSection.Route.InsertActionPoint(apRevEnd, reverseSection.Length);

            apFeed.OnEnter += apFeed_OnEnter;
            apEnter.OnEnter += apEnter_OnEnter;
            apHold.OnEnter += apHold_OnEnter;
            apRevEnd.OnEnter += apRevEnd_OnEnter;

            feedSection.StartFixPoint.OnSnapped += FeedSectionStartFixPoint_OnSnapped;
            feedSection.StartFixPoint.OnUnSnapped += FeedSectionStartFixPoint_OnUnSnapped;

            Length = info.length;
            feedSection.Width = (float)info.feedWidth / 1000;
            Speed = info.conveyorSpeed;
            reverseSection.Speed = info.conveyorSpeed;

            SingleTimer = new Timer(info.singleTimeout);
            SingleTimer.OnElapsed += SingleTimer_OnElapsed;

            UpdateConveyor();
        }

        public override void Scene_OnLoaded()
        {
            base.Scene_OnLoaded();

            if (ControllerProperties == null)
            {
                ControllerProperties = StandardCase.SetMHEControl(miniloadPickStationInfo, this);
            }
            feedSection.RouteAvailable = RouteStatuses.Request;
            LoadTransferringIn = false;
        }

        public override void Reset()
        {
            base.Reset();
            feedSection.RouteAvailable = RouteStatuses.Request;
            LoadTransferringIn = false;
            SingleLoadTransfer = false;
        }

        public override void NextRouteStatus_OnAvailableChanged(object sender, RouteStatusChangedEventArgs e)
        {
            base.NextRouteStatus_OnAvailableChanged(sender, e);
            if (e._available == RouteStatuses.Blocked)
            {
                if (PreviousLoadWaiting.LoadWaiting)
                {
                    feedSection.RouteAvailable = RouteStatuses.Available;
                    LoadTransferringIn = true;
                }
                else
                {
                    feedSection.RouteAvailable = RouteStatuses.Request;
                    LoadTransferringIn = false;
                }
            }
            else if (e._available == RouteStatuses.Available)
            {
                if (apHold.Active)
                {
                    apHold.ActiveLoad.Release();
                }
                if (apEnter.Active)
                {
                    apEnter.ActiveLoad.Release();
                }
            }
        }

        public void FeedSectionStartFixPoint_OnSnapped(FixPoint fixpoint, FixPoint.SnapEventArgs e)
        {
            PreviousConveyor = fixpoint.Parent as IRouteStatus;
            PreviousLoadWaiting = PreviousConveyor.GetLoadWaitingStatus(fixpoint);
            PreviousLoadWaiting.OnLoadWaitingChanged += PreviousLoadWaiting_OnLoadWaitingChanged;
            feedSection.Speed = PreviousConveyor.Speed;
        }

        public void FeedSectionStartFixPoint_OnUnSnapped(FixPoint fixpoint)
        {
            PreviousLoadWaiting.OnLoadWaitingChanged -= PreviousLoadWaiting_OnLoadWaitingChanged;
            PreviousLoadWaiting = null;
            PreviousConveyor = null;
        }

        void PreviousLoadWaiting_OnLoadWaitingChanged(object sender, LoadWaitingChangedEventArgs e)
        {
            if (!e._loadWaiting)
            {
                feedSection.RouteAvailable = RouteStatuses.Request;
            }
            else
            {
                if (feedSection.RouteAvailable == RouteStatuses.Request && !LoadTransferringIn)
                {
                    feedSection.RouteAvailable = RouteStatuses.Available;
                    LoadTransferringIn = true;
                }
            }
        }

        void apFeed_OnEnter(ActionPoint sender, Load load)
        {
            if (apHold.Active)
            {
                load.Switch(apEnter, true);
            }
            else
            {
                load.Switch(apRevStart, true);
            }
        }

        void apEnter_OnEnter(ActionPoint sender, Load load)
        {
            if (NextRouteStatus == null)
            {
                return;
            }

            if (SingleLoadTransfer)
            {
                load.Stop();
                SingleLoadTransfer = false;
                PickStationStatusUpdate(new PickStationStatusUpdateEventArgs(null, (Case_Load)load));
                return;
            }

            if (NextRouteStatus.Available != RouteStatuses.Available) //Load is not passing through on the way to the miniload
            {
                feedSection.RouteAvailable = RouteStatuses.Blocked;
                SingleTimer.Stop();
                PickStationStatusUpdate(new PickStationStatusUpdateEventArgs((Case_Load)apHold.ActiveLoad, (Case_Load)load));
                LoadTransferringIn = false;
                load.Stop();
            }
        }

        void apHold_OnEnter(ActionPoint sender, Load load)
        {
            load.Stop();
            if (PreviousLoadWaiting.LoadWaiting)
            {
                feedSection.RouteAvailable = RouteStatuses.Available;
                LoadTransferringIn = true;
            }
            else
            {
                feedSection.RouteAvailable = RouteStatuses.Request;
                LoadTransferringIn = false;
            }

            //When the first load arrives at the hold position, then start the Single load timer
            SingleTimer.Start();
        }

        void SingleTimer_OnElapsed(Timer sender)
        {
            if (apHold.Active && feedSection.RouteAvailable == RouteStatuses.Request && !LoadTransferringIn)
            {
                feedSection.RouteAvailable = RouteStatuses.Blocked;
                LoadTransferringIn = false;
                //Transfer the load to the PS11 position
                SingleLoadTransfer = true;
                apHold.ActiveLoad.Release();
                //PickStationStatusUpdate(new PickStationStatusUpdateEventArgs((Case_Load)apHold.ActiveLoad, null));
            }
        }

        void apRevEnd_OnEnter(ActionPoint sender, Load load)
        {
            load.Switch(apHold);
        }

        public void LockPickStation()
        {
            feedSection.RouteAvailable = RouteStatuses.Blocked;
        }

        public override void UpdateConveyor()
        {
            float hold = (Length - feedSection.Width) / 2;
            float enter = Length - feedSection.Width / 2;
            
            feedSection.Length = Width / 2;
            reverseSection.Length = enter - hold;

            float xOffset = Length / 2 - feedSection.Width / 2;

            reverseSection.LocalPosition = new Vector3(-(hold + (reverseSection.Length / 2) - Length /2), reverseSection.LocalPosition.Y, 0);
            reverseSection.LocalYaw = (float)Math.PI;

            if (MergeSide == Side.Left)
            {
                feedSection.LocalPosition = new Vector3(-xOffset, feedSection.LocalPosition.Y, -feedSection.Length / 2);
                feedSection.LocalYaw = (float)Math.PI / 2;
            }
            else
            {
                feedSection.LocalPosition = new Vector3(-xOffset, feedSection.LocalPosition.Y, feedSection.Length / 2);
                feedSection.LocalYaw = -(float)Math.PI / 2;
            }

            apEnter.Color = Color.Red;
            apFeed.Color = Color.Red;
            apHold.Color = Color.Blue;
            apRevStart.Color = Color.DarkRed;
            apRevEnd.Color = Color.DarkBlue;

            apEnter.Visible = false;
            apFeed.Visible = false;
            apHold.Visible = false;
            apRevStart.Visible = false;
            apRevEnd.Visible = false;

            apEnter.Distance = enter;
            apHold.Distance = hold;
            apFeed.Distance = feedSection.Length;
            apRevEnd.Distance = reverseSection.Length;
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
            miniloadPickStationInfo.ProtocolInfo = null;
        }

        void controller_OnControllerRenamedEvent(object sender, EventArgs e)
        {
            miniloadPickStationInfo.ControllerName = ((Assembly)sender).Name;
            Core.Environment.Properties.Refresh();
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
                return miniloadPickStationInfo.ControllerName;
            }
            set
            {
                if (!value.Equals(miniloadPickStationInfo.ControllerName))
                {
                    ControllerProperties = null;
                    miniloadPickStationInfo.ProtocolInfo = null;
                    Controller = null;
                }

                miniloadPickStationInfo.ControllerName = value;
                if (value != null)
                {
                    //Note StandardCase.SetMHEControl will set the ControllerProperties and Controller properties!
                    ControllerProperties = StandardCase.SetMHEControl(miniloadPickStationInfo, this);
                    if (ControllerProperties == null)
                    {
                        miniloadPickStationInfo.ControllerName = "No Controller";
                    }
                }
            }
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

        public void DynamicPropertyAssemblyPLCconfig(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = Controller != null;
        }


        #endregion

        public override string Category
        {
            get { return "Miniload"; }
        }

        public override Image Image
        {
            get 
            {
                return Common.Icons.Get("BeltSorterMergePopUp");   
            }
        }

        #region Action Points

        [Browsable(false)]
        public Load Position1Load //Closest to the SRM
        {
            get 
            {
                if (MergeSide == Experior.Dematic.Base.Side.Left)
                {
                    return apEnter.Active ? apEnter.ActiveLoad : null;
                }
                else
                {
                    return apHold.Active ? apHold.ActiveLoad : null;
                }
            }
        }

        [Browsable(false)]
        public Load Position2Load //Furthest from the SRM
        {
            get
            {
                if (MergeSide == Experior.Dematic.Base.Side.Right)
                {
                    return apEnter.Active ? apEnter.ActiveLoad : null;
                }
                else
                {
                    return apHold.Active ? apHold.ActiveLoad : null;
                }
            }
        }

        #endregion

        #region Size and Speed

        [Category("Size and Speed")]
        [DisplayName("Straight Length")]
        [PropertyOrder(1)]
        [Description("Length of the straight section conveyor (meter)")]
        public override float Length
        {
            get { return base.Length; }
            set
            {
                if (value < feedSection.Width)
                {
                    Core.Environment.Log.Write(string.Format("Conveyor Length must not be less than the feed section conveyor width ({0}).", feedSection.Width.ToString()), System.Drawing.Color.Red);

                }
                else
                {
                    base.Length = value;
                    Core.Environment.Invoke(() => UpdateConveyor());
                }
            }
        }

        [Category("Size and Speed")]
        [DisplayName("Feed Width")]
        [PropertyOrder(4)]
        [Description("Width of the feed section conveyor based on standard Dematic case conveyor widths")]
        public CaseConveyorWidth FeedWidth
        {
            get { return miniloadPickStationInfo.feedWidth; }
            set
            {
                feedSection.Width = (float)value / 1000;
                miniloadPickStationInfo.feedWidth = value;
                Core.Environment.Invoke(() => UpdateConveyor());
            }
        }

        [Category("Size and Speed")]
        [DisplayName("Merge Side")]
        [Description("Left or right merge")]
        [PropertyOrder(8)]
        [TypeConverter()]
        public Side MergeSide
        {
            get { return miniloadPickStationInfo.feedSide; }
            set
            {
                miniloadPickStationInfo.feedSide = value;
                Core.Environment.Invoke(() => UpdateConveyor());
            }
        }

        [Category("Size and Speed")]
        [DisplayName("Speed")]
        [Description("Speed of loading conveyor")]
        [PropertyOrder(9)]
        [TypeConverter(typeof(SpeedConverter))]
        public float ConveyorSpeed
        {
            get { return miniloadPickStationInfo.conveyorSpeed; }
            set
            {
                miniloadPickStationInfo.conveyorSpeed = value;
                Speed = value;
                reverseSection.Speed = value;
            }
        }

        [Category("Pick Station")]
        [DisplayName("Single Load Timeout")]
        [Description("How long to wait before reporting the Pick Station status with a single load (at second pick-up position")]
        public float SingleTimeout
        {
            get { return miniloadPickStationInfo.singleTimeout; }
            set
            {
                if (SingleTimer.Running)
                {
                    Log.Write(string.Format("Miniload {0}: Cannot update timer whilst running.", Name));
                }
                else
                {
                    SingleTimer.Timeout = value;
                    miniloadPickStationInfo.singleTimeout = value;
                }
            }
        }

        [Browsable(false)]
        public override float EndOffset
        {
            get { return base.EndOffset; }
            set { base.EndOffset = value; }
        }

        [Browsable(false)]
        public override float StartOffset
        {
            get { return base.StartOffset; }
            set { base.StartOffset = value; }
        }

        [Browsable(false)]
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
    }

    [Serializable]
    [XmlInclude(typeof(MiniloadPickStationInfo))]
    public class MiniloadPickStationInfo : StraightBeltConveyorInfo, IControllableInfo
    {
        public Side feedSide = Side.Left;
        public CaseConveyorWidth feedWidth = CaseConveyorWidth._500mm;
        public float conveyorSpeed = 0.7f;
        public ControlTypesSubSet ControlType;
        public float singleTimeout = 30;

        //IControllable
        private string controllerName = "No Controller";
        public string ControllerName
        {
            get { return controllerName; }
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

    public class PickStationStatusUpdateEventArgs : EventArgs
    {
        public readonly Case_Load _LoadPosOutside;
        public readonly Case_Load _LoadPosInside;
        public PickStationStatusUpdateEventArgs(Case_Load LoadPosOutside, Case_Load LoadPosInside)
        {
            _LoadPosOutside = LoadPosOutside;
            _LoadPosInside = LoadPosInside;
        }
    }

}