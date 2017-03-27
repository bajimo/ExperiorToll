using Experior.Catalog.Dematic.Case.Components;
using Experior.Catalog.Devices;
using Experior.Core;
using Experior.Core.Assemblies;
using Experior.Core.Parts;
using Experior.Core.Properties;
using Experior.Dematic.Base;
using Microsoft.DirectX;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;

namespace Experior.Catalog.Dematic.Storage.MultiShuttle.Assemblies
{
    public class MultiShuttle : Assembly,  IControllable
    {

        private MultiShuttleDirections dir = MultiShuttleDirections.Infeed;

        public float workarround = 0.01f;
        private float bayLength;
        public static List<MultiShuttle> Aisles               = new List<MultiShuttle>();
        public List<DematicActionPoint> ConveyorLocations     = new List<DematicActionPoint>();
        public List<RackConveyor> RackConveyors               = new List<RackConveyor>();
        public List<DropStationConveyor> DropStationConveyors = new List<DropStationConveyor>();
        public List<PickStationConveyor> PickStationConveyors = new List<PickStationConveyor>();
        public Dictionary<int, Shuttle> shuttlecars           = new Dictionary<int, Shuttle>();
        public Dictionary<string, Elevator> elevators         = new Dictionary<string, Elevator>();
        public List<Box1> Racking                             = new List<Box1>();

        private List<System.Windows.Forms.ToolStripItem> subMenu;

        public event EventHandler OnPSTimeOutChanged;

        [Browsable(false)]
        public float BayLength
        {
            get
            {
                return bayLength;
            }
        }

        internal readonly MultiShuttleInfo MultiShuttleinfo;

        public MultiShuttle(MultiShuttleInfo info): base(info)
        {
            try
            {
                if(info.MultiShuttleDriveThrough)
                {
                    workarround += info.raillength / 2;   // info.DriveThroughElevatorOffset ??;                    
                }

                MultiShuttleinfo = info;
                AisleNumber      = info.AisleNo;
                info.height      = 0;
                info.transparency = MultiShuttleinfo.transparency;
                info.colors = Color.FromArgb(info.transparency, MultiShuttleinfo.colors);

                Aisles.Add(this);
                DSinfeedConfig = info.dsInfeedConfig;

                PickStationConfig = info.pickStationConfig;
                DropStationConfig = info.dropStationConfig;
                DSinfeedConfig = info.dsInfeedConfig;
                Rebuild();
                Core.Environment.Scene.OnLoaded += Scene_OnLoaded;

                subMenu = new List<System.Windows.Forms.ToolStripItem>();
                subMenu.Add(new System.Windows.Forms.ToolStripMenuItem("Reset MS", Common.Icons.Get("fault")));
                subMenu[0].Click += new EventHandler(DematicMultiShuttleReset_Click);
                                
                ControllerProperties = StandardCase.SetMHEControl(info, this);                                       

            }
            catch (Exception e)
            {
                Core.Environment.Log.Write(e.Message);
            }
        }

        private void Scene_OnLoaded()
        {
            if (ControllerProperties == null)
            {
                ControllerProperties = StandardCase.SetMHEControl(MultiShuttleinfo, this);
            }
        }

        private void DematicMultiShuttleReset_Click(object sender, EventArgs e)
        {
            if (Core.Environment.InvokeRequired)
            {
                Core.Environment.Invoke(new Action(Reset));
                return;
            }
            Reset();
        }

        public override List<System.Windows.Forms.ToolStripItem> ShowContextMenu()
        {
            return subMenu;
        }

        public override void Reset()
        {
            foreach (Shuttle shuttle in shuttlecars.Values)
            {
                if (shuttle.shuttleAP.Active)
                {
                    shuttle.shuttleAP.ActiveLoad.Dispose();
                }
                shuttle.Reset();
            }

            foreach (Elevator elevator in elevators.Values)
            {
                elevator.Reset();
            }

            base.Reset();
        }

        public override void Dispose()
        {
            subMenu[0].Click -= new EventHandler(DematicMultiShuttleReset_Click);
            Core.Environment.Scene.OnLoaded -= Scene_OnLoaded;
            Aisles.Remove(this);
            DropStationConveyors.Clear();
            ConveyorLocations.Clear();
            RackConveyors.Clear();
            elevators.Clear();
            PickStationConveyors.Clear();
            Racking.Clear();
            base.Dispose();
        }

        public override string Category
        {
            get
            {
                return "MultiShuttle";
            }
        }

        public override Image Image
        {
            get
            {
                return Common.Icons.Get("MultiShuttle");
            }
        }

        private void Rebuild()
        {
            try
            {
                if (Core.Environment.InvokeRequired)
                {
                    Core.Environment.Invoke(new Action(Rebuild));
                    return;
                }

                foreach (Shuttle shuttle in shuttlecars.Values)
                {
                    shuttle.trackRail.Dispose();
                    Remove(shuttle as Core.IEntity);
                    RemoveAssembly(shuttle);
                    shuttle.Dispose();
                }

                shuttlecars.Clear();

                foreach (RackConveyor c in RackConveyors)
                {
                    c.LocationA.OnEnter -= c.rackConvLocA_Enter;
                    c.LocationB.OnEnter -= c.rackConvLocB_Enter;

                    c.LocationA.Dispose();
                    c.LocationB.Dispose();

                    Remove(c);
                    c.Dispose();
                }

                foreach (DropStationConveyor f in DropStationConveyors)
                {
                    Remove(f);
                    Remove(f as Core.IEntity);
                    f.Dispose();
                }

                foreach (Elevator elevator in elevators.Values)
                {
                    Remove(elevator);
                    elevator.Dispose();
                }

                foreach (PickStationConveyor c in PickStationConveyors)
                {
                    Remove(c);
                    Remove(c as Core.IEntity);
                    c.Dispose();
                }

                DropStationConveyors.Clear();
                ConveyorLocations.Clear();
                RackConveyors.Clear();
                elevators.Clear();
                PickStationConveyors.Clear();

                ClearRackingBoxes();
                CreateRackingBoxes();

                CreateShuttles();

                if (MultiShuttleinfo.ElevatorFL)
                {
                    //TODO Add elevator groups
                    BuildElevator(string.Format("L{0}", AisleNumber.ToString().PadLeft(2, '0')), FLElevatorType, RackConveyorLength / 2 + RackConveyorLength / 4);//Rackconveyors, pickstation and dropstation conveyors are also build here as well                                       
                }

                if (MultiShuttleinfo.ElevatorFR)
                {
                    BuildElevator(string.Format("R{0}", AisleNumber.ToString().PadLeft(2, '0')), FLElevatorType, RackConveyorLength / 2 + RackConveyorLength / 4);//Rackconveyors, pickstation and dropstation conveyors are also build here as well                    
                }

                if (!MultiShuttleinfo.MultiShuttleDriveThrough)
                {
                    bayLength = (Raillength - RackConveyorLength) / RackBays;
                }
                else
                {
                    bayLength = (Raillength - RackConveyorLength * 2 - ElevatorConveyorLength) / RackBays;
                }

                bayLength = (Raillength - RackConveyorLength * 2 - ElevatorConveyorLength) / RackBays;

                if (bayLength <= 0)
                {
                    Core.Environment.Log.Write(Name + ": Configuration error! Raillength is too short (baylength is calculated to be < 0)", Color.Red);
                }

                Core.Environment.SolutionExplorer.Refresh();
            }
            catch (Exception e)
            {
                Core.Environment.Log.Write(e.Message, Color.Red);
            }
        }

