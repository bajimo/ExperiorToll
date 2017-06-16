using Experior.Catalog.Dematic.Case.Components;
using Experior.Core.Assemblies;
using Experior.Core.Properties;
using Experior.Dematic.Base;
using Microsoft.DirectX;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using Experior.Dematic;
using System.Linq;
using Experior.Dematic.Base.Devices;
using Experior.Catalog.Dematic.Case;

namespace Experior.Catalog.Dematic.Storage.MultiShuttle.Assemblies
{

    public class MultiShuttle : Assembly, IControllable
    {
        // Note: Whenever a load moves on an PS, DS, Elevator or rack Conveyor the load always moves from Location A to Location B even if we have mixed infeeds.
        // In the case of mixed infeeds/outfeeds the where the elevator conveyor will go in both directions the yaw of the conveyor is changed so that loads always travel in the direction i.e. A to B.

        private float locationLength;
        private MultiShuttleDirections dir = MultiShuttleDirections.Infeed;
        public static List<MultiShuttle> Aisles = new List<MultiShuttle>();
        public List<DematicActionPoint> ConveyorLocations = new List<DematicActionPoint>();
        public List<RackConveyor> RackConveyors = new List<RackConveyor>();
        public List<DropStationConveyor> DropStationConveyors = new List<DropStationConveyor>();
        public List<PickStationConveyor> PickStationConveyors = new List<PickStationConveyor>();
        public Dictionary<int, MSlevel> shuttlecars = new Dictionary<int, MSlevel>();
        public List<Elevator> elevators = new List<Elevator>();
        public List<Box1> Racking = new List<Box1>();
        private List<LevelID> LevelHeightPickstations = new List<LevelID>();
        private List<LevelID> LevelHeightDropstations = new List<LevelID>();
        public List<int> driveThroughLocations = new List<int>();

        // private float PSDSstackHeight; // The height of the DS and PS conveyors
        private List<System.Windows.Forms.ToolStripItem> subMenu;

        public event EventHandler OnPSTimeOutChanged;

        List<float> heights = new List<float>(); // used to work out how tall the elevator mast needs to be

        public float RailLengthOffset;// The length of rail from position A of an outfeed rack conv to start of that conv (there can be no locations in this part of the rack).
        internal readonly MultiShuttleInfo MultiShuttleinfo;

        private bool constructing = true;

