using Experior.Catalog.Dematic.Pallet.Devices;
using Experior.Catalog.Devices;
using Experior.Core.Assemblies;
using Experior.Core.Loads;
using Experior.Core.Properties;
using Experior.Dematic.Base;
using Experior.Dematic.Base.Devices;
using Experior.Dematic.Pallet.Devices;
using System;
using System.ComponentModel;
using System.Xml.Serialization;

namespace Experior.Catalog.Dematic.Pallet.Assemblies
{
    public class PalletStraight : BaseStraight, IControllable
    {
        private PalletStraightInfo palletStraightInfo;
        private bool lineReleaseEventSubcribed = false;

        Experior.Core.Timer ReleaseTimer = new Experior.Core.Timer(0);

        public PalletStraight(PalletStraightInfo info) : base(info)
        {
            try
            {
                palletStraightInfo = info;
                ReleaseTimer.OnElapsed += ReleaseTimer_OnElapsed;
                                
                ConstructDevice = new ConstructDevice(string.Empty);
                LineReleasePhotocellName = palletStraightInfo.LineReleasePhotocellName;
                DeviceInfo deviceInfo = DeviceInfos.Find(i => i.name == LineReleasePhotocellName);
                if (deviceInfo == null)
                {
                    PalletPhotocellInfo photocellInfo = new PalletPhotocellInfo();
                    photocellInfo.name = "LineRelease";
                    photocellInfo.distanceFrom = PositionPoint.End;
                    photocellInfo.distance = 0.125f;
                    photocellInfo.type = ConstructDevice.DeviceTypes["Add Photocell"].Item1; //Item1 is the device type ...obviously!
                    DeviceInfos.Add(photocellInfo);
                }
                ConstructDevice.InsertDevices(this as IConstructDevice);

                SetLineReleasePhotocell();
                LineReleaseEvents(true);

                if (ControlType == ControlTypes.Local)
                {
                    OnLoadArrived += Photocell_OnLoadArrived;
                }
            }
            catch (Exception ex)
            {
                Core.Environment.Log.Write(ex.Message);
            }
        }

        public override void Scene_OnLoaded()
        {
            base.Scene_OnLoaded();
            if (ControllerProperties == null)
            {
                ControllerProperties = StandardCase.SetMHEControl(palletStraightInfo, this);
            }
        }

        public virtual void Photocell_OnLoadArrived(object sender, LoadArrivedEventArgs e)
        {           
        }

        void SetLineReleasePhotocell()
        {
            if (Assemblies != null && Assemblies.Count > 0)
            {
                foreach (Assembly assembly in Assemblies)
                {
                    if (assembly.Name == LineReleasePhotocellName)
                    {
                        LineReleasePhotocell = assembly as PalletPhotocell;
                        LineReleaseEvents(true);
                        return;
                    }
                }
            }
        }

        void ReleaseTimer_OnElapsed(Experior.Core.Timer sender)
        {
            ReleaseTimer.Reset();
            if ((this.NextRouteStatus.Available == RouteStatuses.Available) && (LineReleasePhotocell.PhotocellStatus == PhotocellState.Blocked || LineReleasePhotocell.PhotocellStatus == PhotocellState.LoadBlocked))
            {
                this.RouteAvailable = RouteStatuses.Available;
                StartReleaseTimer();
            }
        }

        [Category("Pallet Straight Configuration")]
        [DisplayName("Line Release Photocell")]
        [TypeConverter(typeof(PhotocellConverter))]
        public string LineReleasePhotocellName { get; set; }

        [Category("Pallet Straight Configuration")]
        public float ReleaseDelay { get; set; }

        public void LineReleaseEvents(bool subscribe)
        {
            if (LineReleasePhotocell == null)
                return;

            if (subscribe && !lineReleaseEventSubcribed) //only subscribe once
            {
                lineReleaseEventSubcribed = true;
                LineReleasePhotocell.OnPhotocellRenamed += LineReleasePhotocell_photocellRenamed;
                LineReleasePhotocell.OnDeviceDeleted += LineReleasePhotocell_PhotocellDeleted;
                LineReleasePhotocell.OnPhotocellStatusChanged += LineReleasePhotocell_OnPhotocellStatusChanged;
            }
            else if (!subscribe && lineReleaseEventSubcribed)
            {
                lineReleaseEventSubcribed = false;
                LineReleasePhotocell.OnDeviceDeleted -= LineReleasePhotocell_PhotocellDeleted;
                LineReleasePhotocell.OnPhotocellRenamed -= LineReleasePhotocell_photocellRenamed;
                LineReleasePhotocell.OnPhotocellStatusChanged -= LineReleasePhotocell_OnPhotocellStatusChanged;
            }
        }