        private void CreateShuttles()
        {
            for (var level = 1; level <= MultiShuttleinfo.ShuttleNumber; level++)
            {
                ShuttleInfo sInfo = new ShuttleInfo() 
                {
                    multiShuttleinfo = MultiShuttleinfo, 
                    level            = level, 
                    parent           = this, 
                    name             = string.Format("{0}L{1}",  AisleNumber.ToString().PadLeft(2, '0'), level) 
                };

                Shuttle shuttle2              = new Shuttle(sInfo);
                shuttle2.trackRail.Length     = MultiShuttleinfo.raillength;
                shuttle2.trackRail.Car.Length = MultiShuttleinfo.carlength;
                shuttle2.trackRail.Car.Width  = MultiShuttleinfo.carwidth;

                shuttlecars.Add(level, shuttle2);
                AddAssembly(shuttle2, new Vector3(0, ((level-1) * MultiShuttleinfo.DistanceLevels) /*- MultiShuttleinfo.DistanceLevels*//*0.5f*/  , 0));             
                shuttle2.shuttleConveyor.Visible = false;
            }
        }

        private void BuildElevator(string name, MultiShuttleDirections elevatorType, float xoffset)
        {
            var zCoord = -RackConveyorWidth / 2 - MultiShuttleinfo.carwidth / 2;
            var elevatorinfo = new MultiShuttleElevatorInfo()
            {
                Multishuttle     = this,
                multishuttleinfo = MultiShuttleinfo,
                ElevatorName     = name,
                ElevatorType     = elevatorType
            };

            if (name.Substring(0, 1) == "R")
            {
                elevatorinfo.Side = RackSide.Right;
                zCoord *= -1;
            }
            else
            {
                elevatorinfo.Side = RackSide.Left;
            }

            var elevator = new Elevator(elevatorinfo);

            elevators.Add(elevator.ElevatorName, elevator);
            elevator.ElevatorHeight = MultiShuttleinfo.ShuttleNumber * MultiShuttleinfo.DistanceLevels + MultiShuttleinfo.RackHeightOffset + 0.5f;
            AddAssembly(elevator);

            if (MultiShuttleinfo.MultiShuttleDriveThrough)
            {
                elevator.LocalPosition = new Vector3(DriveThroughElevatorOffset + xoffset, elevator.ElevatorHeight / 2, zCoord);
            }
            else
            {
                elevator.LocalPosition = new Vector3(MultiShuttleinfo.raillength / 2 + elevator.ElevatorConveyor.ConveyorLength / 2 + xoffset, elevator.ElevatorHeight / 2, zCoord);
            }

            if (elevatorType == MultiShuttleDirections.Outfeed)
            {
                elevator.LocalYaw = (float)Math.PI * 2;
            }
            else if (elevatorType == MultiShuttleDirections.Infeed)
            {
                elevator.LocalYaw = (float)Math.PI;
            }
            
            BuildRackConveyors(elevator, zCoord, xoffset);

            if (MultiShuttleinfo.MultiShuttleDriveThrough) // add a second set of rack conveyors
            {
                BuildRackConveyors(elevator, zCoord, xoffset + elevator.ElevatorConveyor.Length * 2);
            }

            CreatePickstationConveyors(elevator, zCoord, xoffset);
            CreateDropstationConveyors(elevator, zCoord, xoffset);
        }

        /// <summary>
        /// Create Rack Conveyors
        /// </summary>
        /// <param name="elevatorType"></param>
        /// <param name="xoffset"></param>
        /// <param name="elevator"></param>
        /// <param name="zCoord"></param>
        private void BuildRackConveyors( Elevator elevator, float zCoord, float xoffset )
        {
            
            for (var level = 1; level <= MultiShuttleinfo.ShuttleNumber; level++)
            {
                var rackConvInfo = new RackConveyorInfo()
                { 
                    thickness = 0.05f,
                    length    = RackConveyorLength,
                    width     = RackConveyorWidth,
                    speed     = ConveyorSpeed
                };

                var rackConveyor = new RackConveyor(rackConvInfo)
                { 
                    Side = elevator.Side,
                    Name = string.Format("{0}{1}{2}", AisleNumber.ToString().PadLeft(2, '0'), (char)elevator.Side, level.ToString().PadLeft(2, '0'))
                };

                if (!MixedInfeedOutfeed)
                {
                    rackConveyor.RackConveyorType = elevator.ElevatorType;
                }
                else
                {
                    if (LeftEvenLevelInfeed && level % 2 == 0 && elevator.Side == RackSide.Left)
                    {
                        dir = MultiShuttleDirections.Infeed;
                    }
                    else if (LeftEvenLevelInfeed && level % 2 > 0 && elevator.Side == RackSide.Right)
                    {
                        dir = MultiShuttleDirections.Infeed;
                    }
                    else if (!LeftEvenLevelInfeed && level % 2 > 0 && elevator.Side == RackSide.Left)
                    {
                        dir = MultiShuttleDirections.Infeed;
                    }
                    else if (!LeftEvenLevelInfeed && level % 2 == 0 && elevator.Side == RackSide.Right)
                    {
                        dir = MultiShuttleDirections.Infeed;
                    }
                    else
                    {
                        dir = MultiShuttleDirections.Outfeed;
                    }

                    rackConveyor.RackConveyorType = dir;
                }

                AddAssembly(rackConveyor);
                rackConveyor.ConfigureRackConveyor(elevator, zCoord, level, xoffset);
            }
        }