        public MultiShuttle(MultiShuttleInfo info) : base(info)
        {
            try
            {
                MultiShuttleinfo = info;
                AisleNumber = info.AisleNo;
                info.height = 0;
                info.transparency = MultiShuttleinfo.transparency;
                info.boxColor = Color.FromArgb(info.transparency, MultiShuttleinfo.boxColor);
                FrexPositions = info.frexPositions;
                Aisles.Add(this);
                DSinfeedConfig = info.dsInfeedConfig;
                PickStationConfig = info.pickStationConfig;
                DropStationConfig = info.dropStationConfig;
                RackHeight = info.rackHeight;
                RailLengthOffset = (RackConveyorLength / 2 - RackConveyorLength / 4); //section of rail where the rack conveyor is

                DriveThroughLocations = MultiShuttleinfo.DriveThroughLocations; //Sets the number of unused drive through locations
                SetLocationLength();

                if (locationLength <= 0)
                {
                    Core.Environment.Log.Write(Name + ": Configuration error! Raillength is too short (baylength is calculated to be < 0)", Color.Red);
                }

                SetElevatorOffset();

                Rebuild();
                Core.Environment.Scene.OnLoaded += Scene_OnLoaded;

                subMenu = new List<System.Windows.Forms.ToolStripItem>();
                subMenu.Add(new System.Windows.Forms.ToolStripMenuItem("Reset MS", Common.Icons.Get("fault")));
                subMenu[0].Click += new EventHandler(DematicMultiShuttleReset_Click);

                constructing = false;
            }
            catch (Exception e)
            {
                Core.Environment.Log.Write("Error in MultiShuttle constructor: " + e.Message, Color.Red);
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
                Core.Environment.Invoke(() => Reset());
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
            foreach (MSlevel shuttle in shuttlecars.Values)
            {
                shuttle.Reset();
            }

            elevators.ForEach(x => x.Reset());

            MultishuttleReset(new MultishuttleEvent(this));

            base.Reset();
        }

        public override void Dispose()
        {
            if (subMenu != null)
            {
                subMenu[0].Click -= new EventHandler(DematicMultiShuttleReset_Click);
            }
            Core.Environment.Scene.OnLoaded -= Scene_OnLoaded;
            Aisles.Remove(this);
            DropStationConveyors.Clear();
            ConveyorLocations.Clear();
            RackConveyors.Clear();
            PickStationConveyors.Clear();
            Racking.Clear();
            ClearShuttles();
            ClearRackConveyor();
            ClearElevators();
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

        private void SetLocationLength()
        {
            if (!MultiShuttleinfo.DriveThrough)
            {
                if (MultiShuttleinfo.frex)
                {
                    locationLength = (Raillength - (RackConveyorLength + ElevatorConveyorLength)) / (RackLocations);
                }
                else
                {
                    locationLength = (Raillength - RailLengthOffset) / RackLocations;
                }
            }
            else
            {
                if (ElevatorGap == 0)
                {
                    locationLength = (Raillength - RackConveyorLength * 2 - ElevatorConveyorLength) / (RackLocations - driveThroughLocations.Count);
                }
                else
                {
                    locationLength = (Raillength - ElevatorGap) / (RackLocations - driveThroughLocations.Count);
                }
            }
        }

        private void Rebuild()
        {
            try
            {
                if (Core.Environment.InvokeRequired)
                {
                    Core.Environment.Invoke(() => Rebuild());
                    return;
                }

                ClearShuttles();
                ClearRackConveyor();

                foreach (DropStationConveyor f in DropStationConveyors)
                {
                    Remove(f);
                    Remove(f as Core.IEntity);
                    f.Dispose();
                }

                elevators.ForEach((e) => { Remove(e); e.Dispose(); });

                foreach (PickStationConveyor c in PickStationConveyors)
                {
                    Remove(c);
                    Remove(c as Core.IEntity);
                    c.Dispose();
                }

                DropStationConveyors.Clear();
                ConveyorLocations.Clear();
                RackConveyors.Clear();
                ClearElevators();
                PickStationConveyors.Clear();

                CreateShuttles();

                ShowBoxGraphic = MultiShuttleinfo.showBoxGraphic;

                if (MultiShuttleinfo.ElevatorFL)
                {
                    BuildElevator(string.Format("L{0}", AisleNumber.ToString().PadLeft(2, '0')), FLElevatorType);//Rackconveyors, pickstation and dropstation conveyors are also build here as well
                }

                if (MultiShuttleinfo.ElevatorFR)
                {
                    BuildElevator(string.Format("R{0}", AisleNumber.ToString().PadLeft(2, '0')), FRElevatorType);//Rackconveyors, pickstation and dropstation conveyors are also build here as well
                }

                Core.Environment.SolutionExplorer.Refresh();
            }
            catch (Exception e)
            {
                Core.Environment.Log.Write(e.Message, Color.Red);
            }
        }

        [Browsable(false)]
        public float LocationLength
        {
            get
            {
                return locationLength;
            }
        }

        /// <summary>
        /// Uses all relevant heights so that the elevator mast height can be calculated
        /// </summary>
        private void SetMastHeight()
        {
            heights.Clear();
            LevelHeightDropstations.ForEach(x => heights.Add(x.Height));
            LevelHeightPickstations.ForEach(x => heights.Add(x.Height));
            heights.Add((Levels * DistanceLevels) + RackHeight);
            heights.Sort();

            elevators.ForEach(x => x.ElevatorHeight = heights[heights.Count - 1] + 0.2f); // pick largest value and add 0.2 so elevator does not go to the very end, 0.2 may be not needed however it will not hurt!

            //foreach (Elevator elev in elevators.Values)
            //{
            //    elev.ElevatorHeight = heights[heights.Count - 1] + 0.2f;
            //    //elev.LocalPosition = new Vector3(DriveThroughElevatorOffset, elev.ElevatorHeight / 2, elev.LocalPosition.Z); //also adjust the position so that elevator is not through the floor
            //}
        }

        private void ClearRackConveyor()
        {
            foreach (RackConveyor c in RackConveyors)
            {
                c.LocationA.OnEnter -= c.rackConvLocA_Enter;
                c.LocationB.OnEnter -= c.rackConvLocB_Enter;

                c.LocationA.Dispose();
                c.LocationB.Dispose();

                Remove(c);
                c.Dispose();
            }
        }

        private void ClearElevators()
        {
            foreach (var elevator in elevators)
            {
                Remove(elevator);
                elevator.Dispose();
            }
            elevators.Clear();
        }

        private void ClearShuttles()
        {
            foreach (MSlevel shuttle in shuttlecars.Values)
            {
                shuttle.Track.Dispose();
                shuttle.Vehicle.Dispose();
                Remove(shuttle as Core.IEntity);

                Remove(shuttle);
                shuttle.Dispose();
            }

            shuttlecars.Clear();
        }

        private void CreateShuttles()
        {
            for (var level = 1; level <= MultiShuttleinfo.ShuttleNumber; level++)
            {
                MSlevelInfo sInfo = new MSlevelInfo()
                {
                    multiShuttleinfo = MultiShuttleinfo,
                    level = level,
                    parentMultishuttle = this
                };

                sInfo.name = string.Format("{0}L{1}", AisleNumber.ToString().PadLeft(2, '0'), level);
                MSlevel shuttle2 = new MSlevel(sInfo);
                shuttle2.Track.Length = MultiShuttleinfo.raillength;

                shuttlecars.Add(level, shuttle2);
                Add(shuttle2, new Vector3(0, RackHeight + ((level - 1) * MultiShuttleinfo.DistanceLevels), 0));
            }
        }

        private void BuildElevator(string name, MultiShuttleDirections elevatorType)
        {
            var zCoord = -RackConveyorWidth / 2 - MultiShuttleinfo.carwidth / 2;
            Vector3 rackLocalPosition = new Vector3();
            var elevatorinfo = new ElevatorInfo()
            {
                Multishuttle = this,
                multishuttleinfo = MultiShuttleinfo,
                ElevatorName = name,
                ElevatorType = elevatorType
            };

            if (name.Substring(0, 1) == "R")
            {
                elevatorinfo.groupName = FRGroupName;
                elevatorinfo.Side = RackSide.Right;

                if (!SwitchSides)
                {
                    zCoord *= -1;
                }
            }
            else
            {
                elevatorinfo.groupName = FLGroupName;
                elevatorinfo.Side = RackSide.Left;

                if (SwitchSides)
                {
                    zCoord *= -1;
                }
            }

            var elevator = new Elevator(elevatorinfo);
            elevators.Add(elevator);
            Add(elevator);

            SetMastHeight();

            if (!MultiShuttleinfo.DriveThrough)
            {
                rackLocalPosition = BuildRackConveyors(elevator, zCoord, elevator.Side);
            }
            else// add a second set of rack conveyors for drive through
            {
                BuildRackConveyors(elevator, SwitchSides ? -zCoord : zCoord, RackSide.Left);
                BuildRackConveyors(elevator, SwitchSides ? zCoord : -zCoord, RackSide.Right);
            }

            if (MultiShuttleinfo.DriveThrough)
            {
                elevator.LocalPosition = new Vector3(ElevatorOffset, elevator.ElevatorHeight / 2, zCoord);
            }
            else
            {
                elevator.LocalPosition = new Vector3(rackLocalPosition.X + RackConveyorLength / 2 + ElevatorConveyorLength / 2, elevator.ElevatorHeight / 2, zCoord);
            }

            if (!MultiShuttleinfo.DriveThrough)
            {
                if (elevatorType == MultiShuttleDirections.Outfeed)
                {
                    elevator.LocalYaw = (float)Math.PI * 2;
                }
                else if (elevatorType == MultiShuttleDirections.Infeed)
                {
                    elevator.LocalYaw = (float)Math.PI;
                }
            }
            else
            {
                elevator.LocalYaw = (float)Math.PI;
            }
            CreatePickstationConveyors(elevator);
            CreateDropstationConveyors(elevator);
        }

        /// <summary>
        /// Create Rack Conveyors. The position of the rack conveyor is based on the shuttle position
        /// </summary>
        private Vector3 BuildRackConveyors(Elevator elevator, float zCoord, RackSide side)
        {
            Vector3 localPosition = new Vector3();
            for (var level = 1; level <= MultiShuttleinfo.ShuttleNumber; level++)
            {
                var rackConvInfo = new RackConveyorInfo()
                {
                    thickness = 0.05f,
                    length = RackConveyorLength,
                    width = RackConveyorWidth,
                    speed = ConveyorSpeed,
                    parentMultishuttle = this,
                    Color = Info.Color

                };

                var rackConveyor = new RackConveyor(rackConvInfo) { Side = side };
                RackSide rs = RackSide.NA;

                if (MultiShuttleinfo.DriveThrough)
                {
                    rackConveyor.RackConveyorType = elevator.ElevatorType;
                    if (elevator.ElevatorType == MultiShuttleDirections.Outfeed && side == RackSide.Left)
                    {
                        rs = RackSide.Right;
                    }
                    else if (elevator.ElevatorType == MultiShuttleDirections.Outfeed && side == RackSide.Right)
                    {
                        rs = RackSide.Left;
                    }
                    else
                    {
                        rs = side;
                    }
                }
                if (!MixedInfeedOutfeed)
                {
                    rackConveyor.RackConveyorType = elevator.ElevatorType;
                }
                else if (MixedInfeedOutfeed)
                {
                    if (LeftEvenLevelInfeed && level % 2 == 0 && side == RackSide.Left)
                    {
                        dir = MultiShuttleDirections.Infeed;
                    }
                    else if (LeftEvenLevelInfeed && level % 2 > 0 && side == RackSide.Right)
                    {
                        dir = MultiShuttleDirections.Infeed;
                    }
                    else if (!LeftEvenLevelInfeed && level % 2 > 0 && side == RackSide.Left)
                    {
                        dir = MultiShuttleDirections.Infeed;
                    }
                    else if (!LeftEvenLevelInfeed && level % 2 == 0 && side == RackSide.Right)
                    {
                        dir = MultiShuttleDirections.Infeed;
                    }
                    else
                    {
                        dir = MultiShuttleDirections.Outfeed;
                    }

                    rackConveyor.RackConveyorType = dir;
                }

                rackConveyor.Name = string.Format("{0}{1}{2}{3}",
                                                 AisleNumber.ToString().PadLeft(2, '0'),
                                                 (char)(MultiShuttleinfo.DriveThrough ? rs : side),
                                                 level.ToString().PadLeft(2, '0'),
                                                 (char)rackConveyor.RackConveyorType);

                Add(rackConveyor);
                rackConveyor.ConfigureRackConveyor(elevator, zCoord, level, MultiShuttleinfo.DriveThrough ? rs : side);
                localPosition = rackConveyor.LocalPosition;
            }
            return localPosition;
        }

        private void CreateDropOrPickConveyors<T>(LevelID level, Elevator elevator, string name) where T : IPickDropConvInfo, new()
        {
            string side = "L";
            if (elevator.Side == RackSide.Right) side = "R";

            T convInfo = new T() { thickness = 0.05f, Width = 0.5f, color = Info.color, parentMultiShuttle = this, elevator = elevator, level = level, name = string.Format("{0}{1}{2}{3}", AisleNumber.ToString().PadLeft(2, '0'), side, name, level.ID), };
            IPickDropConv conv = null;
            int elevatorSidePlacment = 1;

            if (name == "DS")
            {
                conv = new DropStationConveyor(convInfo as DropStationConveyorInfo)
                {
                    OutfeedSection = OutfeedLength._0mm,
                    InfeedSection = dsInfeedLengths[level.ID],
                };
                DropStationConveyors.Add(conv as DropStationConveyor);
                if (MultiShuttleinfo.DriveThrough)
                {
                    elevatorSidePlacment = -1;
                }
            }
            else if (name == "PS")
            {
                conv = new PickStationConveyor(convInfo as PickStationConveyorInfo)
                {
                    OutfeedSection = PSoutfeed,
                    InfeedSection = 0
                };
                PickStationConveyors.Add(conv as PickStationConveyor);
                ((PickStationConveyor)conv).RouteAvailableOverride = true;
            }

            conv.Side = elevator.Side;
            conv.AisleNumber = MultiShuttleinfo.AisleNo;
            conv.Height = level.Height;
            conv.Positions = 2;
            conv.Width = 0.750f;                                //TODO these values should come from the user
            conv.AccPitch = MultiShuttleinfo.PSAccPitch;

            ((StraightAccumulationConveyor)conv).TransportSection.Route.Motor.Speed = ConveyorSpeed;
            conv.ConvLocationConfiguration(level.ID);
            Add(conv as Assembly);

            if (conv is DropStationConveyor)
            {
                PositionDSConv((DropStationConveyor)conv);
            }
            else if (conv is PickStationConveyor)
            {
                PositionPSConv((PickStationConveyor)conv);
            }

            StraightAccumulationConveyor convAcc = conv as StraightAccumulationConveyor;
            if (!MultiShuttleinfo.DriveThrough)
            {
                convAcc.LocalPosition = new Vector3(elevator.LocalPosition.X + convAcc.Length / 2 + elevator.ElevatorConveyor.Length / 2, level.Height, elevator.LocalPosition.Z);
                if (convAcc is DropStationConveyor)
                {
                    convAcc.LocalYaw = (float)Math.PI;
                }
            }
            else
            {
                convAcc.LocalPosition = new Vector3(elevatorSidePlacment * (elevator.ElevatorConveyor.ConveyorLength / 2 + convAcc.Length / 2 + (ElevatorOffset * (convAcc is DropStationConveyor ? -1 : 1))), level.Height, elevator.LocalPosition.Z);
            }
        }

        private void PositionPSConv(PickStationConveyor convAcc)
        {
            Elevator elevator = convAcc.Elevator;
            LevelID level = convAcc.Level;

            float ConvLength = ((float)((2 * (int)PSAccPitch) + (int)convAcc.OutfeedSection) / 1000) + convAcc.InfeedSection;

            if (!MultiShuttleinfo.DriveThrough)
            {
                convAcc.LocalPosition = new Vector3(elevator.LocalPosition.X + ConvLength / 2 + elevator.ElevatorConveyor.Length / 2, level.Height, elevator.LocalPosition.Z);

            }
            else
            {
                convAcc.LocalPosition = new Vector3((elevator.ElevatorConveyor.ConveyorLength / 2 + ConvLength / 2 + ElevatorOffset), level.Height, elevator.LocalPosition.Z);
            }
        }

        private void PositionDSConv(DropStationConveyor convAcc)
        {
            Elevator elevator = convAcc.Elevator;
            LevelID level = convAcc.Level;

            if (!MultiShuttleinfo.DriveThrough)
            {
                convAcc.LocalPosition = new Vector3(elevator.LocalPosition.X + convAcc.Length / 2 + elevator.ElevatorConveyor.Length / 2, level.Height, elevator.LocalPosition.Z);
                convAcc.LocalYaw = (float)Math.PI;
            }
            else
            {
                convAcc.LocalPosition = new Vector3((elevator.ElevatorConveyor.ConveyorLength / 2 + convAcc.Length / 2  -ElevatorOffset), level.Height, elevator.LocalPosition.Z);
            }
        }

        private void CreateDropstationConveyors(Elevator elevator)
        {
            if (!MixedInfeedOutfeed && elevator.ElevatorType == MultiShuttleDirections.Outfeed || MixedInfeedOutfeed || MultiShuttleinfo.DriveThrough)
            {
                foreach (LevelID level in LevelHeightDropstations)
                {
                    try
                    {
                        CreateDropOrPickConveyors<DropStationConveyorInfo>(level, elevator, "DS");
                    }
                    catch (KeyNotFoundException ex)
                    {
                        Log.Write("\"DropStation conveyor infeed lengths\" IDs do not match  \"Drop Station infeed Length Config\" IDs ", Color.Red);
                        Log.Write(ex.Message, Color.Red);
                    }
                }
            }
        }

        private void CreatePickstationConveyors(Elevator elevator)
        {
            if (!MixedInfeedOutfeed && elevator.ElevatorType == MultiShuttleDirections.Infeed || MixedInfeedOutfeed || MultiShuttleinfo.DriveThrough)
            {
                foreach (LevelID level in LevelHeightPickstations)
                {
                    CreateDropOrPickConveyors<PickStationConveyorInfo>(level, elevator, "PS");
                }
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

            float length = Raillength - ShuttleCarLength / 2;
            if (MultiShuttleinfo.DriveThrough)
            {
                length = Raillength + 0.5f;
            }

            Box1Info boxInfo = new Box1Info()
            {
                thickness = 0.01f,
                length = length,
                width = Rackwidth,
                height = ((MultiShuttleinfo.ShuttleNumber * DistanceLevels) + MultiShuttleinfo.rackAdj),
                boxColor = System.Drawing.Color.FromArgb(Transparency, MultiShuttleinfo.boxColor)
            };

            float xPos = -(ShuttleCarLength + boxInfo.thickness) / 2;
            if (MultiShuttleinfo.DriveThrough)
            {
                xPos = 0;
            }

            Box1 box1 = new Box1(boxInfo);
            Racking.Add(box1);
            Add(box1, new Vector3(xPos, RackHeightOffset - RackHeight, boxInfo.thickness + ShuttleCarWidth / 2));

            Box1 box2 = new Box1(boxInfo);
            Racking.Add(box2);
            Add(box2, new Vector3(xPos, RackHeightOffset - RackHeight, -(boxInfo.width + boxInfo.thickness + ShuttleCarWidth / 2)));
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

        /// <summary>
        /// Get the group name from a conveyor location
        /// </summary>
        /// <param name="loc">A conveyor location</param>
        /// <returns>A Single character for the group name</returns>
        public string ElevatorGroup(string loc)
        {
            string elevatorName = string.Format("{0}{1}", (char)loc.Side(), AisleNumber.ToString().PadLeft(2, '0'));
            return elevators.First(x => x.ElevatorName == elevatorName).GroupName;
        }

        private IController controller;
        private MHEControl controllerProperties;

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

        private void controller_OnControllerRenamedEvent(object sender, EventArgs e)
        {
            MultiShuttleinfo.ControllerName = ((Assembly)sender).Name;
            Core.Environment.Properties.Refresh();
        }

        private void controller_OnControllerDeletedEvent(object sender, EventArgs e)
        {
            if (controller != null)
            {
                controller.OnControllerDeletedEvent -= controller_OnControllerDeletedEvent;
            }

            ControllerName = "No Controller";
            Controller = null;
            if (ControllerProperties != null)
            {
                ControllerProperties.Dispose();
            }
        }

        /// <summary>
        /// The controller will assign a new elevator task if false
        /// set this in a MHEcontrol object and use public void GetNewElevatorTask
        ///
        /// </summary>
        [Browsable(false)]
        public bool AutoNewElevatorTask
        {
            get { return MultiShuttleinfo.autoNewElevatorTask; }
            set
            {
                MultiShuttleinfo.autoNewElevatorTask = value;
            }
        }

        #region User Interface

        #region Rack Configuration

        [PropertyOrder(1)]
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
                    if (!Core.Environment.Scene.Loading && !constructing)
                    {
                        Rebuild();
                    }
                }
            }
        }

        [PropertyOrder(2)]
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
                    if (!Core.Environment.Scene.Loading && !constructing)
                    {
                        Rebuild();
                    }
                }
            }
        }

        [PropertyOrder(3)]
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
                    if (!Core.Environment.Scene.Loading && !constructing)
                    {
                        Rebuild();
                    }
                }
            }
        }

        [Category("Rack Configuration")]
        [PropertyOrder(4)]
        [DescriptionAttribute("The number of locations in rack (presumably evenly distributed over the length of the rack)")]
        [DisplayName("Number of Locations")]
        public int RackLocations
        {
            get
            {
                return MultiShuttleinfo.rackLocations;
            }
            set
            {
                if (MultiShuttleinfo.rackLocations != value && value > 1)
                {
                    MultiShuttleinfo.rackLocations = value;
                    SetLocationLength();
                    //locationLength = (Raillength - RackConveyorLength * 2 - ElevatorConveyorLength) / RackLocations;
                }
            }
        }

        [PropertyOrder(5)]
        [Category("Rack Configuration")]
        [DescriptionAttribute("Height of the rack, shuttles and rack conveyors only. This is independant of height of the entire assembly and allows you to move the rack independently of the DS, PS and elevators.")]
        [DisplayName("Rack Height")]
        //[PropertyAttributesProvider("DynamicPropertyDriveThrough")]
        public float RackHeight
        {
            get
            {
                return this.MultiShuttleinfo.rackHeight;
            }
            set
            {
                foreach (RackConveyor rackConveyor in this.RackConveyors)
                {
                    float num = value - this.MultiShuttleinfo.rackHeight;
                    rackConveyor.LocalPosition = new Vector3(rackConveyor.LocalPosition.X, rackConveyor.LocalPosition.Y + num, rackConveyor.LocalPosition.Z);
                }
                this.MultiShuttleinfo.rackHeight = value;

                if (!Core.Environment.Scene.Loading)
                {
                    this.SetMastHeight();
                    this.ClearShuttles();
                    this.CreateShuttles();
                }
            }
        }

        [PropertyOrder(6)]
        [Category("Rack Configuration")]
        [Description("Rack conveyor length (meter)")]
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
                    if (!Core.Environment.Scene.Loading && !constructing)
                    {
                        Rebuild();
                    }
                }
            }
        }

        [PropertyOrder(7)]
        [Category("Rack Configuration")]
        [Description("Bay positions that are not used to allow for elevators and rack conveyor on drive through aisles - e.g: 10,11,12")]
        [DisplayName("Unused DriveThrough")]
        [PropertyAttributesProvider("DynamicPropertyDriveThrough")]
        public string DriveThroughLocations
        {
            get { return MultiShuttleinfo.DriveThroughLocations; }
            set
            {
                List<int> newDTLocations = new List<int>();
                string[] splitLocs = value.Split(',');
                foreach (string loc in splitLocs)
                {
                    int result;
                    if (int.TryParse(loc, out result))
                    {
                        newDTLocations.Add(result);
                    }
                    else
                    {
                        return;
                    }
                }
                driveThroughLocations = newDTLocations;
                MultiShuttleinfo.DriveThroughLocations = value;
            }
        }

        [PropertyOrder(8)]
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
                    if (!Core.Environment.Scene.Loading && !constructing)
                    {
                        Rebuild();
                    }
                }
            }
        }

        [Category("Rack Configuration")]
        [PropertyOrder(10)]
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

                    elevators.ForEach(x => x.ElevatorConveyorSpeed = value);

                    foreach (PickStationConveyor pickstation in PickStationConveyors)
                    {
                        pickstation.Route.Motor.Speed = value;
                    }
                }
            }
        }

        [Category("Rack Configuration")]
        [PropertyOrder(12)]
        [DescriptionAttribute("The time it takes for the shuttle to set down a load at position 1")]
        [DisplayName("Time to Depth 1")]
        public float TimeToPos1
        {
            get { return MultiShuttleinfo.timeToPos1; }
            set { MultiShuttleinfo.timeToPos1 = value; }
        }

        [Category("Rack Configuration")]
        [PropertyOrder(14)]
        [DescriptionAttribute("The time it takes for the shuttle to set down a load at position 2")]
        [DisplayName("Time to Depth 2")]
        public float TimeToPos2
        {
            get { return MultiShuttleinfo.timeToPos2; }
            set { MultiShuttleinfo.timeToPos2 = value; }
        }

        [PropertyOrder(20)]
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
                    if (!Core.Environment.Scene.Loading && !constructing)
                    {
                        Rebuild();
                    }
                }
            }
        }

        [PropertyOrder(24)]
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
                        MultiShuttleinfo.DriveThrough = false;
                    }
                    Core.Environment.Properties.Refresh();
                    if (!Core.Environment.Scene.Loading && !constructing)
                    {
                        Rebuild();
                    }
                }
            }
        }

        [PropertyOrder(28)]
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
                    if (!Core.Environment.Scene.Loading && !constructing)
                    {
                        Rebuild();
                    }
                }
            }
        }

        [PropertyOrder(28)]
        [Category("Rack Configuration")]
        [DescriptionAttribute("If true then x location 1 will be at the other end of the aisle")]
        [DisplayName("Switch X Locations")]
        [PropertyAttributesProvider("DynamicPropertyDriveThrough")]
        public bool SwitchEnds
        {
            get
            {
                return MultiShuttleinfo.SwitchEnds;
            }
            set
            {
                MultiShuttleinfo.SwitchEnds = value;
            }
        }


        [Category("Rack Configuration")]
        [PropertyOrder(30)]
        [DescriptionAttribute("Show a graphic of the rack.")]
        [DisplayName("Show Rack Graphic")]
        public bool ShowBoxGraphic
        {
            get { return MultiShuttleinfo.showBoxGraphic; }
            set
            {
                MultiShuttleinfo.showBoxGraphic = value;
                if (value)
                {
                    ClearRackingBoxes();
                    CreateRackingBoxes();
                    foreach (MSlevel s in shuttlecars.Values)
                    {
                        s.Track.Visible = false;
                    }
                    Core.Environment.Properties.Refresh();
                }
                else
                {
                    ClearRackingBoxes();
                    foreach (MSlevel s in shuttlecars.Values)
                    {
                        s.Track.Visible = true;
                    }
                    Core.Environment.Properties.Refresh();
                }
            }
        }

        [Category("Rack Configuration")]
        [PropertyOrder(31)]
        [DescriptionAttribute("Colour")]
        [DisplayName("Racking Colour")]
        [PropertyAttributesProvider("DynamicPropertyShowRackGraphic")]
        public Color BoxColour
        {
            get { return MultiShuttleinfo.boxColor; }
            set
            {
                MultiShuttleinfo.boxColor = value;
                ClearRackingBoxes();
                CreateRackingBoxes();
            }
        }

        [Category("Rack Configuration")]
        [PropertyOrder(32)]
        [DescriptionAttribute("Transparency")]
        [DisplayName("Racking Colour Transparency")]
        [PropertyAttributesProvider("DynamicPropertyShowRackGraphic")]
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

        [PropertyOrder(33)]
        [Category("Rack Configuration")]
        [DescriptionAttribute("The rack width of a side.")]
        [DisplayName("Graphic Rack Width")]
        [PropertyAttributesProvider("DynamicPropertyShowRackGraphic")]
        public float Rackwidth
        {
            get { return MultiShuttleinfo.rackWidth; }
            set
            {
                MultiShuttleinfo.rackWidth = value;
                ClearRackingBoxes();
                CreateRackingBoxes();
            }
        }

        [PropertyOrder(34)]
        [Category("Rack Configuration")]
        [Description("Change the height of the rack graphic by this value")]
        [DisplayName("Rack Graphic Height Adj")]
        [PropertyAttributesProvider("DynamicPropertyShowRackGraphic")]
        public float RackAdj
        {
            get { return MultiShuttleinfo.rackAdj; }
            set
            {
                MultiShuttleinfo.rackAdj = value;
                ClearRackingBoxes();
                CreateRackingBoxes();
            }
        }

        public void DynamicPropertyShowRackGraphic(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = ShowBoxGraphic;
        }

        #endregion

        #region Elevator Configuration

        [PropertyOrder(1)]
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
                    if (!Core.Environment.Scene.Loading && !constructing)
                    {
                        Rebuild();
                    }
                }
            }
        }

        [PropertyOrder(2)]
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
                    if (!Core.Environment.Scene.Loading && !constructing)
                    {
                        Rebuild();
                    }
                }
            }
        }

        [PropertyOrder(3)]
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
                    if (!Core.Environment.Scene.Loading && !constructing)
                    {
                        Rebuild();
                    }
                }
            }
        }

        [PropertyOrder(4)]
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
                    if (!Core.Environment.Scene.Loading && !constructing)
                    {
                        Rebuild();
                    }
                }
            }
        }

        [PropertyOrder(5)]
        [CategoryAttribute("Elevator Configuration")]
        [DisplayName("FL Group Name")]
        public string FLGroupName
        {
            get { return MultiShuttleinfo.flElevatorGroupName; }
            set
            {
                MultiShuttleinfo.flElevatorGroupName = value;
                elevators.ForEach(x => x.GroupName = x.Side == RackSide.Left ? value : x.GroupName);
            }
        }

        [PropertyOrder(6)]
        [CategoryAttribute("Elevator Configuration")]
        [DisplayName("FR Group Name")]
        public string FRGroupName
        {
            get { return MultiShuttleinfo.frElevatorGroupName; }
            set
            {
                MultiShuttleinfo.frElevatorGroupName = value;
                elevators.ForEach(x => x.GroupName = x.Side == RackSide.Right ? value : x.GroupName);
            }
        }

        public void DynamicPropertyNotMixed(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = !MixedInfeedOutfeed;
        }

        [PropertyOrder(30)]
        [CategoryAttribute("Elevator Configuration")]
        [DescriptionAttribute("Use rack that extends past the elevators. This must be included in the total rail length ")]
        [DisplayName("Front Rack Extension")]
        public bool FREX
        {
            get
            {
                return MultiShuttleinfo.frex;
            }
            set
            {
                MultiShuttleinfo.frex = value;
                if (!value)
                {
                    FrexPositions = 0;
                    //FrexLevels = string.Empty;
                    //FrexElevatorGap = 0;
                }
                Core.Environment.Properties.Refresh();
            }
        }

        [PropertyOrder(32)]
        [CategoryAttribute("Elevator Configuration")]
        [DescriptionAttribute("The number of positions that will extend past the front. Not including the gap for the elevator.")]
        [DisplayName("Front Rack Extension positions")]
        [PropertyAttributesProvider("DynamicPropertyFREX")]
        public int FrexPositions
        {
            get
            {
                return MultiShuttleinfo.frexPositions;
            }
            set
            {
                MultiShuttleinfo.frexPositions = value;
                Core.Environment.Invoke(() => SetElevatorOffset());
            }
        }

        [PropertyOrder(32)]
        [CategoryAttribute("Elevator Configuration")]
        [DescriptionAttribute("The number of positions that the elevator and the rack conveyor will take up. These locations are not active location that can be used by the shuttle but are still counted as positions")]
        [DisplayName("PSDS Locations")]
        [PropertyAttributesProvider("DynamicPropertyFREX")]
        public int PSDSlocations
        {
            get { return MultiShuttleinfo.psdsLocations; }
            set
            {
                MultiShuttleinfo.psdsLocations = value;
            }
        }

        [PropertyOrder(33)]
        [Category("Elevator Configuration")]
        [Description("Gap between racking positions to allow for the elevator and rack conveyors, if set to 0 then the length of conveyors will be used")]
        [DisplayName("Elevator Gap")]
        [PropertyAttributesProvider("DynamicPropertyDriveThrough")]
        public float ElevatorGap
        {
            get { return MultiShuttleinfo.ElevatorGap; }
            set
            {
                MultiShuttleinfo.ElevatorGap = value;
                SetLocationLength();
            }
        }

        [PropertyOrder(34)]
        [Category("Elevator Configuration")]
        [Description("Switch naming of elevators and rack conveyors so that left is right and right is left")]
        [DisplayName("Side Switch")]
        public bool SwitchSides
        {
            get { return MultiShuttleinfo.SideSwitch; }
            set
            {
                MultiShuttleinfo.SideSwitch = value;

                if (!Core.Environment.Scene.Loading && !constructing)
                {
                    Rebuild();
                }
            }
        }

        /// <summary>
        /// Sets how far the evevator is from the front of the rail.
        /// </summary>
        public void SetElevatorOffset()
        {
            if (FREX)
            {
                ElevatorOffset = -((RackConveyorLength + ElevatorConveyorLength) + (LocationLength * FrexPositions));
            }
            else if (!MultiShuttleinfo.DriveThrough)
            {
                ElevatorOffset = 0;
            }
        }

        public void DynamicPropertyFREX(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = FREX;
        }

        public void DynamicPropertyDriveThrough(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = MultiShuttleinfo.DriveThrough;
        }

        [CategoryAttribute("Elevator Configuration")]
        [DescriptionAttribute("The (x) offset that the elevator and all associated conveyors.")]
        [DisplayName("Elevator Offset (m).")]
        [PropertyAttributesProvider("DynamicPropertyDriveThrough")]
        public float ElevatorOffset
        {
            get
            {
                return MultiShuttleinfo.elevatorOffset;
            }
            set
            {
                {
                    MultiShuttleinfo.elevatorOffset = value;

                    if (!Core.Environment.Scene.Loading && !constructing)
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
                    if (!Core.Environment.Scene.Loading && !constructing)
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
                    if (!Core.Environment.Scene.Loading && !constructing)
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
                    elevators.ForEach(x => x.ElevatorSpeed = value);
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
                    foreach (MSlevel car in shuttlecars.Values)
                    {
                        if (car.Track.Route.Motor.Speed > 0)
                        {
                            car.Track.Route.Motor.Speed = MultiShuttleinfo.shuttlecarSpeed;
                        }
                        else
                        {
                            car.Track.Route.Motor.Speed = -MultiShuttleinfo.shuttlecarSpeed;
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
                    if (!Core.Environment.Scene.Loading && !constructing)
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
                    if (!Core.Environment.Scene.Loading && !constructing)
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
                LevelHeightPickstations.Clear();

                string[] levelIDs = value.Split(';');

                foreach (string levelID in levelIDs)
                {
                    string[] lANDid = levelID.Split(':');

                    float psHeight;
                    if (!float.TryParse(lANDid[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out psHeight))
                    //if (!float.TryParse(lANDid[0], out psHeight))
                    {
                        Log.Write(string.Format("{0}: Error parsing the pick station height from the configuration ({1}) the model will not build correctly", Name, lANDid[0]));
                    }
                    else
                    {
                        LevelID lID = new LevelID { Height = psHeight, ID = lANDid[1] };
                        LevelHeightPickstations.Add(lID);
                    }
                }

                SetMastHeight();

                if (!Core.Environment.Scene.Loading && !constructing)
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
                LevelHeightDropstations.Clear();

                string[] levelIDs = value.Split(';');

                foreach (string levelID in levelIDs)
                {
                    string[] lANDid = levelID.Split(':');
                    float psHeight;

                    if (!float.TryParse(lANDid[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out psHeight))
                    //if (!float.TryParse(lANDid[0], out psHeight))
                    {
                        Log.Write(string.Format("{0}: Error parsing the drop station height from the configuration ({1}) the model will not build correctly", Name, lANDid[0]));
                    }
                    else
                    {
                        LevelID lID = new LevelID { Height = psHeight, ID = lANDid[1] };
                        LevelHeightDropstations.Add(lID);
                    }
                }

                SetMastHeight();

                if (!Core.Environment.Scene.Loading && !constructing)
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

                    //float.TryParse(lANDid[0], out psLength);
                    if (!float.TryParse(lANDid[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out psLength))
                    {
                        Log.Write(string.Format("{0}: Error parsing the drop station height from the configuration ({1}) the model will not build correctly", Name, lANDid[0]));
                    }
                    else
                    {
                        dsInfeedLengths.Add(lANDid[1], psLength);
                    }

                }

                MultiShuttleinfo.dsInfeedConfig = value;
                if (!Core.Environment.Scene.Loading && !constructing)
                {
                    Rebuild();
                }

            }
        }

        [CategoryAttribute("PS and DS Configuration")]
        [DescriptionAttribute("Pick Station outfeed length.")]
        [DisplayName("Pick Station Outfeed Length")]
        [PropertyOrder(4)]
        public OutfeedLength PSoutfeed
        {
            get { return MultiShuttleinfo.psOutfeed; }
            set
            {
                MultiShuttleinfo.psOutfeed = value;
                if (!Core.Environment.Scene.Loading && !constructing)
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

        [Category("PS and DS Configuration")]
        [Description("Accumulation pitch of the accumulation conveyor")]
        [DisplayName("PS Accumulation Pitch")]
        [PropertyOrder(6)]
        public AccumulationPitch PSAccPitch
        {
            get { return MultiShuttleinfo.PSAccPitch; }
            set
            {
                MultiShuttleinfo.PSAccPitch = value;

                foreach (PickStationConveyor conv in PickStationConveyors)
                {
                    conv.AccPitch = value;
                }

                foreach (PickStationConveyor conv in PickStationConveyors)
                {
                    PositionPSConv(conv);
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

        [Category("Position")]
        [DisplayName("Height")]
        [Description("Total Height of the MultiShuttle")]
        [TypeConverter()]
        public float MSHeight
        {
            get { return Position.Y; }
            set
            {
                Position = new Vector3(Position.X, value, Position.Z);
                Core.Environment.Properties.Refresh();
            }
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

        [Browsable(true)]
        public override Color Color
        {
            get
            {
                return base.Color;
            }

            set
            {
                base.Color = value;
                foreach (PickStationConveyor conv in PickStationConveyors)
                {
                    conv.Color = value;
                }
                foreach (DropStationConveyor conv in DropStationConveyors)
                {
                    conv.Color = value;
                }
                foreach (RackConveyor conv in RackConveyors)
                {
                    conv.Color = value;
                }
            }
        }



        #endregion

        /// <summary>
        /// Generic property for a PLC of any type, DatCom, DCI etc it is set when the ControllerName is set
        /// </summary>
        [Category("Configuration")]
        [DisplayName("Controller Setup")]
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
                if (!value.Equals(MultiShuttleinfo.ControllerName)) // The controller has been swapped for another controller in the model
                {
                    if (ControllerProperties != null)
                    {
                        ControllerProperties.Dispose();
                    }

                    ControllerProperties = null;
                    MultiShuttleinfo.ProtocolInfo = null;
                    Controller = null;
                }

                MultiShuttleinfo.ControllerName = value;

                if (value != null)
                {
                    ControllerProperties = StandardCase.SetMHEControl(MultiShuttleinfo, this);
                    if (ControllerProperties == null)
                    {
                        MultiShuttleinfo.ControllerName = "No Controller";
                    }
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
                string aNum = value.ToString().PadLeft(2, '0');

                foreach (DematicActionPoint dAP in ConveyorLocations)
                {
                    dAP.LocName = aNum + dAP.LocName.Substring(2); // takes the form aasyyxz: aa=aisle, s = side, yy = level, x = conv type see enum ConveyorTypes , Z = loc A or B e.g. 01R05OA
                }

                foreach (Elevator e in elevators)
                {
                    e.ElevatorName = e.ElevatorName.Substring(0, 1) + aNum;
                }

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
        public event EventHandler<ElevatorTasksStatusChangedEventArgs> OnElevatorTasksStatusChanged;

        public event EventHandler<RackConveyorArrivalEventArgs> OnArrivedAtInfeedRackConvPosA;
        public event EventHandler<RackConveyorArrivalEventArgs> OnArrivedAtInfeedRackConvPosB;

        public event EventHandler<RackConveyorArrivalEventArgs> OnArrivedAtOutfeedRackConvPosA;
        public event EventHandler<RackConveyorArrivalEventArgs> OnArrivedAtOutfeedRackConvPosB;

        public event EventHandler<PickDropStationArrivalEventArgs> OnArrivedAtPickStationConvPosA;
        public event EventHandler<PickDropStationArrivalEventArgs> OnArrivedAtPickStationConvPosB;
        public event EventHandler<PickDropStationArrivalEventArgs> OnLoadTransferingToPickStation;

        public event EventHandler<PickDropStationArrivalEventArgs> OnArrivedAtDropStationConvPosA;
        public event EventHandler<PickDropStationArrivalEventArgs> OnArrivedAtDropStationConvPosB;
        public event EventHandler<DropStationConvClearEventArgs> OnDropStationConvClear;

        public event EventHandler<TaskEventArgs> OnArrivedAtShuttle;
        public event EventHandler<MultishuttleVehicleEvent> OnVehicleException;

        public event EventHandler<TaskEventArgs> OnArrivedAtRackLocation;
        public static event EventHandler<LoadCreatedEventArgs> OnLoadCreated;

        public event EventHandler<MultishuttleEvent> OnReset; 

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

        //public event EventHandler<TaskEventArgs> OnArrivedOntoShuttle;

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

        public virtual void ElevatorTasksStatusChanged(ElevatorTasksStatusChangedEventArgs eventArgs)
        {
            if (OnElevatorTasksStatusChanged != null)
            {
                OnElevatorTasksStatusChanged(this, eventArgs);
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

        public virtual void DropStationConvClear(DropStationConvClearEventArgs eventArgs)
        {
            if (OnDropStationConvClear != null)
            {
                OnDropStationConvClear(this, eventArgs);
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

        public virtual void VehicleException(MultishuttleVehicleEvent eventArgs)
        {
            if (OnVehicleException != null)
            {
                OnVehicleException(this, eventArgs);
            }
        }

        public virtual void LoadCreated(LoadCreatedEventArgs eventArgs)
        {
            if (OnLoadCreated != null)
            {
                OnLoadCreated(this, eventArgs);
            }
        }

        protected virtual void MultishuttleReset(MultishuttleEvent e)
        {
            OnReset?.Invoke(this, e);
        }
    }
}