using Experior.Catalog.Dematic.Case.Devices;
using Experior.Core.Assemblies;
using Experior.Core.Loads;
using Experior.Core.Properties;
using Experior.Core.Properties.TypeConverter;
using Experior.Core.Routes;
using Experior.Dematic.Base;
using Experior.Dematic.Base.Devices;
using Microsoft.DirectX;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Xml.Serialization;

namespace Experior.Catalog.Dematic.Case.Components
{
    public class AccumulationSensor 
    {
        //public Sensor sensor = new Sensor();
        public DematicSensor sensor = new DematicSensor();

        public Core.Parts.Cube cube = new Core.Parts.Cube(Color.Yellow, 0.06f, 0.08f, 0.06f);
        public string locName; // This is used in (e.g.) multishuttle infeed to give this location another name - this is different from its .name;
    }

    public class StraightAccumulationConveyor : StraightConveyor, IControllable
    {        
        public StraightAccumulationConveyorInfo straightAccumulationinfo;
        public List<AccumulationSensor> sensors;
        public event EventHandler OnConveyorLoaded; //Types that derive from this type should use this event to set up the derived conveyor
        public bool RouteAvailableOverride = false;
        private IController controller;

        //Picking
        public event EventHandler<ManualPickArrivalArgs> OnArrivedAtPickingPosition; // subcribed to by a controller
        public static event EventHandler<ManualPickCompleteArgs> OnManualPickingCompleteRoutingScript; // subcribed to by a routingscript (This is called regardless of the controller being used)

        //TODO Add Local control to picking point - Just have a timer that delays at the location for X seconds then releases the load

        private Core.Timer BlockedTimer;
        private Core.Timer ClearTimer;


        public StraightAccumulationConveyor(StraightAccumulationConveyorInfo info) : base(info)
        {            
            straightAccumulationinfo = info;

            if (straightAccumulationinfo.blockedTimeout != 0)
            {
                BlockedTimer = new Core.Timer(straightAccumulationinfo.blockedTimeout);
                BlockedTimer.AutoReset = false;
                BlockedTimer.OnElapsed += BlockedTimer_OnElapsed;
            }

            if (straightAccumulationinfo.clearTimeout != 0)
            {
                ClearTimer = new Core.Timer(straightAccumulationinfo.clearTimeout);
                ClearTimer.AutoReset = false;
                ClearTimer.OnElapsed += ClearTimer_OnElapsed;
            }

            RollAngle = info.rollAngle;
            AccPitch = info.Pitch;
            OutfeedSection = info.OutfeedSection;
            UpdateConveyor();

            //The scene is not loading therefore it is loaded from the catalog
            if (!Core.Environment.Scene.Loading)
            {
               
            }
        }

        public override Image Image
        {
            get
            {
                return Common.Icons.Get("accumulation");
            }
        }

        public override void Scene_OnLoaded()
        {
            base.Scene_OnLoaded();
            UpdateConveyor();

            if (ControllerProperties == null)
            {
                ControllerProperties = StandardCase.SetMHEControl(straightAccumulationinfo, this);
            }
        }

        public override void Dispose()
        {
            DisposeSensors();
            base.Dispose();
        }

        public override void Reset()
        {
            UpdatePhotocellColours();
            RouteAvailable = RouteStatuses.Available;

            base.Reset();
        }

        #region Control Logic
        public override void NextRouteStatus_OnAvailableChanged(object sender, RouteStatusChangedEventArgs e) 
        {
            if (e._available == RouteStatuses.Available)
            {
                if (sensors != null && sensors.Count > 0)
                {
                    if (sensors[0].sensor.Active && PickPosition != 1) //If pick position is the front position then the controller needs to monitor the ongoing conveyor and handle the release.
                    {
                        if (sensors[0].sensor.LoadWaiting)
                        {
                            ((Case_Load)sensors[0].sensor.ActiveLoad).ReleaseLoad_PLCControl();
                        }
                    }
                }
            }
            else
            {
                //Don't do anything here for accumulation conveyor
            }

            //Pass through Route available status of the line full is not setup
            if (LineFullPosition == 0 && LineFullPassthrough)
            {
                RouteAvailable = e._available;
            }
        }