        private void CreateDropstationConveyors(Elevator elevator, float zCoord, float xoffset)
        {
            foreach (LevelID level in MultiShuttleinfo.LevelHeightDropstations)
            {
                {
                    var dropStationConvInfo = new DropStationConveyorInfo()
                    { 
                        type      = FixPoint.Types.End,
                        parent    = this,
                        elevator  = elevator,
                        thickness = 0.05f,
                        color     = Core.Environment.Scene.DefaultColor,
                        Width     = 0.5f,
                        name      = string.Format("{0}DS{1}", AisleNumber.ToString().PadLeft(2, '0'), level.ID)
                    };

                    try
                    {
                        var dropStationConveyor = new DropStationConveyor(dropStationConvInfo)
                        {
                            Side = elevator.Side,
                            AisleNumber = MultiShuttleinfo.AisleNo,
                            Height = level.Height,
                            ListSolutionExplorer = true,
                            Positions = 2,
                            OutfeedSection = Case.Components.OutfeedLength._0mm,
                            InfeedSection = dsInfeedLengths[level.ID],
                            //InfeedSection        = DSinfeed,
                            Width = 0.750f,
                            AccPitch = Case.Components.AccumulationPitch._500mm
                        };
    

                        dropStationConveyor.TransportSection.Route.Motor.Speed = ConveyorSpeed;
                        dropStationConveyor.ConvLocationConfiguration(level.ID);
                        Add(dropStationConveyor);
                        DropStationConveyors.Add(dropStationConveyor);
                        dropStationConveyor.LocalPosition = new Vector3(MultiShuttleinfo.raillength / 2 + elevator.ElevatorConveyor.ConveyorLength + dropStationConveyor.Length / 2 + xoffset, level.Height, zCoord);
                        dropStationConveyor.LocalYaw = -(float)Math.PI;
                    }
                    catch (KeyNotFoundException ex)
                    {
                        Log.Write("\"DropStation conveyor infeed lengths\" IDs do not match  \"Drop Station infeed Length Config\" IDs ", Color.Red);
                        Log.Write(ex.Message, Color.Red);
                    }
                }
            }
        }

        private void CreatePickstationConveyors(Elevator elevator, float zCoord, float xoffset)
        {

            foreach (LevelID level in MultiShuttleinfo.LevelHeightPickstations)
            {
                var pickStationConvinfo = new PickStationConveyorInfo()
                {
                    thickness          = 0.05f,
                    color              = Core.Environment.Scene.DefaultColor,
                    Width              = 0.5f,                  
                    CreateLineFull     = true,
                    elevator           = elevator,
                    ParentMultiShuttle = this,
                    createTempAP       = true,
                    name               = string.Format("{0}PS{1}", AisleNumber.ToString().PadLeft(2, '0'), level.ID)
                };

                var pickStationConveyor = new PickStationConveyor(pickStationConvinfo)
                {
                    Side           = elevator.Side,
                    AisleNumber    = MultiShuttleinfo.AisleNo,
                    Positions      = 2,
                    OutfeedSection = PSoutfeed,
                    InfeedSection  = 0,
                    Width          = 0.750f,
                    Height         = level.Height,
                    AccPitch       = Case.Components.AccumulationPitch._500mm
                };

                pickStationConveyor.Route.Motor.Speed      = ConveyorSpeed;
                pickStationConveyor.ListSolutionExplorer   = true;
                pickStationConveyor.RouteAvailableOverride = true;
                Add(pickStationConveyor);
                PickStationConveyors.Add(pickStationConveyor);
                pickStationConveyor.LocalPosition = new Vector3(MultiShuttleinfo.raillength / 2 + elevator.ElevatorConveyor.ConveyorLength + pickStationConveyor.Length / 2 + xoffset, level.Height, zCoord);
                pickStationConveyor.ConvLocationConfiguration(level.ID);
            }
        }

        private void ClearRackingBoxes()
        {
            foreach (Box1 box in Racking)
            {
                Remove(box);
                Remove(box as Core.IEntity);
                box.Dispose();
            }
            Racking.Clear();
        }

        private void CreateRackingBoxes()
        {
            Box1Info boxInfo = new Box1Info()
            {
                thickness = 0.01f,
                length = MultiShuttleinfo.raillength - MultiShuttleinfo.carlength / 2,
                width = MultiShuttleinfo.DepthDistPos2 + 0.4f,
                height = MultiShuttleinfo.ShuttleNumber * MultiShuttleinfo.DistanceLevels,
                colors = System.Drawing.Color.FromArgb(MultiShuttleinfo.transparency, MultiShuttleinfo.colors)
            };

            Box1 box1 = new Box1(boxInfo);
            Racking.Add(box1);
            AddAssembly(box1, new Vector3(-(MultiShuttleinfo.carlength + boxInfo.thickness) / 2, 0, boxInfo.thickness + MultiShuttleinfo.carwidth / 2));

            Box1 box2 = new Box1(boxInfo);
            Racking.Add(box2);
            AddAssembly(box2, new Vector3(-(MultiShuttleinfo.carlength + boxInfo.thickness) / 2, 0, -(boxInfo.width + boxInfo.thickness + MultiShuttleinfo.carwidth / 2)));
        }

        public StraightConveyor GetConveyorFromLocName(string ConveyorName)
        {
            if (ConveyorName == null) return null;

            var loc = ConveyorLocations.Find(x => x.LocName == ConveyorName);
            if (loc == null)
            {
                Log.Write("Location " + ConveyorName + " does not exist level of original message might be wrong!", Color.Red);
                return null;
            }
            StraightConveyor sc = loc.Parent.Parent.Parent as StraightConveyor;
            return loc.Parent.Parent.Parent as StraightConveyor;
        }


        public void DynamicPropertyMixedInfeedOutfeed(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = MixedInfeedOutfeed;
        }

        private IController controller;

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

        private void controller_OnControllerRenamedEvent(object sender, EventArgs e)
        {
            ControllerName = ((Assembly)sender).Name;
        }

