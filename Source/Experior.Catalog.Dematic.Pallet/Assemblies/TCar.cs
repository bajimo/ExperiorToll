using System;
using System.Drawing;
using Experior.Core.Assemblies;
using Experior.Core.TransportSections;
using System.ComponentModel;
using Experior.Core.Properties;
using Experior.Core.Properties.TypeConverter;
using Experior.Core.Loads;
using Microsoft.DirectX;
using Experior.Core.Parts;
using Experior.Dematic.Base;
using Experior.Dematic.Base.Devices;
using Experior.Core.Mathematics;
using System.Xml.Serialization;
using Experior.Core.Routes;
using System.Collections.ObjectModel;
using System.Linq;
using System.Collections.Generic;

namespace Experior.Catalog.Dematic.Pallet.Assemblies
{
    public class TCar : Assembly, IRouteStatus, IControllable
    {
        private TCarInfo tCarInfo;
        private StraightTransportSection track;
        private Cube trackRight;
        private PalletStraight conveyor;
        private readonly Load trackLoad;
        private string[] sourceList;
        private string[] destinationList;
        private ActionPoint trackStopPoint;
        private bool busy;
        private float furthestFixPoint;
        public Dictionary<string, DematicFixPoint> SourceFixPoints = new Dictionary<string, DematicFixPoint>();

        public ObservableCollection<TCarTask> Tasks = new ObservableCollection<TCarTask>();

        public delegate void SourceArrival(Load load, DematicFixPoint sourceFixPoint);
        /// <summary>
        /// The MHE control object will assign an "ap_enter" method in its contructor
        /// </summary>
        public SourceArrival sourceArrival;

        //This is the event that can be subscribed to in the routing script
        public delegate void OnSourceLoadArrivedEvent(TCar sender, Load load, DematicFixPoint sourceFixPoint);
        public static event OnSourceLoadArrivedEvent OnSourceLoadArrived;

        private void SourceLoadArrived(Load load, DematicFixPoint sourceFixPoint)
        {
            OnSourceLoadArrived?.Invoke(this, load, sourceFixPoint);
        }

        public delegate void OnDestinationStatusChangedEvent(TCar sender, RouteStatus routeStatus);
        public static event OnDestinationStatusChangedEvent OnDestinationStatusChanged;

        private void DestinationStatusChanged(RouteStatus routeStatus)
        {
            OnDestinationStatusChanged?.Invoke(this, routeStatus);
        }