        public void sensor_OnEnter(DematicSensor sender, Load load)
        {
            if (sender.PreviousActive)
            {
                BlockChargeCheck();
                return;
            }
            
            int result;
            if (int.TryParse(sender.Name, out result))
            {
                sensors[result].cube.Color = Color.Red;

                //Check if the location is a picking position 
                if (result == (PickPosition - 1))
                {
                    load.Stop();
                    if (OnArrivedAtPickingPosition != null)
                        OnArrivedAtPickingPosition(this, new ManualPickArrivalArgs(load));
                }
    
                if (result != 0) // photocells are named 0 to n with 0 being the front photocell
                {
                    //bool stopLoad = false;
                    if (InfeedBlockChargePositions == 0 || result < (Positions - InfeedBlockChargePositions + 1))
                    {
                        //Normal Position
                        if (sensors[result - 1].sensor.Active) //Active means that there is a load on the sensor
                        {
                            //stopLoad = true;
                            load.Stop();
                        }
                    }
                    if (result >= (Positions - InfeedBlockChargePositions))
                    {
                        //Block Position
                        BlockChargeCheck();
                    }
                }
                else //Load has arrived at front (zeroth) photocell 
                {
                    SetLoadWaiting(true, false, load);
                    if (NextRouteStatus == null || NextRouteStatus.Available != RouteStatuses.Available)
                    {
                        ((Case_Load)load).StopLoad_PLCControl();
                        //load.Stop();
                    }
                }

                if (InfeedBlockChargePositions == 0 || result < (Positions - InfeedBlockChargePositions)) //Normal positions
                {
                    //Stops the load behind if it's active to create a gap
                    if (sensors.Count > (result + 1) && sensors[result + 1].sensor.Active)
                    {
                        sensors[result + 1].sensor.StopActiveLoads();
                    }
                }

                //Set the route available status or start the timer
                if (LineFullPosition != 0 && result == LineFullPosition - 1)
                {
                    if (ClearTimer != null)
                    {
                        ClearTimer.Stop();
                        ClearTimer.Reset();
                    }
                    if (BlockedTimeout == 0)
                    {
                        RouteAvailable = RouteStatuses.Blocked;
                    }
                    else
                    {
                        BlockedTimer.Start();
                    }
                }
            }
        }

        protected void sensor_OnLeave(DematicSensor sender, Load load)
        {
            if (sender.PreviousActiveLoad == load)
            {
                BlockChargeCheck();
                return;
            }

            int result;
            if (int.TryParse(sender.Name, out result))
            {
                if (result == LineFullPosition - 1)
                    sensors[result].cube.Color = Color.Blue;
                else if (PickPosition != 0 && result == PickPosition - 1)
                    sensors[result].cube.Color = Color.DarkGreen;
                else if (result < (Positions - InfeedBlockChargePositions))
                    sensors[result].cube.Color = Color.Yellow;
                else
                    sensors[result].cube.Color = Color.Orange;

                if (InfeedBlockChargePositions == 0 || result < (Positions - InfeedBlockChargePositions)) //Normal position (not block charge)
                {
                    //If there is a sensor behind you and it has a load and this sensor has been cleared then release the one behind
                    if (sensors.Count > (result + 1) && sensors[result + 1].sensor.Active)
                    {
                        if ((result + 1) != (PickPosition - 1) || ReleasePickLoad) // Also check if the position has stopped due to picking operation
                        {
                            ((Case_Load)sensors[result + 1].sensor.ActiveLoad).ReleaseLoad();
                            if (ReleasePickLoad) //Added by BG but shouldn't make any difference but it might
                            {
                                ReleasePickLoad = false;
                            }
                        }
                    }
                }
                else //Block Charge Position
                {
                    //Check all the positions behind and release the load if active
                    for (int i = result + 1; i < Positions; i++)
                    {
                        if (i != (PickPosition - 1) || ReleasePickLoad) // Also check if the position has stopped due to picking operation
                            sensors[i].sensor.ReleaseActiveCaseLoads();
                    }
                }
                
                if (result == 0) //Load has cleared front photocell
                {
                    if (sensors[0].sensor.ActiveLoad != null)
                        SetLoadWaiting(false, sensors[0].sensor.ActiveLoad.StartDisposing, load);
                    else
                        SetLoadWaiting(false, false, load);
                }

                //Set the route available status or start the timer
                if (LineFullPosition != 0 && result == LineFullPosition - 1)
                {
                    if (BlockedTimer != null)
                    {
                        BlockedTimer.Stop();
                        BlockedTimer.Reset();
                    }
                    if (LineAvailableTimeout == 0)
                    {
                        RouteAvailable = RouteStatuses.Available;
                    }
                    else
                    {
                        ClearTimer.Start();
                    }
                }
            }
        }