        private void controller_OnControllerDeletedEvent(object sender, EventArgs e)
        {
            controller.OnControllerDeletedEvent -= controller_OnControllerDeletedEvent;
            ControllerName                       = "No Controller";
            Controller                           = null;
            ControllerProperties                 = null;
        }

        /// <summary>
        /// The controller will assign a new elevator task if false
        /// </summary>
        public bool AutoNewElevatorTask
        {
            get {return MultiShuttleinfo.autoNewElevatorTask;}
            set
            {
                MultiShuttleinfo.autoNewElevatorTask = value;
            }
        }

        #region User Interface

        #region Rack Configuration

        [Category("Rack Configuration")]
        [PropertyOrder(12)]
        [DescriptionAttribute("Colour")]
        [DisplayName("Racking Colour")]
        public Color Colour
        {
            get { return MultiShuttleinfo.colors; }
            set
            {
                MultiShuttleinfo.colors = value;
                ClearRackingBoxes();
                CreateRackingBoxes();
            }
        }

        [Category("Rack Configuration")]
        [PropertyOrder(12)]
        [DescriptionAttribute("Transparency")]
        [DisplayName("Racking Colour Transparency")]
        public int Transparency
        {
            get { return MultiShuttleinfo.transparency; }
            set
            {
                if (value > 255) { value = 255; }
                else if (value < 0) { value = 0; }
                MultiShuttleinfo.transparency = value;
                ClearRackingBoxes();
                CreateRackingBoxes();
            }
        }

        [Category("Rack Configuration")]
        [PropertyOrder(6)]
        [DescriptionAttribute("Conveyor Speed (m/s) for rack conveyor, elevator conveyor, pick station conveyor")]
        [DisplayName("Conveyor Speed (m/s)")]
        public float ConveyorSpeed
        {
            get
            {
                return MultiShuttleinfo.ConveyorSpeed;
            }
            set
            {
                if (value > 0)
                {
                    MultiShuttleinfo.ConveyorSpeed = value;

                    foreach (RackConveyor rack in RackConveyors)
                    {
                        rack.TransportSection.Route.Motor.Speed = value;
                    }

                    foreach (Elevator elevator in elevators.Values)
                    {
                        elevator.ElevatorConveyorSpeed = value;
                    }

                    foreach (PickStationConveyor pickstation in PickStationConveyors)
                    {
                        pickstation.Route.Motor.Speed = value;
                    }
                }
            }
        }

        [Category("Rack Configuration")]
        [PropertyOrder(9)]
        [DescriptionAttribute("The distane to depth in the rack position 1 (center to center)")]
        [DisplayName("Distance to rack depth Pos 1 (m.)")]
        public float DepthDistPos1
        {
            get
            {
                return MultiShuttleinfo.DepthDistPos1;
            }
            set
            {
                if (value >= DepthDistPos2)
                {
                    Core.Environment.Log.Write("DepthDistPos1 can't be greater than DepthDistPos2", Color.Orange);
                }
                else
                {
                    MultiShuttleinfo.DepthDistPos1 = value;
                    if (!Core.Environment.Scene.Loading)
                    {
                        Rebuild();
                    }
                }
            }
        }



        [Category("Rack Configuration")]
        [PropertyOrder(9)]
        [DescriptionAttribute("The distane to depth in the rack position 2 (center to center)")]
        [DisplayName("Distance to rack depth Pos 2 (m.)")]
        public float DepthDistPos2
        {
            get
            {
                return MultiShuttleinfo.DepthDistPos2;
            }
            set
            {
                if (value <= DepthDistPos1)
                {
                    Core.Environment.Log.Write("DepthDistPos2 can't be less than DepthDistPos1", Color.Orange);
                }
                else
                {
                    MultiShuttleinfo.DepthDistPos2 = value;
                    if (!Core.Environment.Scene.Loading)
                    {
                        Rebuild();
                    }
                }
            }
        }

        [PropertyOrder(1)]
        [Category("Rack Configuration")]
        [DescriptionAttribute("Rail length in meter.")]
        [DisplayName("Rail length (m.)")]
        public float Raillength
        {
            get
            {
                return MultiShuttleinfo.raillength;
            }
            set
            {
                if (value > 2 && value <= 2000 && MultiShuttleinfo.raillength != value)
                {
                    MultiShuttleinfo.raillength = value;
                    if (!Core.Environment.Scene.Loading)
                    {
                        Rebuild();
                    }
                }
            }
        }

        [Category("Rack Configuration")]
        [DescriptionAttribute("Number of levels.")]
        [DisplayName("Number of levels.")]
        public int Levels
        {
            get
            {
                return MultiShuttleinfo.ShuttleNumber;
            }
            set
            {
                if (value > 0 && value <= 100 && MultiShuttleinfo.ShuttleNumber != value)
                {
                    MultiShuttleinfo.ShuttleNumber = value;
                    if (!Core.Environment.Scene.Loading)
                    {
                        Rebuild();
                    }
                }
            }
        }

        [Category("Rack Configuration")]
        [DescriptionAttribute("Distance between levels. (meter)")]
        [DisplayName("Distance between levels.")]
        public float DistanceLevels
        {
            get
            {
                return MultiShuttleinfo.DistanceLevels;
            }
            set
            {
                if (value > 0 && value <= 100 && MultiShuttleinfo.DistanceLevels != value)
                {
                    MultiShuttleinfo.DistanceLevels = value;
                    if (!Core.Environment.Scene.Loading)
                    {
                        Rebuild();
                    }
                }
            }
        }

        [Category("Rack Configuration")]
        [DescriptionAttribute("Rack Height Offset in meters. ")]
        [DisplayName("Rack Height Offset (m).")]
        public float RackHeightOffset
        {
            get
            {
                return MultiShuttleinfo.RackHeightOffset;
            }
            set
            {
                if (value >= 0 && value <= 100 && MultiShuttleinfo.RackHeightOffset != value)
                {
                    MultiShuttleinfo.RackHeightOffset = value;
                    if (!Core.Environment.Scene.Loading)
                    {
                        Rebuild();
                    }
                }
            }
        }

        [Category("Rack Configuration")]
        [DescriptionAttribute("Rack conveyor length (meter)")]
        [DisplayName("Rack conveyor length")]
        public float RackConveyorLength
        {
            get
            {
                return MultiShuttleinfo.RackConveyorLength;
            }
            set
            {
                if (value > 0 && value <= 100 && MultiShuttleinfo.RackConveyorLength != value)
                {
                    MultiShuttleinfo.RackConveyorLength = value;
                    if (!Core.Environment.Scene.Loading)
                    {
                        Rebuild();
                    }
                }
            }
        }

