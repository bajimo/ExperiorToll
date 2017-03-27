using Experior.Catalog.Logistic.Track;
using Experior.Core.Assemblies;
using Experior.Core.Loads;
using Experior.Core.Properties;
using Experior.Dematic.Base;
using Experior.Dematic.Base.Devices;
using Microsoft.DirectX;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Xml.Serialization;

namespace Experior.Catalog.Dematic.Pallet.Devices
{

    public class PalletPhotocellDisplay : PhotocellDisplay
    {
        public PalletPhotocellDisplay(PhotocellDisplayInfo info) : base(info)
        {
        }

        public override float Distance
        {
            get
            {
                return base.Distance;
            }
            set
            {
                base.Distance = value;
                if (Parent != null)
                {
                    PalletPhotocell pec = Parent as PalletPhotocell;
                    LocalPosition = new Vector3(pec.PhotocellInfo.length / 2 - value, 0, 0);
                }
            }
        }
    }

    public class PalletPhotocell : Device
    {
        public PalletPhotocellInfo PhotocellInfo;
        public DematicSensor sensor = new DematicSensor();
        private Load occupiedLoad = null;
        
        public event EventHandler<PhotocellRenamedEventArgs> OnPhotocellRenamed;
        public event EventHandler<PhotocellStatusChangedEventArgs> OnPhotocellStatusChanged;
        public static EventHandler<PhotocellStatusChangedEventArgs> OnPhotocellStatusChangedRoutingScript;

        private Core.Timer BlockedTimer = new Core.Timer(0);
        private Core.Timer ClearTimer = new Core.Timer(0);
        bool restartBlocked = false;
        bool restartClear = false;

        static int nameIndex = 0;

        public PalletPhotocellDisplay photocellDisplay;

        #region Constructors

        public PalletPhotocell(PalletPhotocellInfo info, BaseTrack conv) : base(info, conv)
        {
            PhotocellInfo = info;
            AssemblyInfo ai = new AssemblyInfo();
            conveyor = conv;

            photocellDisplay = new PalletPhotocellDisplay(new PhotocellDisplayInfo { width = info.width });
            photocellDisplay.ListSolutionExplorer = false;
            photocellDisplay.OnPhotocellDisplayDeleted += photocellDisplay_OnPhotocellDisplayDeleted;

            Add(photocellDisplay, new Vector3(info.length / 2, 0, 0));
            sensor.OnEnter += sensor_OnEnter;
            sensor.OnLeave += sensor_OnLeave;
            sensor.Color = Color.Green;
            sensor.Visible = false;

            conv.TransportSection.Route.InsertActionPoint(sensor);

            OnNameChanged += PalletPhotocell_OnNameChanged;
        }

        public override bool Visible
        {
            get
            {
                return base.Visible;
            }

            set
            {
                base.Visible = value;
                photocellDisplay.Visible = value;
            }
        }


        private void PalletPhotocell_OnNameChanged(Assembly sender, string current, string old)
        {
            if (OnPhotocellRenamed != null)
            {
                OnPhotocellRenamed(this, new PhotocellRenamedEventArgs(PhotocellInfo, old, current));
            }
        }

        void routeStatus_OnRouteStatusChanged(object sender, RouteStatusChangedEventArgs e)
        {
            if (e._available == RouteStatuses.Available)
            {
                //Start the timers
                if (restartBlocked && BlockedTimeout != 0)
                {
                    BlockedTimer.Start();
                }
                if (restartClear && ClearTimeout != 0)
                {
                    ClearTimer.Start();
                }
                restartBlocked = false;
                restartClear = false;
            }
            else
            {
                if (BlockedTimeout != 0 && BlockedTimer.Running)
                {
                    BlockedTimer.Stop();
                    restartBlocked = true;
                }
                if (ClearTimeout != 0 && ClearTimer.Running)
                {
                    ClearTimer.Stop();
                    restartClear = true;
                }
            }
        }

        public override void Device_OnSizeUpdated(object sender, SizeUpdateEventArgs e)
        {
            if (e._length != null)
            {
                this.UpdateLength((float)e._length);
            }
            else if (e._width != null)
            {
                this.UpdateWidth((float)e._width);
            }        
        }