        private void BlockChargeCheck()
        {
            if (InfeedBlockChargePositions == 0)
                return;

            //Check each block charge position (in reverse order) and stop load if no block charge locations in front are
            //clear and release load if any one location in front is clear
            for (int i = Positions - 1; i > Positions - InfeedBlockChargePositions; i--)
            {
                if (sensors[i].sensor.Active)
                {
                    bool ClearInFront = false;
                    for (int f = i - 1; f >= Positions - InfeedBlockChargePositions; f--)
                    {
                        if (!sensors[f].sensor.Active)
                        {
                            ClearInFront = true;
                            break;
                        }
                    }
                    if (ClearInFront)
                    {
                        if (i != (PickPosition - 1) || ReleasePickLoad) // Also check if the position has stopped due to picking operation
                            sensors[i].sensor.ReleaseActiveCaseLoads();
                    }
                    else
                    {
                        sensors[i].sensor.StopActiveLoads();
                    }
                }
            }
        }

        private bool _ReleasePickLoad;
        [Browsable(false)]
        public bool ReleasePickLoad
        {
            get { return _ReleasePickLoad; }
            set
            {
                if (!value)
                {
                    _ReleasePickLoad = false;
                }
                else
                {
                    //Check if the position in front is clear and release the load if it is else set this and release the load when the position becomes clear
                    if (!sensors[PickPosition-2].sensor.Active) //The position after the pick position is not occupied
                    {
                        if (sensors[PickPosition-1].sensor.Active) //The pick position
                        {
                            sensors[PickPosition - 1].sensor.ReleaseActiveCaseLoads();
                        }
                        else
                        {
                            ReleasePickLoad = false; //If there is nothing in the pick position then set the release false
                        }
                    }
                    else //The position after the pick position is occupied
                    {
                        _ReleasePickLoad = true; //Release load when location in front becomes clear (Leaving of position in front)
                        if (OnManualPickingCompleteRoutingScript != null)
                        {
                            OnManualPickingCompleteRoutingScript(this, new ManualPickCompleteArgs(sensors[PickPosition - 1].sensor.ActiveLoad));
                        }
                    }
                }
            }
        }


        void BlockedTimer_OnElapsed(Core.Timer sender)
        {
            BlockedTimer.Stop();
            BlockedTimer.Reset();
            RouteAvailable = RouteStatuses.Blocked;
        }

        void ClearTimer_OnElapsed(Core.Timer sender)
        {
            ClearTimer.Stop();
            ClearTimer.Reset();
            RouteAvailable = RouteStatuses.Available;
        }

        #endregion