        [Category("Rack Configuration")]
        [DescriptionAttribute("Rack conveyor width (meter)")]
        [DisplayName("Rack conveyor width")]
        public float RackConveyorWidth
        {
            get
            {
                return MultiShuttleinfo.RackConveyorWidth;
            }
            set
            {
                if (value > 0 && value <= 100 && MultiShuttleinfo.RackConveyorWidth != value)
                {
                    MultiShuttleinfo.RackConveyorWidth = value;
                    if (!Core.Environment.Scene.Loading)
                    {
                        Rebuild();
                    }
                }
            }
        }

        [Category("Rack Configuration")]
        [PropertyOrder(5)]
        [DescriptionAttribute("The number of bays in rack (presumably evenly distributed over the length of the rack)")]
        [DisplayName("Bays in rack")]
        public int RackBays
        {
            get
            {
                return MultiShuttleinfo.RackBays;
            }
            set
            {
                if (MultiShuttleinfo.RackBays != value && value > 1)
                {
                    MultiShuttleinfo.RackBays = value;



                    bayLength = (Raillength - RackConveyorLength * 2 - ElevatorConveyorLength) / RackBays;
                }
            }
        }


        [Category("Rack Configuration")]
        [DescriptionAttribute("Mixed infeed/outfeed on both sides")]
        [DisplayName("Mixed infeed/outfeed")]
        [PropertyAttributesProvider("DynamicPropertyNotMiddleElevator")]
        public bool MixedInfeedOutfeed
        {
            get
            {
                return MultiShuttleinfo.MixedInfeedOutfeed;
            }
            set
            {
                if (MultiShuttleinfo.MixedInfeedOutfeed != value)
                {
                    MultiShuttleinfo.MixedInfeedOutfeed = value;
                    if (value)
                    {
                        MultiShuttleinfo.MultiShuttleDriveThrough = false;
                    }
                    Core.Environment.Properties.Refresh();
                    if (!Core.Environment.Scene.Loading)
                    {
                        Rebuild();
                    }
                }
            }
        }

        [Category("Rack Configuration")]
        [DescriptionAttribute("If true then left even levels are infeed and odd levels are outfeed. Right even levels are outfeed and odd levels infeed.")]
        [DisplayName("Left Even Infeed")]
        [PropertyAttributesProvider("DynamicPropertyMixedInfeedOutfeed")]
        public bool LeftEvenLevelInfeed
        {
            get
            {
                return MultiShuttleinfo.LeftEvenLevelInfeed;
            }
            set
            {
                if (MultiShuttleinfo.LeftEvenLevelInfeed != value)
                {
                    MultiShuttleinfo.LeftEvenLevelInfeed = value;
                    Core.Environment.Properties.Refresh();
                    if (!Core.Environment.Scene.Loading)
                    {
                        Rebuild();
                    }
                }
            }
        }

        #endregion

        #region Elevator Configuration


        [PropertyOrder(3)]
        [CategoryAttribute("Elevator Configuration")]
        [DescriptionAttribute("Front left elevator")]
        [DisplayName("FL elevator")]
        [PropertyAttributesProvider("DynamicPropertyNotMiddleElevator")]
        public bool FrontLeftElevator
        {
            get
            {
                return MultiShuttleinfo.ElevatorFL;
            }
            set
            {
                if (MultiShuttleinfo.ElevatorFL != value)
                {
                    MultiShuttleinfo.ElevatorFL = value;
                    if (!Core.Environment.Scene.Loading)
                    {
                        Rebuild();
                    }
                }
            }
        }

        [PropertyOrder(4)]
        [CategoryAttribute("Elevator Configuration")]
        [DescriptionAttribute("Front right elevator")]
        [DisplayName("FR elevator")]
        [PropertyAttributesProvider("DynamicPropertyNotMiddleElevator")]
        public bool FrontRightElevator
        {
            get
            {
                return MultiShuttleinfo.ElevatorFR;
            }
            set
            {
                if (MultiShuttleinfo.ElevatorFR != value)
                {
                    MultiShuttleinfo.ElevatorFR = value;
                    if (!Core.Environment.Scene.Loading)
                    {
                        Rebuild();
                    }
                }
            }
        }

        [CategoryAttribute("Elevator Configuration")]
        [DisplayName("FR elevator type")]
        [PropertyAttributesProvider("DynamicPropertyNotMixed")]
        public MultiShuttleDirections FRElevatorType
        {
            get
            {
                return MultiShuttleinfo.ElevatorFRtype;
            }
            set
            {
                if (MultiShuttleinfo.ElevatorFRtype != value)
                {
                    MultiShuttleinfo.ElevatorFRtype = value;
                    if (!Core.Environment.Scene.Loading)
                    {
                        Rebuild();
                    }
                }
            }
        }

        [CategoryAttribute("Elevator Configuration")]
        [DisplayName("FL elevator type")]
        [PropertyAttributesProvider("DynamicPropertyNotMixed")]
        public MultiShuttleDirections FLElevatorType
        {
            get
            {
                return MultiShuttleinfo.ElevatorFLtype;
            }
            set
            {
                if (MultiShuttleinfo.ElevatorFLtype != value)
                {
                    MultiShuttleinfo.ElevatorFLtype = value;
                    if (!Core.Environment.Scene.Loading)
                    {
                        Rebuild();
                    }
                }
            }
        }

        public void DynamicPropertyNotMixed(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = !MixedInfeedOutfeed;
        }

        [CategoryAttribute("Elevator Configuration")]
        [DescriptionAttribute("Drive Through Elevator Offset in meters. ")]
        [DisplayName("Drive Through Elevator Offset (m).")]
        [PropertyAttributesProvider("DynamicPropertyMiddleElevator")]
        public float DriveThroughElevatorOffset
        {
            get
            {
                return MultiShuttleinfo.DriveThroughElevatorOffset;
            }
            set
            {
                if (Math.Abs(value) <= Raillength / 2 - RackConveyorLength - ElevatorConveyorLength / 2 && MultiShuttleinfo.DriveThroughElevatorOffset != value)
                {
                    MultiShuttleinfo.DriveThroughElevatorOffset = value;
                    if (!Core.Environment.Scene.Loading)
                    {
                        Rebuild();
                    }
                }
            }
        }