        void photocellDisplay_OnPhotocellDisplayDeleted(object sender, EventArgs e)
        {
            photocellDisplay.OnPhotocellDisplayDeleted -= photocellDisplay_OnPhotocellDisplayDeleted;
            if (photocellDisplay.deleteFromUser)
            {
                //If the user deletes the photocell then the whole photocell is deleted however just the graphic is deleted and recreated when the conveyor changes its width.
                //If the user has deleted this then we must delete everything
                this.Dispose();
            }
        }

        void sensor_OnLeave(DematicSensor sender, Load load)
        {
            if (BlockedTimeout != 0)
            {
                BlockedTimer.Reset();
                BlockedTimer.OnElapsed -= BlockedTimer_OnElapsed;
            }

            if (ClearTimeout != 0 && PhotocellStatus == PhotocellState.Blocked) // || PhotocellStatus == PhotocellState.LoadBlocked)) [BG]
            {
                PhotocellStatus = PhotocellState.LoadClear;
                ClearTimer.OnElapsed += ClearTimer_OnElapsed;
                ClearTimer.Timeout = ClearTimeout;
                ClearTimer.Start();
            }
            else
            {
                PhotocellStatus = PhotocellState.Clear;
            }

            occupiedLoad = null;
        }

        void sensor_OnEnter(DematicSensor sender, Load load)
        {
            if (ClearTimeout != 0)
            {
                ClearTimer.Reset();
                ClearTimer.OnElapsed -= ClearTimer_OnElapsed;
            }

            if (BlockedTimeout != 0)
            {
                PhotocellStatus = PhotocellState.LoadBlocked;
                BlockedTimer.OnElapsed += BlockedTimer_OnElapsed;
                BlockedTimer.Timeout = BlockedTimeout;
                BlockedTimer.Start();
            }
            else
            {
                PhotocellStatus = PhotocellState.Blocked;
            }

            occupiedLoad = load;
        }

        void BlockedTimer_OnElapsed(Core.Timer sender)
        {
            PhotocellStatus = PhotocellState.Blocked;
            BlockedTimer.OnElapsed -= BlockedTimer_OnElapsed;
            BlockedTimer.Reset();
        }

        void ClearTimer_OnElapsed(Core.Timer sender)
        {
            PhotocellStatus = PhotocellState.Clear;
            ClearTimer.OnElapsed -= ClearTimer_OnElapsed;
            ClearTimer.Reset();
        }
        #endregion

        #region Administration methods, dispose etc
        public void UpdateWidth(float width)
        {
            PhotocellInfo.width = width;
            RemoveAssembly(photocellDisplay);  //Easier to recreate graphic as it is just a graphic
            photocellDisplay.deleteFromUser = false;
            photocellDisplay.Dispose();

            photocellDisplay = new PalletPhotocellDisplay(new PhotocellDisplayInfo { width = width });
            photocellDisplay.ListSolutionExplorer = false;
            photocellDisplay.OnPhotocellDisplayDeleted += photocellDisplay_OnPhotocellDisplayDeleted;

            Add(photocellDisplay, new Vector3(0, 0, 0));
            DeviceDistance = PhotocellInfo.distance;
        }

        public void UpdateLength(float length)
        {
            PhotocellInfo.length = length;

            DeviceDistance = PhotocellInfo.distance;
        }

        private void UpdatePosition()
        {
            float sensorDistance = DeviceDistance;

            if (DistanceFrom == PositionPoint.End)
            {
                sensorDistance = PhotocellInfo.length - DeviceDistance;
            }

            sensor.Distance = sensorDistance;
            if (photocellDisplay != null)
            {
                photocellDisplay.Distance = sensorDistance;
            }
        }

        public static string GetValidPhotocellName(string prefix)
        {
            return prefix + nameIndex++.ToString();
        }

        public override void Reset()
        {
            base.Reset();
            PhotocellStatus = PhotocellState.Clear;
            restartBlocked = false;
            restartClear = false;
            occupiedLoad = null;

            BlockedTimer.Reset();
            BlockedTimer.OnElapsed -= BlockedTimer_OnElapsed;
            ClearTimer.Reset();
            ClearTimer.OnElapsed -= ClearTimer_OnElapsed;
        }