        #region IController
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
                controller = value;
                if (controller != null)
                {
                    controller.OnControllerDeletedEvent += controller_OnControllerDeletedEvent;
                    controller.OnControllerRenamedEvent += controller_OnControllerRenamedEvent;
                }
                Core.Environment.Properties.Refresh();
            }
        }

        private void controller_OnControllerDeletedEvent(object sender, EventArgs e)
        {
            controller.OnControllerDeletedEvent -= controller_OnControllerDeletedEvent;
            ControllerName = "No Controller";
            Controller = null;
            ControllerProperties = null;
        }

        void controller_OnControllerRenamedEvent(object sender, EventArgs e)
        {
            straightAccumulationinfo.ControllerName = ((Assembly)sender).Name;
            Core.Environment.Properties.Refresh();
        }
        #endregion
        
        #region User Interface


        [Category("Position")]
        [DisplayName("Roll")]
        [Description("The Roll of the conveyor, used for tilting a accumilation conveyor")]
        [PropertyOrder(7)]
        [TypeConverter(typeof(Rad2AngleConverter))]
        public float RollAngle
        {
            get { return straightAccumulationinfo.rollAngle; }
            set 
            {
                straightAccumulationinfo.rollAngle = value;
                Roll = value;
            }
        }

        protected int pitch;

        [Category("Configuration")]
        [DisplayName("Accumulation Pitch")]
        [Description("Pitch of the accumulation conveyor places, based on standard Dematic conveyors")]
        [PropertyOrder(0)]
        public virtual AccumulationPitch AccPitch
        {
            get { return straightAccumulationinfo.Pitch; }
            set
            {
                straightAccumulationinfo.Pitch = value;
                if (value != AccumulationPitch._Custom)
                {
                    pitch = (int)value;
                }
                else
                {
                    pitch = (int)(CustomPitch * 1000);
                }
                SetAccumulationLength(false);
            }
        }

        [Category("Configuration")]
        [DisplayName("Custom Pitch")]
        [Description("Custom accumulation pitch of the accumulation conveyor places")]
        [PropertyOrder(1)]
        [PropertyAttributesProvider("DynamicPropertyCustomPitch")]
        public virtual float CustomPitch
        {
            get { return straightAccumulationinfo.CustomPitch; }
            set
            {
                straightAccumulationinfo.CustomPitch = value;
                pitch = (int)(value * 1000);
                SetAccumulationLength(false);
            }
        }

        public void DynamicPropertyCustomPitch(Core.Properties.PropertyAttributes attributes)
        {
            attributes.IsBrowsable = AccPitch == AccumulationPitch._Custom;
        }

        [Category("Configuration")]
        [DisplayName("Accumulation Positions")]
        [Description("Number of accumulation positions, Position Colours\nYellow: Standard accumulation Position\nBlue: Line Full Position\nGreen: Picking position\nRed: Position occupied")]
        [PropertyOrder(2)]
        public int Positions
        {
            get { return straightAccumulationinfo.Positions; }
            set
            {
                straightAccumulationinfo.Positions = value;
                SetAccumulationLength(true);
            }
        }

        protected int outfeedSection;

        [Category("Configuration")]
        [DisplayName("Outfeed Section Length")]
        [Description("Length of the outfeed roller section in front of the first accumulation position")]
        [PropertyOrder(3)]
        public virtual OutfeedLength OutfeedSection
        {
            get { return straightAccumulationinfo.OutfeedSection; }
            set
            {
                straightAccumulationinfo.OutfeedSection = value;
                outfeedSection = (int)value;
                SetAccumulationLength(false);
            }
        }

        [Category("Configuration")]
        [DisplayName("Infeed Section Length")]
        [Description("Length of the infeed roller secrion before the last accumulation position")]
        [PropertyOrder(4)]
        public virtual float InfeedSection
        {
            get { return straightAccumulationinfo.InfeedSection; }
            set
            {
                straightAccumulationinfo.InfeedSection = value;
                SetAccumulationLength(false);
            }
        }

        [Category("Line Full Photocell")]
        [DisplayName("Accumulation Position")]
        [Description("Photocell position used to indicate linefull")]
        [PropertyOrder(5)]
        public int LineFullPosition
        {
            get { return straightAccumulationinfo.LineFullPosition; }
            set
            {
                if (value <= Positions && value > -1)
                {
                    straightAccumulationinfo.LineFullPosition = value;
                    Core.Environment.Properties.Refresh();
                    UpdatePhotocellColours();
                }
            }
        }

        public void DynamicPropertyLineFullZero(Core.Properties.PropertyAttributes attributes)
        {
            attributes.IsBrowsable = LineFullPosition == 0;
        }

        [Category("Line Full Photocell")]
        [DisplayName("Passthrough Linefull")]
        [Description("Set this true if this conveyor should be set line full status as the conveyor in front. Only available if the accumulation position is set to 0")]
        [PropertyAttributesProvider("DynamicPropertyLineFullZero")]
        [PropertyOrder(6)]
        public bool LineFullPassthrough
        {
            get { return straightAccumulationinfo.lineFullPassthrough; }
            set
            {
                straightAccumulationinfo.lineFullPassthrough = value;
            }
        }

        [Category("Line Full Photocell")]
        [DisplayName("Blocked Timeout")]
        [Description("How many seconds should the photocell be covered before being set blocked. Note if set to 0 photocell becomes blocked as soon as soon as a load arrives")]
        [PropertyOrder(1)]
        public float BlockedTimeout
        {
            get { return straightAccumulationinfo.blockedTimeout; }
            set
            {
                straightAccumulationinfo.blockedTimeout = value;

                if (value == 0)
                {
                    if (BlockedTimer != null)
                    {
                        BlockedTimer.Stop();
                        BlockedTimer.OnElapsed -= BlockedTimer_OnElapsed;
                        BlockedTimer = null;
                    }
                }
                else
                {
                    if (BlockedTimer == null)
                    {
                        BlockedTimer = new Core.Timer(value);
                        BlockedTimer.OnElapsed += BlockedTimer_OnElapsed;
                    }
                    else
                    {
                        if (BlockedTimer.Running)
                        {
                            BlockedTimer.Stop();
                            BlockedTimer.Timeout = value;
                            BlockedTimer.Start();
                        }
                        else
                            BlockedTimer.Timeout = value;
                    }
                }
            }
        }

        [Category("Line Full Photocell")]
        [DisplayName("Clear Timeout")]
        [Description("How many seconds should the photocell be uncovered before being set clear. Note if set to 0 photocell becomes clear as soon as soon as a load leaves")]
        [PropertyOrder(2)]
        public float LineAvailableTimeout
        {
            get { return straightAccumulationinfo.clearTimeout; }
            set
            {
                straightAccumulationinfo.clearTimeout = value;
                if (value == 0)
                {
                    if (ClearTimer != null)
                    {
                        ClearTimer.Stop();
                        ClearTimer.OnElapsed -= ClearTimer_OnElapsed;
                        ClearTimer = null;
                    }
                }
                else
                {
                    if (ClearTimer == null)
                    {
                        ClearTimer = new Core.Timer(value);
                        ClearTimer.OnElapsed += ClearTimer_OnElapsed;
                    }
                    else
                    {
                        if (ClearTimer.Running)
                        {
                            ClearTimer.Stop();
                            ClearTimer.Timeout = value;
                            ClearTimer.Start();
                        }
                        else
                            ClearTimer.Timeout = value;
                    }
                }
            }
        }

        [Category("Configuration")]
        [DisplayName("Infeed Block Charge Positions")]
        [Description("Number of positions on the infeed that are slugged (used to qiuckly clear highspeed divert outfeeds e.g. DHDM)")]
        [PropertyOrder(5)]
        public int InfeedBlockChargePositions
        {
            get { return straightAccumulationinfo.InfeedBlockChargePositions; }
            set
            {
                if (value < Positions)
                {
                    straightAccumulationinfo.InfeedBlockChargePositions = value;
                    UpdatePhotocellColours();
                }
            }
        }

        [Category("Routing")]
        [DisplayName("Picking Position")]
        [Description("Select which position on accumulation conveyor us being used as a picking position. 0 = No pick position. Note: Accumulation position 1 cannot be set to a pick position")]
        [PropertyOrder(1)]
        public int PickPosition
        {
            get { return straightAccumulationinfo.PickingPosition; }
            set
            {
                if (value == 0 || (value > 1 && value <= Positions))
                {
                    straightAccumulationinfo.PickingPosition = value;
                    UpdatePhotocellColours();
                }

                Core.Environment.Properties.Refresh();
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
                return straightAccumulationinfo.ControlType;
            }
            set
            {
                straightAccumulationinfo.ControlType = value;
                Core.Environment.Properties.Refresh();
            }
        }

        [Category("Routing")]
        [DisplayName("Controller")]
        [Description("Controller name that handles the picking position control")]
        [PropertyAttributesProvider("DynamicPropertyControllers")]
        [PropertyOrder(2)]
        [TypeConverter(typeof(CaseControllerConverter))]
        public string ControllerName
        {
            get
            {
                return straightAccumulationinfo.ControllerName;
            }
            set
            {
                if (!value.Equals(straightAccumulationinfo.ControllerName))
                {
                    ControllerProperties = null;
                    straightAccumulationinfo.ProtocolInfo = null;
                }

                straightAccumulationinfo.ControllerName = value;
                if (value != null)
                {
                    ControllerProperties = StandardCase.SetMHEControl(straightAccumulationinfo, this);
                }
            }
        }

        //public void DynamicPropertyControllers(Core.Properties.PropertyAttributes attributes)
        //{
        //    attributes.IsBrowsable = PickPosition > 0;
        //}

        public void DynamicPropertyControllers(Core.Properties.PropertyAttributes attributes)
        {
            attributes.IsBrowsable = straightAccumulationinfo.ControlType == ControlTypes.Controller;
        }

        private MHEControl controllerProperties;
        [Category("Routing")]
        [DisplayName("Control")]
        [Description("Embedded routing control with protocol and routing specific configuration")]
        [PropertyOrder(3)]
        [PropertyAttributesProvider("DynamicPropertyAssemblyPLCconfig")]
        public MHEControl ControllerProperties
        {
            set
            {
                controllerProperties = value;
            }
            get
            {
                return controllerProperties;
            }
        }

        public void DynamicPropertyAssemblyPLCconfig(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = Controller != null;
        }

        [Category("Size and Speed")]
        [DisplayName("Length")]
        [Description("Length of the conveyor (meter)")]
        [PropertyOrder(1)]
        [TypeConverter()]
        [ReadOnly(true)]
        public override float Length
        {
            get
            {
                return base.Length;
            }
            set
            {
                base.Length = value;
                UpdateLength(value);
                Core.Environment.Invoke(new Action(UpdateConveyor));
            }
        }

        [ReadOnly(true)]
        public override Vector3 PositionEnd
        {
            get
            {
                return base.PositionEnd;
            }
            set
            {
            }
        }

        [ReadOnly(true)]
        public override Vector3 PositionStart
        {
            get
            {
                return base.PositionStart;
            }
            set
            {
            }
        }

        
        #endregion

        #region Helper Methods

        private void DisposeSensors()
        {
            if (sensors != null && sensors.Count > 0)
            {
                foreach (AccumulationSensor accumSensor in sensors)
                {
                    accumSensor.sensor.OnEnter -= sensor_OnEnter;
                    accumSensor.sensor.OnLeave -= sensor_OnLeave;
                    accumSensor.sensor.Dispose();
                    accumSensor.cube.Dispose();
                }
                sensors.Clear();
            }
        }

        protected void SetAccumulationLength(bool positionUpdate)
        {
            if (LineFullPosition != 0 && positionUpdate)
            {
                //Calculate what the Line Full Photocell position should be
                int lfPosition = Positions - 1;
                if (lfPosition < 1)
                {
                    lfPosition = 1;
                }
                LineFullPosition = lfPosition;
            }
            
            //Check that the picking position is still valid
            if (PickPosition != 0 && PickPosition > Positions)
            {
                PickPosition = Positions;
            }

            //Length = ((float)((Positions * (int)AccPitch) + (int)OutfeedSection) / 1000) + InfeedSection;
            Length = ((float)((Positions * pitch) + outfeedSection) / 1000) + InfeedSection;

            
            //UpdatePhotocellColours();
        }

        void UpdatePhotocellColours()
        {
            if (sensors != null && sensors.Count == Positions) //Only do this if all sensors have been created
            {
                for (int i = 0; i < Positions; i++)
                {
                    if (i >= (Positions - InfeedBlockChargePositions))
                    {
                        sensors[i].cube.Color = Color.Orange;
                    }
                    else
                    {
                        sensors[i].cube.Color = Color.Yellow;
                    }
                }
                if (PickPosition != 0)
                    sensors[PickPosition - 1].cube.Color = Color.DarkGreen;

                if (LineFullPosition != 0)
                {
                    sensors[LineFullPosition - 1].cube.Color = Color.Blue;
                }
            }
        }

        public override void UpdateConveyor()
        {
            //Calculate how many positions there are on the conveyor
            //int pitch = (int)AccPitch;
            float fPitch = (float)pitch / 1000;
            int length = (int)(Length * 1000);
            int sensorCount = Positions;

            if (sensors == null)
            {
                sensors = new List<AccumulationSensor>();
                for (int x = 1; x <= sensorCount; x++)
                {
                    AccumulationSensor accumSensor = new AccumulationSensor();
                    sensors.Add(accumSensor);
                }
            }
            else if (sensorCount > sensors.Count)
            {
                int sensorDifference = sensorCount - sensors.Count;
                for (int x = 1; x <= sensorDifference; x++)
                {
                    AccumulationSensor accumSensor = new AccumulationSensor();
                    sensors.Add(accumSensor);
                }
            }
            else if (sensorCount < sensors.Count)
            {
                for (int x = sensors.Count; x > sensorCount; x--)
                {
                    AccumulationSensor s = sensors[x - 1];
                    sensors.RemoveAt(x - 1);

                    s.sensor.OnEnter -= sensor_OnEnter;
                    s.sensor.OnLeave -= sensor_OnLeave;
                    s.cube.Dispose();
                    s.sensor.Dispose();
                }
            }

            int i = 0;
            foreach (AccumulationSensor accumSensor in sensors)
            {
                //float outfeedSection = (float)OutfeedSection / 1000;
                float foutfeedSection = (float)outfeedSection / 1000;

                accumSensor.sensor.Distance = Length - (foutfeedSection + (i * fPitch));
                accumSensor.sensor.Visible = false;
                accumSensor.sensor.Color = Color.Gray;
                accumSensor.sensor.Edge = ActionPoint.Edges.Leading;
                accumSensor.sensor.Name = string.Format("{0}", i.ToString());

                TransportSection.Route.InsertActionPoint(accumSensor.sensor);

                Add(accumSensor.cube);
                accumSensor.cube.LocalPosition = new Vector3(-Length / 2 + (foutfeedSection + (i * fPitch)), 0, -Width / 2 + 0.015f);

                accumSensor.sensor.OnEnter -= sensor_OnEnter;
                accumSensor.sensor.OnLeave -= sensor_OnLeave;
                accumSensor.sensor.OnEnter += sensor_OnEnter;
                accumSensor.sensor.OnLeave += sensor_OnLeave;

                i++;
            }

            if (OnConveyorLoaded != null)
            {
                OnConveyorLoaded(this, new EventArgs());
            }
            UpdatePhotocellColours();
        }
        #endregion
    }

    
    

    [Serializable]
    [XmlInclude(typeof(StraightAccumulationConveyorInfo))]
    public class StraightAccumulationConveyorInfo : StraightConveyorInfo, IControllableInfo
    {
        //Specific case straight belt conveyor info to be added here
        public bool CreateLineFull = true; //when using the accumulation as part of another assembly such as a multishuttle then the line full photocell is ommitted.
        //public string LineFullPhotocellName = "LineFull";
        public int LineFullPosition = 1;
        public bool lineFullPassthrough = false;
        public AccumulationPitch Pitch = AccumulationPitch._750mm;
        public float CustomPitch = 0.75f;
        public int Positions = 3;
        public OutfeedLength OutfeedSection = OutfeedLength._125mm;      
        public float InfeedSection;
        public int InfeedBlockChargePositions = 0;
        public float blockedTimeout = 1;
        public float clearTimeout = 2;
        public int PickingPosition = 0;
        public ControlTypes ControlType;
        public float rollAngle = 0;
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
    }

    public class ManualPickArrivalArgs : EventArgs
    {
        public readonly Load _load;

        public ManualPickArrivalArgs(Load load)
        {
            _load = load;
        }
    }

    public class ManualPickCompleteArgs : EventArgs
    {
        public readonly Load _load;

        public ManualPickCompleteArgs(Load load)
        {
            _load = load;
        }
    }
}