        [CategoryAttribute("Elevator Configuration")]
        [DescriptionAttribute("Elevator conveyor length (meter)")]
        [DisplayName("Elevator conveyor length")]
        public float ElevatorConveyorLength
        {
            get
            {
                return MultiShuttleinfo.ElevatorConveyorLength;
            }
            set
            {
                if (value > 0 && value <= 100 && MultiShuttleinfo.ElevatorConveyorLength != value)
                {
                    MultiShuttleinfo.ElevatorConveyorLength = value;
                    if (!Core.Environment.Scene.Loading)
                    {
                        Rebuild();
                    }
                }
            }
        }

        [CategoryAttribute("Elevator Configuration")]
        [DescriptionAttribute("Elevator conveyor width (meter)")]
        [DisplayName("Elevator conveyor width")]
        public float ElevatorConveyorWidth
        {
            get
            {
                return MultiShuttleinfo.ElevatorConveyorWidth;
            }
            set
            {
                if (value > 0 && value <= 100 && MultiShuttleinfo.ElevatorConveyorWidth != value)
                {
                    MultiShuttleinfo.ElevatorConveyorWidth = value;
                    if (!Core.Environment.Scene.Loading)
                    {
                        Rebuild();
                    }
                }
            }
        }

                [PropertyOrder(1)]
        [CategoryAttribute("Elevator Configuration")]
        [DescriptionAttribute("Elevator speed in m/s")]
        [DisplayName("Elevator speed (m/s)")]
        public float ElevatorSpeed
        {
            get
            {
                return MultiShuttleinfo.elevatorSpeed;
            }
            set
            {
                if (value > 0 && value <= 1000)
                {
                    MultiShuttleinfo.elevatorSpeed = value;

                    foreach (Elevator elevator in elevators.Values)
                    {
                        elevator.ElevatorSpeed = value;
                    }
                }
            }
        }

        #endregion

        #region Shuttle Configuration

        [PropertyOrder(1)]
        [CategoryAttribute("Shuttle Configuration")]
        [DescriptionAttribute("Shuttle car speed in m/s")]
        [DisplayName("Shuttle car speed (m/s)")]
        public float ShuttleCarSpeed
        {
            get
            {
                return MultiShuttleinfo.shuttlecarSpeed;
            }
            set
            {
                if (value > 0 && value <= 1000)
                {
                    MultiShuttleinfo.shuttlecarSpeed = value;
                    foreach (Shuttle car in shuttlecars.Values)
                    {
                        if (car.trackRail.Route.Motor.Speed > 0)
                        {
                            car.trackRail.Route.Motor.Speed = MultiShuttleinfo.shuttlecarSpeed;
                        }
                        else
                        {
                            car.trackRail.Route.Motor.Speed = -MultiShuttleinfo.shuttlecarSpeed;
                        }
                    }
                }
            }
        }

        [PropertyOrder(2)]
        [CategoryAttribute("Shuttle Configuration")]
        [DescriptionAttribute("Shuttle Car Loading speed in m/s")]
        [DisplayName("Loading speed (m/s)")]
        public float LoadingSpeed
        {
            get
            {
                return MultiShuttleinfo.loadingSpeed;
            }
            set
            {
                if (value > 0)
                {
                    MultiShuttleinfo.loadingSpeed = value;
                }
            }
        }

        [PropertyOrder(3)]
        [CategoryAttribute("Shuttle Configuration")]
        [DescriptionAttribute("Shuttle Car Unloading speed in m/s")]
        [DisplayName("Unloading speed (m/s)")]
        public float UnloadingSpeed
        {
            get
            {
                return MultiShuttleinfo.unloadingSpeed;
            }
            set
            {
                if (value > 0)
                {
                    MultiShuttleinfo.unloadingSpeed = value;
                }
            }
        }

        [CategoryAttribute("Shuttle Configuration")]
        [DescriptionAttribute("Shuttle car Length in meter.")]
        [DisplayName("Shuttle car Length (m.)")]
        public float ShuttleCarLength
        {
            get
            {
                return MultiShuttleinfo.carlength;
            }
            set
            {
                if (value > 0 && value <= 100 && MultiShuttleinfo.carlength != value)
                {
                    MultiShuttleinfo.carlength = value;
                    if (!Core.Environment.Scene.Loading)
                    {
                        Rebuild();
                    }
                }
            }
        }

        [CategoryAttribute("Shuttle Configuration")]
        [DescriptionAttribute("Shuttle car width in meter.")]
        [DisplayName("Shuttle car width (m.)")]
        public float ShuttleCarWidth
        {
            get
            {
                return MultiShuttleinfo.carwidth;
            }
            set
            {
                if (value > 0 && value <= 100 && MultiShuttleinfo.carwidth != value)
                {
                    MultiShuttleinfo.carwidth = value;
                    if (!Core.Environment.Scene.Loading)
                    {
                        Rebuild();
                    }
                }
            }
        }

        [CategoryAttribute("Shuttle Configuration")]
        [PropertyOrder(9)]
        [DescriptionAttribute("Shuttle positioning time in seconds. When making a drop or pick this time as added.")]
        [DisplayName("Shuttle positioning time (s.)")]
        public float ShuttlePositioningTime
        {
            get
            {
                return MultiShuttleinfo.ShuttlePositioningTime;
            }
            set
            {
                if (value != MultiShuttleinfo.ShuttlePositioningTime && value > 0)
                {
                    MultiShuttleinfo.ShuttlePositioningTime = value;
                }
            }
        }

        #endregion

        #region PS and DS Configuration

        [CategoryAttribute("PS and DS Configuration")]
        [DescriptionAttribute("Number of PickStations in the form \"3:01;4:02\" -> 3m and id of \"01\" and 4m and id of \"02\".")]
        [DisplayName("PickStation Config")]
        [PropertyOrder(1)]
        public string PickStationConfig
        {
            get
            {
                return MultiShuttleinfo.pickStationConfig;
            }
            set
            {
                MultiShuttleinfo.pickStationConfig = value;
                MultiShuttleinfo.LevelHeightPickstations.Clear();

                string[] levelIDs = value.Split(';');

                foreach (string levelID in levelIDs)
                {
                    string[] lANDid = levelID.Split(':');

                    float psHeight;
                    float.TryParse(lANDid[0], out psHeight);
                    LevelID lID = new LevelID { Height = psHeight, ID = lANDid[1] };
                    MultiShuttleinfo.LevelHeightPickstations.Add(lID);
                }

                if (!Core.Environment.Scene.Loading)
                {
                    Rebuild();
                }
            }
        }