        public TCar(TCarInfo info) : base(info)
        {
            tCarInfo = info;
            info.height = info.height * 2; // assembley is placed at height/2 so that it comes out at height ?!!??

            Core.Environment.Scene.OnLoaded += Scene_OnLoaded;

            var offCentre = TCarWidth / 2 - TCarWidth;

            // Left track used to move the load
            track = new StraightTransportSection(Color.Gray, TCarLength, 0.05f, 0.05f);
            Add(track);
            track.Route.Motor.Speed = 1; // m/s ?
            track.LocalPosition = new Vector3(-TCarLength / 2, -0.05f, offCentre);

            // Load Vehicle
            trackLoad = Core.Loads.Load.CreateBox(0.1f, 0.1f, 0.1f, Color.BlueViolet);
            trackLoad.Embedded = true;
            trackLoad.Deletable = false;
            track.Route.Add(trackLoad);
            trackLoad.OnPositionChanged += TrackLoad_OnPositionChanged;
            trackLoad.Stop();
            trackLoad.Visible = false;

            // Right track (visual only)
            trackRight = new Cube(Color.Gray, TCarLength, 0.05f, 0.05f);
            Add(trackRight);
            trackRight.LocalPosition = new Vector3(-TCarLength / 2, -0.05f, -offCentre);

            // Action point for lift rail
            trackStopPoint = track.Route.InsertActionPoint(0);
            trackStopPoint.Color = Color.Black;
            trackStopPoint.Visible = false;
            trackStopPoint.OnEnter += TrackStopPoint_OnEnter;

            // Conveyor
            PalletStraightInfo straightInfo = new PalletStraightInfo
            {
                ConveyorType = PalletConveyorType.Roller,
                thickness = 0.05f,
                spacing = 0.1f,
                width = ConveyorWidth,
                length = TCarWidth,
                speed = 0.3f,
                color = tCarInfo.color,
            };
            conveyor = new PalletStraight(straightInfo);
            conveyor.LineReleasePhotocell.OnPhotocellStatusChanged += LineReleasePhotocell_OnPhotocellStatusChanged;
            conveyor.ThisRouteStatus.OnRouteStatusChanged += ThisRouteStatus_OnRouteStatusChanged;
            conveyor.Entering.Name = "EnterPoint";
            conveyor.Leaving.Name = "ExitPoint";
            conveyor.StartFixPoint.Visible = false;
            conveyor.EndFixPoint.Visible = false;
            Add(conveyor);
            conveyor.LocalPosition = new Vector3(-conveyor.Width / 2, 0, 0);
            conveyor.LocalYaw = Trigonometry.PI(Trigonometry.Angle2Rad(90.0f));

            SetupFixPoints();

            Tasks.CollectionChanged += Tasks_CollectionChanged;

            Reset();
        }
        private void SetupFixPoints()
        {
            RemoveAllDematicFixpoints();
            furthestFixPoint = 0; // Reset furthest fix point as might have changed

            var leftPosition = conveyor.EndFixPoint.LocalPosition.X;
            var rightPosition = conveyor.StartFixPoint.LocalPosition.X;

            // Add Source fix points
            if (tCarInfo.Source != null)
            {
                sourceList = tCarInfo.Source.Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries);
                AddFixPoints(sourceList, LoadDirection.Source, leftPosition, rightPosition);
            }
            // Add Destination fix points
            if (tCarInfo.Destination != null)
            {
                destinationList = tCarInfo.Destination.Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries);
                AddFixPoints(destinationList, LoadDirection.Destination, leftPosition, rightPosition);
            }
            // Adjust the length of the TCar so it's always long enough for the Fixpoints
            TCarLength = furthestFixPoint + (TCarWidth / 2);
        }

        private void AddFixPoints(string[] fixPointList, LoadDirection direction, float leftPosition, float rightPosition)
        {
            var color = direction == LoadDirection.Source ? Color.Red : Color.Blue;
            var fixPointType = direction == LoadDirection.Source ? FixPoint.Types.Start : FixPoint.Types.End;
            foreach (var fixPoint in fixPointList)
            {
                var localYaw = Trigonometry.PI(Trigonometry.Angle2Rad(90.0f));
                var fixPointArray = fixPoint.Split(':');
                var side = fixPointArray[0];
                var name = fixPointArray.Length > 2 ? fixPointArray[2] : "";
                var trackSide = string.Equals("R", side) ? rightPosition : leftPosition;
                if (string.Equals("R", side) && direction == LoadDirection.Source)
                {
                    localYaw = Trigonometry.PI(Trigonometry.Angle2Rad(270.0f));
                } 
                if (string.Equals("L", side) && direction == LoadDirection.Destination)
                {
                    localYaw = Trigonometry.PI(Trigonometry.Angle2Rad(270.0f));
                }
                var trackPosition = TCarLength / 2 - 0.05f; // Default value to be replaced by TryParse
                var isFloat = float.TryParse(fixPointArray[1], System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.CreateSpecificCulture("en-GB"), out trackPosition);
                if (isFloat)
                {
                    // Find out which fixpoint is the furthest
                    furthestFixPoint = furthestFixPoint < trackPosition ? trackPosition : furthestFixPoint;
                    DematicFixPoint fp = new DematicFixPoint(color, fixPointType, this);
                    fp.Name = name;
                    if (direction == LoadDirection.Source)
                    {
                        fp.OnSnapped += new FixPoint.SnappedEvent(SourceFixPoint_OnSnapped);
                        fp.OnUnSnapped += new FixPoint.UnSnappedEvent(SourceFixPoint_OnUnSnapped);
                    }
                    else
                    {
                        fp.OnSnapped += new FixPoint.SnappedEvent(DestinationFixPoint_OnSnapped);
                        fp.OnUnSnapped += new FixPoint.UnSnappedEvent(DestinationFixPoint_OnUnSnapped);
                    }
                    Add(fp);
                    fp.LocalPosition = new Vector3(-trackPosition, 0, trackSide);
                    fp.LocalYaw = localYaw;
                    // Label
                    Text3D label = new Text3D(Color.Yellow, 0.08f, 0.1f, new Font(FontFamily.GenericSansSerif, 1.0f));
                    label.Text = name;
                    Add(label);
                    label.LocalPosition = new Vector3(-trackPosition + 0.08f, 0, trackSide - 0.02f);
                    label.Pitch = Trigonometry.PI(Trigonometry.Angle2Rad(90.0f));
                }
            }
        }

        private void RemoveAllDematicFixpoints()
        {
            var dematicFixPoints = FixPoints.FindAll(x => x is DematicFixPoint);
            foreach (var fp in dematicFixPoints)
            {
                fp.Dispose();
                Remove(fp);
            }
            var labels = Parts.OfType<Text3D>();
            foreach (var label in labels)
            {
                label.Dispose();
                Remove(label);
            }
        }

        private void UpdateLength()
        {
            var offCentre = TCarWidth / 2 - TCarWidth;
            // Update length
            track.Length = trackRight.Length = tCarInfo.TCarLength;
            // Re-Position
            track.LocalPosition = new Vector3(-TCarLength / 2, -0.05f, offCentre);
            trackRight.LocalPosition = new Vector3(-TCarLength / 2, -0.05f, -offCentre);
            Core.Environment.SolutionExplorer.Refresh();
        }

        private void UpdateWidth()
        {
            var offCentre = TCarWidth / 2 - TCarWidth;
            track.LocalPosition = new Vector3(-TCarLength / 2, -0.05f, offCentre);
            trackRight.LocalPosition = new Vector3(-TCarLength / 2, -0.05f, -offCentre);
            conveyor.Length = TCarWidth;
            conveyor.LocalPosition = new Vector3(-conveyor.Width / 2, 0, 0);
            SetupFixPoints();
        }

        private void ThisRouteStatus_OnRouteStatusChanged(object sender, RouteStatusChangedEventArgs e)
        {
            if (e._available == RouteStatuses.Blocked)
            {
                if (Tasks != null && Tasks.Count > 0)
                {
                    var currentTask = Tasks[0];
                    if (currentTask.Source.Attached.Parent is IRouteStatus)
                    {
                        currentTask.Source.FixPointRouteStatus.Available = RouteStatuses.Blocked;
                    }
                    // Move car to offload destination
                    if (currentTask.Destination != null)
                    {
                        MoveTCar(currentTask.Destination);
                    }
                }
            }
        }

        private void Tasks_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems == null)
            {
                if (Tasks.Count > 0)
                {
                    MoveTCar(Tasks[0].Source);
                }
                else
                {
                    busy = false;
                }
            }
            else
            {
                if (!busy && Tasks.Count > 0)
                {
                    MoveTCar(Tasks[0].Source);
                }
            }
        }

        private void TrackStopPoint_OnEnter(ActionPoint sender, Load load)
        {
            track.Route.Motor.Stop();

            if (Tasks.Count > 0)
            {
                var currentTask = Tasks[0];

                if (currentTask.TCarCycle == TCycle.Load)
                {
                    IRouteStatus sourceConveyor = (IRouteStatus)currentTask.Source.Attached.Parent;
                    sourceConveyor.TransportSection.Route.NextRoute = conveyor.TransportSection.Route;
                    if (currentTask.Source.LocalPosition.Z == conveyor.StartFixPoint.LocalPosition.X)
                    {
                        conveyor.LocalYaw = Trigonometry.PI(Trigonometry.Angle2Rad(270.0f));
                    }
                    else
                    {
                        conveyor.LocalYaw = Trigonometry.PI(Trigonometry.Angle2Rad(90.0f));
                    }
                    conveyor.ThisRouteStatus.Available = RouteStatuses.Available;
                    currentTask.Source.FixPointRouteStatus.Available = RouteStatuses.Available;
                    currentTask.TCarCycle = TCycle.Unload;
                }
                else
                {
                    if (currentTask.Destination.LocalPosition.Z == conveyor.EndFixPoint.LocalPosition.X)
                    {
                        if (conveyor.TransportSection.Route.Loads.Count > 0)
                        {
                            var loadDistance = conveyor.TransportSection.Route.Loads.First().Distance;
                            conveyor.LocalYaw = Trigonometry.PI(Trigonometry.Angle2Rad(270.0f));
                            conveyor.TransportSection.Route.Loads.First().Distance = conveyor.TransportSection.Route.Length - loadDistance;
                        }
                    }
                    else if (currentTask.Destination.LocalPosition.Z == conveyor.StartFixPoint.LocalPosition.X)
                    {
                        if (conveyor.TransportSection.Route.Loads.Count > 0)
                        {
                            var loadDistance = conveyor.TransportSection.Route.Loads.First().Distance;
                            conveyor.LocalYaw = Trigonometry.PI(Trigonometry.Angle2Rad(90.0f));
                            conveyor.TransportSection.Route.Loads.First().Distance = conveyor.TransportSection.Route.Length - loadDistance;
                        }
                    }
                    IRouteStatus destinationConveyor = (IRouteStatus)currentTask.Destination.Attached.Parent;
                    conveyor.TransportSection.Route.NextRoute = destinationConveyor.TransportSection.Route;
                    if (currentTask.Destination.FixPointRouteStatus.Available == RouteStatuses.Available)
                    {
                        conveyor.ThisRouteStatus.Available = RouteStatuses.Request;
                    }
                }
            }
        }

        private void SourceFixPoint_OnSnapped(FixPoint stranger, FixPoint.SnapEventArgs e)
        {
            if (stranger.Parent is IRouteStatus)
            {
                IRouteStatus sourceConveyor = (IRouteStatus)stranger.Parent;
                DematicFixPoint dfp = (DematicFixPoint)stranger.Attached;

                if (!SourceFixPoints.ContainsKey(dfp.Name))
                {
                    SourceFixPoints.Add(dfp.Name, dfp);
                }

                if (dfp != null)
                {
                    dfp.FixPointLoadWaitingStatus = sourceConveyor.GetLoadWaitingStatus(stranger);
                    dfp.FixPointLoadWaitingStatus.OnLoadWaitingChanged += FixPointLoadWaitingStatus_OnLoadWaitingChanged;
                }
            }
            else
            {
                // call upsnap method
            }
        }

        private void FixPointLoadWaitingStatus_OnLoadWaitingChanged(object sender, LoadWaitingChangedEventArgs e)
        {
            Load load = e._waitingLoad;
            if (load != null && load.Route != null && e._loadWaiting)
            {
                IRouteStatus conveyor = (IRouteStatus)e._waitingLoad.Route.Parent.Parent;
                if (ControlType == ControlTypes.Local)
                {
                    // Select a destination at random (this will be replaced by controller code)
                    string destinationName = GetRandomDestination(); // Temporary : Get random one for now
                    DematicFixPoint destinationFP = (DematicFixPoint)FixPoints.Find(x => x.Name == destinationName);
                    DematicFixPoint sourceFP = (DematicFixPoint)conveyor.EndFixPoint.Attached;
                    if (sourceFP != null)
                    {
                        TCarTask task = new TCarTask
                        {
                            Source = sourceFP,
                            Destination = destinationFP,
                        };
                        Tasks.Add(task);
                    }
                }
                else if (ControlType == ControlTypes.Project_Script) // TODO: Invent event to attach routing script to
                {
                    SourceLoadArrived(load, (DematicFixPoint)conveyor.EndFixPoint.Attached);
                }
                else if (ControlType == ControlTypes.Controller)
                {
                    if (Controller != null)
                    {
                        sourceArrival?.Invoke(load, (DematicFixPoint)conveyor.EndFixPoint.Attached);
                    }
                }
            }
        }

        private string GetRandomDestination()
        {
            var destinationArray = tCarInfo.Destination.Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries);
            var random = new Random();
            string destination = destinationArray[random.Next(destinationArray.Length)];
            var fixPointArray = destination.Split(':');
            if (fixPointArray.Length == 3)
            {
                return fixPointArray[2];
            }
            return null;
        }

        private void SourceFixPoint_OnUnSnapped(FixPoint stranger)
        {
            if (stranger.Parent is IRouteStatus)
            {
                IRouteStatus sourceConveyor = (IRouteStatus)stranger.Parent;
                DematicFixPoint dfp = (DematicFixPoint)stranger.Attached;
                if(dfp != null)
                { 
                    if (SourceFixPoints.ContainsKey(dfp.Name))
                    {
                        SourceFixPoints.Remove(dfp.Name);
                    }
                    dfp.FixPointLoadWaitingStatus.OnLoadWaitingChanged -= FixPointLoadWaitingStatus_OnLoadWaitingChanged;
                }
            }
        }

        private void DestinationFixPoint_OnSnapped(FixPoint stranger, FixPoint.SnapEventArgs e)
        {
            if (stranger.Parent is IRouteStatus)
            {
                IRouteStatus destinationConveyor = (IRouteStatus)stranger.Parent;
                DematicFixPoint dfp = (DematicFixPoint)stranger.Attached;
                if (dfp != null)
                {
                    dfp.FixPointRouteStatus = destinationConveyor.GetRouteStatus(stranger);
                    dfp.FixPointRouteStatus.OnRouteStatusChanged += DestinationConveyorRouteStatus_OnRouteStatusChanged;
                }
            }
        }

        private void DestinationConveyorRouteStatus_OnRouteStatusChanged(object sender, RouteStatusChangedEventArgs e)
        {
            RouteStatus routeStatus = (RouteStatus)sender;
            if (routeStatus != null)
            {
                DestinationStatusChanged(routeStatus);

                var currentTask = Tasks.Count > 0 ? Tasks[0] : null;
                if (currentTask != null && currentTask.Destination != null)
                {
                    if (currentTask.Destination.FixPointRouteStatus == routeStatus)
                    {
                        if (e._available == RouteStatuses.Blocked)
                        {
                            Tasks.RemoveAt(0);
                        }
                        // Make sure the destination conveyor who's status has changed matches the current task
                        // Allow within acceptable tolerance for the positioning of the conveyor and fixpoint   
                        var isCurrentTask = currentTask.Destination.LocalPosition.X.WithinRange(conveyor.LocalPosition.X, 0.01f);
                        if (e._available == RouteStatuses.Available && isCurrentTask)
                        {
                            conveyor.ThisRouteStatus.Available = RouteStatuses.Request;
                        }
                    }
                }
            }
        }

        private void DestinationFixPoint_OnUnSnapped(FixPoint stranger)
        {
            if (stranger.Parent is IRouteStatus)
            {
                IRouteStatus destinationConveyor = (IRouteStatus)stranger.Parent;
                DematicFixPoint dfp = (DematicFixPoint)stranger.Attached;
                if (dfp != null)
                {
                    dfp.FixPointRouteStatus.OnRouteStatusChanged -= DestinationConveyorRouteStatus_OnRouteStatusChanged;
                }
            }
        }

        private void TrackLoad_OnPositionChanged(Load load, Vector3 position)
        {
            // Simple way to make the conveyor move as the 'trackload' moves (we use the trackload to move the platform)
            conveyor.LocalPosition = new Vector3(-load.Distance, 0, 0);
        }

        private void LineReleasePhotocell_OnPhotocellStatusChanged(object sender, PhotocellStatusChangedEventArgs e)
        {
            if (e._PhotocellStatus == PhotocellState.Blocked)
            {
                e._Load.UserDeletable = false;
            }
            else if (e._PhotocellStatus == PhotocellState.Clear)
            {
                if (e._Load != null)
                {
                    e._Load.UserDeletable = true;
                }
                conveyor.ThisRouteStatus.Available = RouteStatuses.Request;
            }
        }

        private void MoveTCar(DematicFixPoint fp)
        {
            busy = true;
            if (Running) return;
            float distance = -fp.LocalPosition.X;
            if (trackStopPoint.Distance == distance)
            {
                TrackStopPoint_OnEnter(trackStopPoint, trackLoad);
            }
            else
            {
                if (trackStopPoint.Distance <= distance)
                {
                    track.Route.Motor.Forward();
                }
                else
                {
                    track.Route.Motor.Backward();
                }
                trackStopPoint.Distance = distance;
                track.Route.Motor.Start();
                trackLoad.Release();
            }
        }

        public void Scene_OnLoaded()
        {
            if (ControllerProperties == null)
            {
                ControllerProperties = StandardCase.SetMHEControl(tCarInfo, this);
            }
        }

        public override void Reset()
        {
            base.Reset();
            busy = false;
            Tasks.Clear();
            track.Route.Motor.Forward();
            track.Route.Motor.Stop();
            trackStopPoint.Distance = 0;
            trackLoad.Distance = 0;
            conveyor.LocalPosition = new Vector3(0, 0, 0); // Move the platform to the default position (instant, not using timers)
            conveyor.TransportSection.Route.ClearLoads();
            conveyor.ThisRouteStatus.Available = RouteStatuses.Available;
            conveyor.TransportSection.Route.NextRoute = null;
            conveyor.LocalYaw = Trigonometry.PI(Trigonometry.Angle2Rad(90.0f));
            conveyor.Reset();
        }

        public override void Dispose()
        {
            foreach (FixPoint fp in FixPoints)
            {
                if (fp.Type == FixPoint.Types.Start)
                {
                    fp.OnSnapped -= new FixPoint.SnappedEvent(SourceFixPoint_OnSnapped);
                    fp.OnUnSnapped -= new FixPoint.UnSnappedEvent(SourceFixPoint_OnUnSnapped);
                }
            }
            Tasks.CollectionChanged -= Tasks_CollectionChanged;
            track.Dispose();
            conveyor.LineReleasePhotocell.OnPhotocellStatusChanged -= LineReleasePhotocell_OnPhotocellStatusChanged;
            conveyor.ThisRouteStatus.OnRouteStatusChanged -= ThisRouteStatus_OnRouteStatusChanged;
            conveyor.Dispose();
            trackLoad.OnPositionChanged -= TrackLoad_OnPositionChanged;
            trackLoad.Dispose();
            trackStopPoint.OnEnter -= TrackStopPoint_OnEnter;
            trackStopPoint.Dispose();
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
                conveyor.Color = value;
            }
        }

        public bool Running
        {
            get { return track.Route.Motor.Running; }
        }

        [Category("Configuration")]
        [DisplayName("Source")]
        [Description("Source conveyor details")]
        [PropertyOrder(1)]
        public string Source
        {
            get
            {
                return tCarInfo.Source;
            }
            set
            {
                tCarInfo.Source = value;
                Core.Environment.Invoke(() => SetupFixPoints());
            }
        }

        [Category("Configuration")]
        [DisplayName("Destination")]
        [Description("Destination conveyor details")]
        [PropertyOrder(2)]
        public string Destination
        {
            get
            {
                return tCarInfo.Destination;
            }
            set
            {
                tCarInfo.Destination = value;
                Core.Environment.Invoke(() => SetupFixPoints());
            }
        }

        [Category("Configuration")]
        [DisplayName(@"TCar Length")]
        [PropertyOrder(3)]
        [TypeConverter(typeof(FloatConverter))]
        [ReadOnly(true)]
        public float TCarLength
        {
            get { return tCarInfo.TCarLength; }
            set
            {
                if (value >= 0.01f && !tCarInfo.TCarLength.Equals(value))
                {
                    tCarInfo.TCarLength = value;
                    Core.Environment.Invoke(() => UpdateLength());
                }
            }
        }

        [Category("Configuration"), DisplayName(@"TCar Width"), PropertyOrder(4), TypeConverter(typeof(FloatConverter))]
        public float TCarWidth
        {
            get { return tCarInfo.TCarWidth; }
            set
            {
                tCarInfo.TCarWidth = value;
                Core.Environment.Invoke(() => UpdateWidth());
            }
        }

        [Category("Configuration"), DisplayName(@"Conveyor Width"), PropertyOrder(5), TypeConverter(typeof(FloatConverter))]
        public float ConveyorWidth
        {
            get { return tCarInfo.ConveyorWidth; }
            set
            {
                tCarInfo.ConveyorWidth = value;
                Core.Environment.Invoke(() => UpdateWidth());
            }
        }

        [Category("Position")]
        [DisplayName("Height")]
        [Description("Height of the conveyor (meter)")]
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
            get { return "T Car"; }
        }

        public override Image Image
        {
            get { return Common.Icons.Get("TCar"); }
        }


        #endregion

        #region Implement IRouteStatus

        public RouteStatus GetRouteStatus(FixPoint startFixPoint)
        {
            DematicFixPoint dFixPoint = (DematicFixPoint)startFixPoint;
            return dFixPoint.FixPointRouteStatus;
        }

        public LoadWaitingStatus GetLoadWaitingStatus(FixPoint endFixPoint)
        {
            return null;
        }

        public ITransportSection TransportSection
        {
            get
            {
                return conveyor.TransportSection;
            }

            set
            {
                conveyor.TransportSection = value;
            }
        }

        public float Speed
        {
            get
            {
                return conveyor.Speed;
            }
            set
            {
                conveyor.Speed = value;

                //if (OnSpeedUpdated != null)
                //{
                //    OnSpeedUpdated(this, new EventArgs());
                //}
            }
        }

        public int LoadCount
        {
            get
            {
                return conveyor.LoadCount;
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
            tCarInfo.ProtocolInfo = null;
        }

        void controller_OnControllerRenamedEvent(object sender, EventArgs e)
        {
            tCarInfo.ControllerName = ((Assembly)sender).Name;
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
                return tCarInfo.ControlType;
            }
            set
            {
                tCarInfo.ControlType = value;
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
                return tCarInfo.ControllerName;
            }
            set
            {
                if (!value.Equals(tCarInfo.ControllerName))
                {
                    ControllerProperties = null;
                    tCarInfo.ProtocolInfo = null;
                    Controller = null;
                }

                tCarInfo.ControllerName = value;
                if (value != null)
                {
                    //Note StandardCase.SetMHEControl will set the ControllerProperties and Controller properties!
                    ControllerProperties = StandardCase.SetMHEControl(tCarInfo, this);
                    if (ControllerProperties == null)
                    {
                        tCarInfo.ControllerName = "No Controller";
                    }
                }
            }
        }

        public FixPoint EndFixPoint
        {
            get; 
        }

        public void DynamicPropertyControllers(Core.Properties.PropertyAttributes attributes)
        {
            attributes.IsBrowsable = tCarInfo.ControlType == ControlTypes.Controller;
        }

        public void DynamicPropertyAssemblyPLCconfig(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = Controller != null;
        }

        #endregion
    }

    [Serializable]
    [XmlInclude(typeof(TCarInfo))]
    public class TCarInfo : AssemblyInfo, IControllableInfo
    {
        public float TCarLength;
        public float TCarWidth;
        public float ConveyorWidth;
        public string Source;
        public string Destination;
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