        public override void Dispose()
        {
            if (sensor != null)
            {
                conveyor.TransportSection.Route.RemoveActionPoint(sensor);
                sensor.Dispose();
            }

            RemoveAssembly(photocellDisplay);
            photocellDisplay.Dispose();
            RemoveAssembly(this);

            base.Dispose();
        }

        #endregion

        #region User Interface
        [Category("Position")]
        [DisplayName("Distance From")]
        [Description("Measure the distance from either the start or the end of the conveyor")]
        public PositionPoint DistanceFrom
        {
            get { return PhotocellInfo.distanceFrom; }
            set
            {
                PhotocellInfo.distanceFrom = value;
                UpdatePosition();
            }
        }


        [Category("Position")]
        [DisplayName("Distance")]
        [Description("The position of the photocell based on the distance from either the start or end of the conveyor (m)")]
        [TypeConverter()]
        public override float DeviceDistance
        {
            get
            {
                return PhotocellInfo.distance;
            }
            set
            {
                PhotocellInfo.distance = value;   //save the distance value
                UpdatePosition();
            }
        }

        [Category("Status")]
        [DisplayName("Occupied")]
        [ReadOnly(true)]
        public PhotocellState PhotocellStatus
        {
            get { return PhotocellInfo.photocellStatus; }
            set
            {
                PhotocellInfo.photocellStatus = value;

                switch (value)
                {
                    case PhotocellState.Clear: photocellDisplay.cylinder.Color = Color.Green; break;
                    case PhotocellState.Blocked: photocellDisplay.cylinder.Color = Color.Red; break;
                    case PhotocellState.LoadClear: photocellDisplay.cylinder.Color = Color.YellowGreen; break;
                    case PhotocellState.LoadBlocked: photocellDisplay.cylinder.Color = Color.Orange; break;
                }
              
                if (OnPhotocellStatusChanged != null)
                {
                    bool loadDeleted = false;
                    if (sensor.ActiveLoad != null)
                        loadDeleted = sensor.ActiveLoad.StartDisposing;

                    OnPhotocellStatusChanged(this, new PhotocellStatusChangedEventArgs(value, loadDeleted, sensor.ActiveLoad));
                }
                if (RoutingEvent && OnPhotocellStatusChangedRoutingScript != null)
                {
                    OnPhotocellStatusChangedRoutingScript(this, new PhotocellStatusChangedEventArgs(value, false, sensor.ActiveLoad));
                }
                Core.Environment.Properties.Refresh();
            }
        }

        [Category("Status")]
        [DisplayName("Trigger Event")]
        [Description("Trigger a routing script event whenever the status of the photocell changes")]
        public bool RoutingEvent
        {
            get { return PhotocellInfo.routingEvent; }
            set
            {
                PhotocellInfo.routingEvent = value;
            }
        }

        [Category("Configuration")]
        [DisplayName("Blocked Timeout")]
        [Description("How many seconds should the photocell be covered before being set blocked. Note if set to 0 photocell becomes blocked as soon as soon as a load arrives")]
        [PropertyOrder(1)]
        public float BlockedTimeout
        {
            get { return PhotocellInfo.blockedTimeout; }
            set { PhotocellInfo.blockedTimeout = value; }
        }

        [Category("Configuration")]
        [DisplayName("Clear Timeout")]
        [Description("How many seconds should the photocell be uncovered before being set clear. Note if set to 0 photocell becomes clear as soon as soon as a load leaves")]
        [PropertyOrder(2)]
        public float ClearTimeout
        {
            get { return PhotocellInfo.clearTimeout; }
            set { PhotocellInfo.clearTimeout = value; }
        }

        [Browsable(false)]
        public Load OccupiedLoad
        {
            get { return occupiedLoad; }
        }

        [Browsable(false)]
        public bool Occupied
        {
            get { return occupiedLoad == null ? false : true; }
        }

        #endregion

        #region Catalogue Properties

        public override string Category
        {
            get { return "Photcell Point"; }
        }

        public override Image Image
        {
            get { return Common.Icons.Get("photoeye"); }
        }

        #endregion
    }

    [Serializable]
    [XmlInclude(typeof(PalletPhotocellInfo))]
    public class PalletPhotocellInfo : PhotocellInfo
    {

    }
}