        [CategoryAttribute("PS and DS Configuration")]
        [DescriptionAttribute("Number of DropStations in the form \"3:01;4:02\" -> 3m and id of \"01\" and 4m and id of \"02\".")]
        [DisplayName("DropStation Config")]
        [PropertyOrder(2)]
        public string DropStationConfig
        {
            get
            {
                return MultiShuttleinfo.dropStationConfig;
            }
            set
            {
                MultiShuttleinfo.dropStationConfig = value;
                MultiShuttleinfo.LevelHeightDropstations.Clear();

                string[] levelIDs = value.Split(';');

                foreach (string levelID in levelIDs)
                {
                    string[] lANDid = levelID.Split(':');

                    float psHeight;
                    float.TryParse(lANDid[0], out psHeight);
                    LevelID lID = new LevelID { Height = psHeight, ID = lANDid[1] };
                    MultiShuttleinfo.LevelHeightDropstations.Add(lID);
                }

                if (!Core.Environment.Scene.Loading)
                {
                    Rebuild();
                }
            }
        }

        Dictionary<string, float> dsInfeedLengths = new Dictionary<string, float>();

        [CategoryAttribute("PS and DS Configuration")]
        [DescriptionAttribute("Number of DropStations in the form \"0.375:01;0:02\" -> 0.375m and id of \"01\" and 0m and id of \"02\"." + "\nThe drop station IDs must match \"DropStation conveyor infeed lengths\"")]
        [DisplayName("Drop Station infeed Length Config")]
        [PropertyOrder(3)]
        public string DSinfeedConfig
        {
            get { return MultiShuttleinfo.dsInfeedConfig; }
            set
            {
                dsInfeedLengths.Clear();
                string[] levelIDs = value.Split(';');

                foreach (string levelID in levelIDs)
                {
                    string[] lANDid = levelID.Split(':');
                    float psLength;
                    float.TryParse(lANDid[0], out psLength);
                    dsInfeedLengths.Add(lANDid[1], psLength);

                }

                MultiShuttleinfo.dsInfeedConfig = value;
                if (!Core.Environment.Scene.Loading)
                {
                    Rebuild();
                }

            }
        }

        [CategoryAttribute("PS and DS Configuration")]
        [DescriptionAttribute("Pick Station outfeed length.")]
        [DisplayName("Pick Station Outfeed Length")]
        [PropertyOrder(4)]
        public Case.Components.OutfeedLength PSoutfeed
        {
            get { return MultiShuttleinfo.psOutfeed; }
            set
            {
                MultiShuttleinfo.psOutfeed = value;
                if (!Core.Environment.Scene.Loading)
                {
                    Rebuild();
                }
            }
        }

        [CategoryAttribute("PS and DS Configuration")]
        [DescriptionAttribute("Time before elevator will pick a single load")]
        [DisplayName("Pick Station Timeout")]
        [PropertyOrder(5)]
        public float PStimeout
        {
            get { return MultiShuttleinfo.psTimeout; }
            set
            {
                MultiShuttleinfo.psTimeout = value;
                if (OnPSTimeOutChanged != null)
                {
                    OnPSTimeOutChanged(this, new EventArgs());
                }
            }
        }

        //[CategoryAttribute("PS and DS Configuration")]
        //[DescriptionAttribute("Drop Station infeed length.")]
        //[DisplayName("Drop Station infeed Length")]
        //[PropertyOrder(4)]
        //public float DSinfeed
        //{
        //    get { return MultiShuttleinfo.dsInfeed; }
        //    set
        //    {
        //        MultiShuttleinfo.dsInfeed = value;
        //        Rebuild();
        //    }
        //}



        #endregion