        public virtual void LineReleasePhotocell_OnPhotocellStatusChanged(object sender, PhotocellStatusChangedEventArgs e)
        {
            if (e._PhotocellStatus == PhotocellState.Blocked)
            {
                if (ControlType == ControlTypes.Controller && Controller != null)
                {
                    ThisRouteStatus.Available = RouteStatuses.Blocked;
                }
                else
                {
                    SetLoadWaiting(true, false, e._Load);

                    //Always set this conveyor blocked whenever the load arrives at the photocell, this tells the 
                    //previous conveyor that it has arrived and therefore the next load can be released into it
                    if (NextRouteStatus != null && NextRouteStatus.Available == RouteStatuses.Available && ThisRouteStatus.Available != RouteStatuses.Request)
                    {
                        ThisRouteStatus.Available = RouteStatuses.Blocked;
                        ThisRouteStatus.Available = RouteStatuses.Request; //This means the load can just travel into the next location
                    }
                    else
                    {
                        ThisRouteStatus.Available = RouteStatuses.Blocked;
                    }
                }

                // Always fire the event - it will not do anything however unless it has been subscribed too
                LoadArrived(new LoadArrivedEventArgs(e._Load));
            }
            else if (e._PhotocellStatus == PhotocellState.Clear)
            {
                SetLoadWaiting(false, false, e._Load);
                ThisRouteStatus.Available = RouteStatuses.Request;

                //If the load was deleted then, it does not need to wait to transfer to the next conveyor
                if (e._LoadDeleted)
                {
                    ThisRouteStatus.Available = RouteStatuses.Available;
                }
                if (e._Load != null)
                {
                    LoadLeft(new LoadArrivedEventArgs(e._Load));
                }
            }
        }

        public void ReleaseLoad(Load load)
        {
            if (!((Experior.Dematic.Base.EuroPallet)load).LoadWaitingForWCS)
            {
                SetLoadWaiting(true, false, load);
                if (NextRouteStatus != null && NextRouteStatus.Available == RouteStatuses.Available && ThisRouteStatus.Available != RouteStatuses.Request)
                {
                    ThisRouteStatus.Available = RouteStatuses.Request;
                }
            }
        }

        public override void NextRouteStatus_OnAvailableChanged(object sender, RouteStatusChangedEventArgs e)
        {
            Experior.Dematic.Base.EuroPallet load = (Experior.Dematic.Base.EuroPallet)LineReleasePhotocell.OccupiedLoad;

            if (e._available == RouteStatuses.Available && ThisRouteStatus.Available == RouteStatuses.Blocked) //Move into next position
            {
                if (load != null && !load.LoadWaitingForWCS)
                {
                    ThisRouteStatus.Available = RouteStatuses.Request;
                }
            }
            else if (e._available == RouteStatuses.Blocked && ThisRouteStatus.Available != RouteStatuses.Blocked) //Load has arrived at next position
            {
                ThisRouteStatus.Available = RouteStatuses.Available;
            }
        }

        #region Private Methods

        void StartReleaseTimer()
        {
            if (this.ReleaseDelay != 0)
            {
                ReleaseTimer.Timeout = this.ReleaseDelay;
                ReleaseTimer.Start();
            }
        }

        void LineReleasePhotocell_photocellRenamed(object sender, PhotocellRenamedEventArgs e)
        {
            LineReleasePhotocellName = e._NewName;
        }

        void LineReleasePhotocell_PhotocellDeleted(object sender, EventArgs e)
        {
            LineReleaseEvents(false);
            LineReleasePhotocell = null;
            LineReleasePhotocellName = "";
        }

        #endregion

        #region Properties

        [Browsable(false)]
        private PalletPhotocell lineReleasePhotocell;
        [Browsable(false)]
        public PalletPhotocell LineReleasePhotocell
        {
            get { return lineReleasePhotocell; }
            set
            {
                lineReleasePhotocell = value;
                LineReleaseEvents(true);
            }
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
            palletStraightInfo.ProtocolInfo = null;
        }

        void controller_OnControllerRenamedEvent(object sender, EventArgs e)
        {
            palletStraightInfo.ControllerName = ((Assembly)sender).Name;
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
                return palletStraightInfo.ControlType;
            }
            set
            {
                palletStraightInfo.ControlType = value;
                if (ControllerProperties != null && value != ControlTypes.Controller)
                {
                    ControllerName = "No Controller";
                    OnLoadArrived += Photocell_OnLoadArrived;
                }
                else
                {
                    OnLoadArrived -= Photocell_OnLoadArrived;
                }

                Core.Environment.Properties.Refresh();
            }
        }

        public void DynamicPropertyAssemblyPLCconfig(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = Controller != null;
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
                return palletStraightInfo.ControllerName;
            }
            set
            {
                if (!value.Equals(palletStraightInfo.ControllerName))
                {
                    ControllerProperties = null;
                    palletStraightInfo.ProtocolInfo = null;
                    Controller = null;
                }

                palletStraightInfo.ControllerName = value;
                if (value != null)
                {
                    //Note StandardCase.SetMHEControl will set the ControllerProperties and Controller properties!
                    ControllerProperties = StandardCase.SetMHEControl(palletStraightInfo, this);
                    if (ControllerProperties == null)
                    {
                        palletStraightInfo.ControllerName = "No Controller";
                    }
                }
            }
        }

        public void DynamicPropertyControllers(Core.Properties.PropertyAttributes attributes)
        {
            attributes.IsBrowsable = palletStraightInfo.ControlType == ControlTypes.Controller;
        }

        #endregion

        public override void Dispose()
        {
            LineReleaseEvents(false);
            ReleaseTimer.OnElapsed -= ReleaseTimer_OnElapsed;
            //if (ControlType == ControlTypes.Local)
            //{
            //    OnLoadArrived -= Photocell_OnLoadArrived;
            //}
            base.Dispose();
        }


    }

    [Serializable]
    [XmlInclude(typeof(PalletStraightInfo))]
    public class PalletStraightInfo : BaseStraightInfo, IControllableInfo
    {
        public string LineReleasePhotocellName = "LineRelease";
        public float ReleaseDelay = 0;
        public float LoadWaitingDelay = 0;
        public bool ScriptRelease = false;

        #region IControllable Properties

        public ControlTypes ControlType;

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

        #endregion
    }

}