        [Browsable(false)]
        public override Core.Assemblies.EventCollection Events
        {
            get
            {
                return base.Events;
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



        #endregion

        /// <summary>
        /// Generic property for a PLC of any type, DatCom, DCI etc it is set when the ControllerName is set
        /// </summary>
        [Category("Configuration")]
        [DisplayName("Controller Setup")]
        [PropertyAttributesProvider("DynamicPropertyAssemblyPLCconfig")]
        public MHEControl ControllerProperties { get; set; }

        [Category("Configuration")]
        [DisplayName("Controller")]
        [Description("Controller name that handles this conveyor")]
        [TypeConverter(typeof(CaseControllerConverter))] //TODO CaseControllerConverter is in the communication point move to a base class
        public virtual string ControllerName
        {
            get
            {
                return MultiShuttleinfo.ControllerName;
            }
            set
            {
                if (!value.Equals(MultiShuttleinfo.ControllerName))
                {
                    ControllerProperties = null;
                    MultiShuttleinfo.ProtocolInfo = null;
                }

                MultiShuttleinfo.ControllerName = value;
                if (value != null)
                {

                    ControllerProperties = StandardCase.SetMHEControl(MultiShuttleinfo, this);
                }
            }
        }

        public void DynamicPropertyAssemblyPLCconfig(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = Controller != null;
        }

        [CategoryAttribute("Configuration")]
        [PropertyOrder(1)]
        [DescriptionAttribute("Elevator No")]
        public int AisleNumber
        {
            get
            {
                return MultiShuttleinfo.AisleNo;
            }
            set
            {
                MultiShuttleinfo.AisleNo = value;
            }
        }

        public string LeftElevatorTaskDisplay
        {
            get;
            set;
        }

        public string RightElevatorTaskDisplay
        {
            get;
            set;
        }

        //[CategoryAttribute("Configuration")]
        //[DescriptionAttribute("Pick station conveyor length (meter)")]
        //[DisplayName("Pick station length")]
        //public float PickStationConveyorLength
        //{
        //    get
        //    {
        //        return MultiShuttleinfo.PickStationConveyorLength;
        //    }
        //    set
        //    {
        //        if (value > 0 && value <= 100 && MultiShuttleinfo.PickStationConveyorLength != value)
        //        {
        //            MultiShuttleinfo.PickStationConveyorLength = value;
        //            Rebuild();
        //        }
        //    }
        //}



        private ObservableCollection<ElevatorTask> elevatorTasks = new ObservableCollection<ElevatorTask>();
        private ObservableCollection<ShuttleTask> shuttleTasks = new ObservableCollection<ShuttleTask>();

        [Browsable(false)]
        public ObservableCollection<ElevatorTask> ElevatorTasks
        {
            get
            {
                return elevatorTasks;
            }
            set
            {
                elevatorTasks = value;
            }
        }

        [Browsable(false)]
        public ObservableCollection<ShuttleTask> ShuttleTasks
        {
            get
            {
                return shuttleTasks;
            }
            set
            {
                shuttleTasks = value;
            }
        }

        [Browsable(false)]
        public bool DropStationAvaiable { get; set; }
        [Browsable(false)]
        public Cycle Loading { get; set; }
        [Browsable(false)]
        public Cycle Unloading { get; set; }


        public event EventHandler<ArrivedOnElevatorEventArgs> OnArrivedAtElevatorConvPosA;
        public event EventHandler<ArrivedOnElevatorEventArgs> OnArrivedAtElevatorConvPosB;

        public event EventHandler<RackConveyorArrivalEventArgs> OnArrivedAtInfeedRackConvPosA;
        public event EventHandler<RackConveyorArrivalEventArgs> OnArrivedAtInfeedRackConvPosB;

        public event EventHandler<RackConveyorArrivalEventArgs> OnArrivedAtOutfeedRackConvPosA;
        public event EventHandler<RackConveyorArrivalEventArgs> OnArrivedAtOutfeedRackConvPosB;

        public event EventHandler<PickDropStationArrivalEventArgs> OnArrivedAtPickStationConvPosA;
        public event EventHandler<PickDropStationArrivalEventArgs> OnArrivedAtPickStationConvPosB;
        public event EventHandler<PickDropStationArrivalEventArgs> OnLoadTransferingToPickStation;

        public event EventHandler<PickDropStationArrivalEventArgs> OnArrivedAtDropStationConvPosA;
        public event EventHandler<PickDropStationArrivalEventArgs> OnArrivedAtDropStationConvPosB;

        public event EventHandler<TaskEventArgs> OnArrivedAtShuttle;

        public event EventHandler<TaskEventArgs> OnArrivedAtRackLocation;

        public static event EventHandler<LoadCreatedEventArgs> OnLoadCreated;
        //public event EventHandler<EventArgs> OnElevatorArrived;

        //public void ElevatorArrived(EventArgs eventArgs)
        //{
        //    if (OnElevatorArrived != null)
        //    {
        //        OnElevatorArrived(this, eventArgs);
        //    }
        //}

        [Browsable(false)]
        public TaskType TaskType { get; set; }

        public event EventHandler<TaskEventArgs> OnArrivedOntoShuttle;

        public virtual void ArrivedAtElevatorConvPosA(ArrivedOnElevatorEventArgs eventArgs)
        {
            if (OnArrivedAtElevatorConvPosA != null)
            {
                OnArrivedAtElevatorConvPosA(this, eventArgs);
            }
        }

        public virtual void ArrivedAtElevatorConvPosB(ArrivedOnElevatorEventArgs eventArgs)
        {
            if (OnArrivedAtElevatorConvPosB != null)
            {
                OnArrivedAtElevatorConvPosB(this, eventArgs);
            }
        }

        public virtual void ArrivedAtInfeedRackConvPosA(RackConveyorArrivalEventArgs eventArgs)
        {
            if (OnArrivedAtInfeedRackConvPosA != null)
            {
                OnArrivedAtInfeedRackConvPosA(this, eventArgs);
            }
        }

        public virtual void ArrivedAtInfeedRackConvPosB(RackConveyorArrivalEventArgs eventArgs)
        {
            if (OnArrivedAtInfeedRackConvPosB != null)
            {
                OnArrivedAtInfeedRackConvPosB(this, eventArgs);
            }
        }

        public virtual void ArrivedAtOutfeedRackConvPosA(RackConveyorArrivalEventArgs eventArgs)
        {
            if (OnArrivedAtOutfeedRackConvPosA != null)
            {
                OnArrivedAtOutfeedRackConvPosA(this, eventArgs);
            }
        }

        public virtual void ArrivedAtOutfeedRackConvPosB(RackConveyorArrivalEventArgs eventArgs)
        {
            if (OnArrivedAtOutfeedRackConvPosB != null)
            {
                OnArrivedAtOutfeedRackConvPosB(this, eventArgs);
            }
        }
        public virtual void ArrivedAtPickStationConvPosA(PickDropStationArrivalEventArgs eventArgs)
        {
            if (OnArrivedAtPickStationConvPosA != null)
            {
                OnArrivedAtPickStationConvPosA(this, eventArgs);
            }
        }
        public virtual void ArrivedAtPickStationConvPosB(PickDropStationArrivalEventArgs eventArgs)
        {
            if (OnArrivedAtPickStationConvPosB != null)
            {
                OnArrivedAtPickStationConvPosB(this, eventArgs);
            }
        }

        public virtual void LoadTransferingToPickStation(PickDropStationArrivalEventArgs eventArgs)
        {
            if (OnLoadTransferingToPickStation != null)
            {
                OnLoadTransferingToPickStation(this, eventArgs);
            }
        }

        public virtual void ArrivedAtDropStationConvPosA(PickDropStationArrivalEventArgs eventArgs)
        {
            if (OnArrivedAtDropStationConvPosA != null)
            {
                OnArrivedAtDropStationConvPosA(this, eventArgs);
            }
        }
        public virtual void ArrivedAtDropStationConvPosB(PickDropStationArrivalEventArgs eventArgs)
        {
            if (OnArrivedAtDropStationConvPosB != null)
            {
                OnArrivedAtDropStationConvPosB(this, eventArgs);
            }
        }

        public virtual void ArrivedAtRackLocation(TaskEventArgs eventArgs)
        {
            if (OnArrivedAtRackLocation != null)
            {
                OnArrivedAtRackLocation(this, eventArgs);
            }
        }

        public virtual void ArrivedAtShuttle(TaskEventArgs eventArgs)
        {
            if (OnArrivedAtShuttle != null)
            {
                OnArrivedAtShuttle(this, eventArgs);
            }
        }
        public virtual void LoadCreated(LoadCreatedEventArgs eventArgs)
        {
            if (OnLoadCreated != null)
            {
                OnLoadCreated(this, eventArgs);
            }
        }

    }
}
