using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System;
using Microsoft.DirectX;
using Experior.Core.Properties;
using Experior.Core.Parts;
using Experior.Dematic.Storage.Base;
using Experior.Core.Assemblies;
using Experior.Core.TransportSections;
using Experior.Core.Routes;
using Experior.Dematic;
using Experior.Dematic.Base;
using Experior.Catalog.Dematic.Case.Components;
using Experior.Catalog.Assemblies;

namespace Experior.Catalog.Dematic.Storage.Assemblies
{       
    public enum MultiShuttleDirections
    {
        Infeed,
        Outfeed
    }

    public class DematicMultiShuttle : Assembly
    {
        public static List<DematicMultiShuttle> AllCranes = new List<DematicMultiShuttle>();
        public static List<IMultiShuttleController> AllControllers = new List<IMultiShuttleController>();
        internal DematicMultiShuttleInfo MultiShuttleinfo;
        internal Dictionary<string, ActionPoint> conveyorlocations = new Dictionary<string, ActionPoint>();
        [Browsable(false)] public Dictionary<string, RackConveyor> RackConveyors = new Dictionary<string, RackConveyor>();
        [Browsable(false)] public List<RackConveyor> RackConveyorsList = new List<RackConveyor>();
        [Browsable(false)] public Dictionary<string, OutfeedDropStationConveyor> DropStationPoints = new Dictionary<string, OutfeedDropStationConveyor>();
        [Browsable(false)] public List<InfeedPickStationConveyor> PickStationConveyors = new List<InfeedPickStationConveyor>();
        [Browsable(false)] public Dictionary<string, ActionPoint> PickStationNameToActionPoint = new Dictionary<string, ActionPoint>();

        private float bayLength;
        public HashSet<Case_Load> caseloads = new HashSet<Case_Load>();
        public Dictionary<int, Shuttle> shuttlecars = new Dictionary<int, Shuttle>();
        public Dictionary<string, MultiShuttleElevator> elevators = new Dictionary<string, MultiShuttleElevator>();
        private List<System.Windows.Forms.ToolStripItem> subMenu;

        public enum OutFeedNamingConventions
        {
            OLD_POS1_POS2_002_001,
            NEW_POS1_POS2_001_002,
        }

        [Browsable(false)] public string POS1 = "001";
        [Browsable(false)] public string POS2 = "002";
        [Browsable(false)] public string POS1OUTFEED = "001";
        [Browsable(false)] public string POS2OUTFEED = "002";

        [Browsable(false)]
        public float BayLength
        {
            get { return bayLength; }
        } 

        internal IMultiShuttleController control;

        [Browsable(false)]
        public IMultiShuttleController Control
        {
            get { return control; }
        }

        #region constructor

        public DematicMultiShuttle(DematicMultiShuttleInfo info) : base(info)
        {
            info.height = 0;
            MultiShuttleinfo = info;            
            AllCranes.Add(this);  
            Rebuild();
            Core.Environment.Scene.OnLoaded += SetController;
            subMenu = new List<System.Windows.Forms.ToolStripItem>();
            subMenu.Add(new System.Windows.Forms.ToolStripMenuItem("Reset MS", Common.Icons.Get("fault")));
            subMenu[0].Click += new EventHandler(DematicMultiShuttleReset_Click);
        }
        #endregion

        void DematicMultiShuttleReset_Click(object sender, EventArgs e)
        {
            if (Core.Environment.InvokeRequired)
            {
                Core.Environment.InvokeEvent(new Action(Reset));
                return;
            }

            Reset();
        }

        #region Overriding Assembly methods

        public override List<System.Windows.Forms.ToolStripItem> ShowContextMenu()
        {
            return subMenu;
        }

        public override void Reset()
        {
            if (control != null) { control.Reset(); }                

            foreach (Case_Load caseload in caseloads)
            {
                caseload.Deletable = true;
                caseload.UserDeletable = true;
                caseload.Dispose();
            }

            caseloads.Clear();
            foreach (Shuttle shuttle in shuttlecars.Values){shuttle.Reset();}
            foreach (MultiShuttleElevator elevator in elevators.Values){ elevator.Reset();}
            base.Reset();
        }
       
        public override void Dispose()
        {
            Core.Environment.Scene.OnLoaded -= SetController;
            if (control != null) { control.RemoveMultiShuttle(this); }
                
            control = null;
            AllCranes.Remove(this);
            DropStationPoints.Clear();
            conveyorlocations.Clear();
            RackConveyors.Clear();
            RackConveyorsList.Clear();
            elevators.Clear();
            PickStationConveyors.Clear();
            PickStationNameToActionPoint.Clear();

            base.Dispose();
        }

        public override string Category
        {
            get { return "DematicMultiShuttle"; }
        }

        public override Image Image
        {
            get
            {                      
                return Common.Icons.Get("DematicMultiShuttle");
            }
        }

        #endregion

        #region Helper methods

        private void Rebuild()
        {
            if (Core.Environment.InvokeRequired)
            {
                Core.Environment.InvokeEvent(new Action(Rebuild));
                return;
            }

            //Naming convention
            if (OutfeedNamingConvention == OutFeedNamingConventions.NEW_POS1_POS2_001_002)
            {
                POS1OUTFEED = "002";
                POS2OUTFEED = "001";
                //001, 002, 001, 002 for infeed
                //001, 002, 001, 002 for outfeed
            }
            else
            {
                POS1OUTFEED = "001";
                POS2OUTFEED = "002";
                //001, 002, 001, 002 for infeed
                //002, 001, 002, 001 for outfeed
                //Note: All variables in the MS has been named after this old naming convention.
            }

            if (control != null) { control.Clear(); }
                
            foreach (Shuttle shuttle in shuttlecars.Values)
            {
                Remove(shuttle as Core.IEntity);
                RemovePart(shuttle);
                shuttle.Dispose();
            }

            shuttlecars.Clear();

            foreach (ActionPoint loc in conveyorlocations.Values) { loc.Dispose(); }
                
            foreach (RackConveyor c in RackConveyors.Values)
            {  
                c.Location1.OnEnter -= c.rackConvLoc1_Enter;
                c.Location2.OnEnter -= c.rackConvLoc2_Enter;

                //if (ElevatorConveyor.Exit != null)
                //{
                //    ElevatorConveyor.Exit.OnEnter -= new ActionPoint.EnterEvent(Exit_Enter);
                //    ElevatorConveyor.Exit.Dispose();
                //}

                c.Location1.Dispose();
                c.Location2.Dispose();

                Remove(c);
                c.Dispose();
            }

            foreach (OutfeedDropStationConveyor f in DropStationPoints.Values)
            {
                Remove(f );
                Remove(f as Core.IEntity);
                f.Dispose();
            }

            foreach (MultiShuttleElevator elevator in elevators.Values)
            {
                Remove(elevator);
                elevator.Dispose();
            }

            foreach (InfeedPickStationConveyor c in PickStationConveyors)
            {
                //if (c.Location1 != null)//TODO dont comment the onenters---moved to rackconveyor??
                //    c.Location1.OnEnter -= rackConvLoc1_Enter;
                //if (c.Location2 != null)
                //    c.Location2.OnEnter -= rackConvLoc2_Enter;
                //if (ElevatorConveyor.Exit != null)
                //    ElevatorConveyor.Exit.OnEnter -= new ActionPoint.EnterEvent(infeedExit_Enter);
                //if (ElevatorConveyor.Entry != null)
                //    ElevatorConveyor.Entry.OnEnter -= new ActionPoint.EnterEvent(infeed_Entry_Enter);
                if (c.Location1 != null)
                    c.Location1.Dispose();
                if (c.Location2 != null)
                    c.Location2.Dispose();
                //if (ElevatorConveyor.Exit != null)
                //    ElevatorConveyor.Exit.Dispose();
                //if (ElevatorConveyor.Entry != null)
                //    ElevatorConveyor.Entry.Dispose();

                c.Location1 = null;
                c.Location2 = null;
                //ElevatorConveyor.Exit = null;

                ////Remove(ElevatorConveyor as RigidPart);
                Remove(c as Core.IEntity);
                c.Dispose();

                if (c.infeedFix != null)
                {
                    c.infeedFix.OnSnapped -= infeedFix_Snapped;
                    c.infeedFix.OnUnSnapped -= infeedFix_UnSnapped;
                    Remove(c.infeedFix);
                    c.infeedFix.Dispose();
                    c.infeedFix = null;
                }
            }

            DropStationPoints.Clear();
            conveyorlocations.Clear();
            RackConveyors.Clear();
            RackConveyorsList.Clear();
            elevators.Clear();
            PickStationConveyors.Clear();
            PickStationNameToActionPoint.Clear();

            // Create multishuttle

            CreateShuttles();

            foreach (Shuttle shuttle in shuttlecars.Values)
            {
                shuttle.Length = MultiShuttleinfo.raillength;
                shuttle.Car.Length = MultiShuttleinfo.carlength;
                shuttle.Car.Width = MultiShuttleinfo.carwidth;
            }

            if (MultiShuttleinfo.ElevatorFL)
            {
                BuildElevator(FrontLeftElevatorGroupName + "L", FLElevatorType, 0);
            }

            if (MultiShuttleinfo.ElevatorFR)
            {
                BuildElevator(FrontRightElevatorGroupName + "R", FRElevatorType, 0);
            }

            if (!MultiShuttleDriveThrough)
                bayLength = (Raillength - RackConveyorLength) / RackBays;
            else
                bayLength = (Raillength - RackConveyorLength * 2 - ElevatorConveyorLength) / RackBays;

            if (bayLength <= 0)
                Core.Environment.Log.Write(Name + ": Configuration error! Raillength is too short (baylength is calculated to be < 0)", Color.Red);

            if (control != null)
                control.Build();

            Core.Environment.SolutionExplorer.Refresh();
        }

        private void CreateShuttles()
        {
            for (int i = 1; i <= MultiShuttleinfo.ShuttleNumber; i++)
            {
                Shuttle shuttle = new Shuttle(MultiShuttleinfo, i, this);
                shuttle.Name = "Shuttle " + i.ToString().PadLeft(2, '0');
                shuttlecars.Add(i, shuttle);
                AddPart(shuttle, new Vector3(0, i * MultiShuttleinfo.DistanceLevels + MultiShuttleinfo.RackHeightOffset, 0));
            }
        }

        private void BuildElevator(string Name, MultiShuttleDirections elevatorType, float xoffset)
        {
            float zCoord = -RackConveyorWidth / 2 - MultiShuttleinfo.carwidth / 2;
            string mSside = Name.Substring(1, 1);

            if (mSside == "R") { zCoord *= -1; }                

            MultiShuttleElevatorInfo elevatorinfo = new MultiShuttleElevatorInfo();
            elevatorinfo.Multishuttle             = this;
            elevatorinfo.multishuttleinfo         = MultiShuttleinfo;
            elevatorinfo.ElevatorName             = Name;
            elevatorinfo.ElevatorType             = elevatorType;
            elevatorinfo.Multishuttle             = this;
            MultiShuttleElevator elevator         = new MultiShuttleElevator(elevatorinfo);
            elevators.Add(elevator.ElevatorName, elevator);            
            elevator.ElevatorHeight     = MultiShuttleinfo.ShuttleNumber * MultiShuttleinfo.DistanceLevels + MultiShuttleinfo.RackHeightOffset + 0.5f;
            Add(elevator);

            if (MultiShuttleDriveThrough)
            {
                elevator.LocalPosition = new Vector3(DriveThroughElevatorOffset + xoffset, elevator.ElevatorHeight/2, zCoord);
            }
            else
            {
                elevator.LocalPosition = new Vector3(MultiShuttleinfo.raillength/2 + elevator.ElevatorConveyor.ConveyorLength/2 + xoffset, elevator.ElevatorHeight/2, zCoord);
            }

            if (elevatorType == MultiShuttleDirections.Outfeed)
            {
                elevator.LocalYaw = (float)Math.PI*2;
            }
            else
            {
                elevator.LocalYaw = (float)Math.PI;
            }

            //Create Rack Conveyors
            for (int i = 1; i <= MultiShuttleinfo.ShuttleNumber; i++)
            {
                RackConveyorInfo rackConvInfo = new RackConveyorInfo();
                rackConvInfo.thickness        = 0.05f;
                rackConvInfo.length           = RackConveyorLength;
                rackConvInfo.width            = RackConveyorWidth;
                rackConvInfo.speed            = ConveyorSpeed;
                RackConveyor rackConveyor     = new RackConveyor(rackConvInfo);

                if (!MixedInfeedOutfeed) //In this case elevator type (infeed or outfeed) will always be the same as rack conveyor type (infeed or outfeed)
                {
                    rackConveyor.RackConveyorType = elevatorType;
                }

                AddAssembly(rackConveyor);
                rackConveyor.ConfigureRackConveyor(elevator, /*rackConveyor,*/ Name, /*elevatorType,*/ mSside, zCoord, i, xoffset);
            }

            if (elevatorType == MultiShuttleDirections.Infeed)
            {
                CreatePickstationConveyors(elevator, mSside, zCoord, xoffset);

                if (MixedInfeedOutfeed) { CreateDropstationConveyors(elevator, mSside, zCoord, xoffset); }                   

                if (MultiShuttleDriveThrough)
                {
                    foreach (LevelHeight level in MultiShuttleinfo.LevelHeightDropstations)
                    {
                        if (level.Side == mSside || string.IsNullOrWhiteSpace(level.Side))
                        {
                            DropStationPoint outfeedpoint = new DropStationPoint(FixPoint.Types.End, this, elevator);
                            outfeedpoint.SavedLevel = level;
                            outfeedpoint.Level = level.Level;
                            outfeedpoint.ListSolutionExplorer = true;
                            string groupside = FrontLeftInfeedRackGroupName + mSside;
                            if (mSside == "R")
                                groupside = FrontRightInfeedRackGroupName + mSside;
                            string outfeedpointname = groupside + outfeedpoint.Level;
                            outfeedpoint.Name = "Drop station " + outfeedpointname;
                            outfeedpoint.DropPositionGroupSide = groupside;
                            Add(outfeedpoint as RigidPart);
                          ////  DropStationPoints.Add(outfeedpointname, outfeedpoint);
                            outfeedpoint.LocalPosition = new Vector3(-elevator.ElevatorConveyor.ConveyorLength / 2 + DriveThroughElevatorOffset + xoffset, level.Height, zCoord);
                            outfeedpoint.LocalYaw = 0;
                        }
                    }
                }
            }
            else
            {
                CreateDropstationConveyors(elevator, mSside, zCoord, xoffset);

                if (MixedInfeedOutfeed) { CreatePickstationConveyors(elevator, mSside, zCoord, xoffset);}
                    
                if (MultiShuttleDriveThrough)
                {
                    foreach (LevelHeight level in MultiShuttleinfo.LevelHeightPickstations)
                    {
                        if (level.Side == mSside || string.IsNullOrWhiteSpace(level.Side))
                        {
                            //Front right in

                            string groupside = FrontLeftOutfeedRackGroupName + mSside;
                            if (mSside == "R")
                                groupside = FrontRightOutfeedRackGroupName + mSside;

                            InfeedPickStationConveyor infeedPickStationConveyor = CreateInfeedPickStationConv(elevator,
                                                                                                              level,
                                                                                                              groupside,
                                                                                                              "P" + AisleNo + groupside + POS1 + level.Level,
                                                                                                              "P" + AisleNo + groupside + POS2 + level.Level);
                            Add(infeedPickStationConveyor);

                            PickStationConveyors.Add(infeedPickStationConveyor);
                            infeedPickStationConveyor.LocalPosition = new Vector3(-elevator.ElevatorConveyor.ConveyorLength / 2 - infeedPickStationConveyor.Length / 2 + DriveThroughElevatorOffset, level.Height, zCoord);
                            infeedPickStationConveyor.LocalYaw = (float)Math.PI;

                            infeedPickStationConveyor.infeedFix = new FixPoint(FixPoint.Types.Start, this);
                            infeedPickStationConveyor.infeedFix.Route = infeedPickStationConveyor.Route;
                            AddPart(infeedPickStationConveyor.infeedFix);
                            infeedPickStationConveyor.infeedFix.LocalPosition = new Vector3(-elevator.ElevatorConveyor.ConveyorLength / 2 - infeedPickStationConveyor.Length + DriveThroughElevatorOffset, level.Height, zCoord);
                            infeedPickStationConveyor.infeedFix.LocalYaw = (float)Math.PI;
                            infeedPickStationConveyor.infeedFix.OnSnapped += new FixPoint.SnappedEvent(infeedFix_Snapped);
                            infeedPickStationConveyor.infeedFix.OnUnSnapped += new FixPoint.UnSnappedEvent(infeedFix_UnSnapped);
                        }
                    }
                }
            }

            return;
        }

        private InfeedPickStationConveyor CreateInfeedPickStationConv(MultiShuttleElevator elevator, LevelHeight levelHeight, string groupName, string location1Name, string location2Name)
        {
            InfeedPickStationConveyor3Info info2 = new InfeedPickStationConveyor3Info();
            info2.thickness                      = 0.05f;
            info2.color                          = Core.Environment.Scene.DefaultColor;
            info2.Width                          = 0.5f;
            info2.CreateLineFull                 = true;
            info2.elevator                       = elevator;
            info2.levelHeight                    = levelHeight;
            info2.multiShuttle                   = this;
            info2.location1Name                  = location1Name;
            info2.location2Name                  = location2Name;
            info2.createTempAP                   = true;

            //info2.name = infeedConveyorName;
            InfeedPickStationConveyor infeedPickStationConveyor = new InfeedPickStationConveyor(info2);

            infeedPickStationConveyor.Name = "Pick station " + groupName + infeedPickStationConveyor.Level;
 
            infeedPickStationConveyor.Positions            = 2;
            infeedPickStationConveyor.OutfeedSection       = Case.Components.OutfeedLength._125mm;
            infeedPickStationConveyor.InfeedSection        = 0;
            infeedPickStationConveyor.Width                = 0.750f;
            infeedPickStationConveyor.AccPitch             = Case.Components.AccumulationPitch._500mm;
            infeedPickStationConveyor.Route.Motor.Speed    = ConveyorSpeed;
            infeedPickStationConveyor.ListSolutionExplorer = true;
            infeedPickStationConveyor.PlaceTransferPoints();

            int res;            
            infeedPickStationConveyor.sensors.Where(i => int.TryParse(i.sensor.Name,out res) == true);

            return infeedPickStationConveyor;
        }

        private void CreateDropstationConveyors(MultiShuttleElevator elevator, string MSside, float z_coord, float xoffset)
        {
            foreach (LevelHeight level in MultiShuttleinfo.LevelHeightDropstations)
            {
                if (level.Side == MSside || string.IsNullOrWhiteSpace(level.Side))
                {
                    OutfeedDropStationConveyorInfo dropStationConvInfo = new OutfeedDropStationConveyorInfo();
                    dropStationConvInfo.type                           = FixPoint.Types.End;
                    dropStationConvInfo.parent                         = this;
                    dropStationConvInfo.elevator                       = elevator;
                    dropStationConvInfo.thickness                      = 0.05f;
                    dropStationConvInfo.color                          = Core.Environment.Scene.DefaultColor;
                    dropStationConvInfo.Width                          = 0.5f;
                    //dropStationConvInfo.CreateLineFull                 = true;
                    OutfeedDropStationConveyor dropStationConv         = new OutfeedDropStationConveyor(dropStationConvInfo);
                    dropStationConv.SavedLevel                         = level;
                    dropStationConv.Level                              = level.Level;
                    dropStationConv.ListSolutionExplorer               = true;
                    dropStationConv.Positions                          = 2;
                    dropStationConv.OutfeedSection                     = Case.Components.OutfeedLength._0mm;
                    dropStationConv.InfeedSection                      = 0.22f;
                    dropStationConv.Width                              = 0.750f;
                    dropStationConv.AccPitch                           = Case.Components.AccumulationPitch._500mm;
                    dropStationConv.PlaceTransferPoints();
                    dropStationConv.TransportSection.Route.Motor.Speed = ConveyorSpeed;
                    dropStationConv.ListSolutionExplorer = true;

                    AddAssembly(dropStationConv);

                    string groupside                  = FrontLeftInfeedRackGroupName + MSside;

                    if (MSside == "R")
                        groupside = FrontRightInfeedRackGroupName + MSside;

                    string outfeedpointname = groupside + dropStationConv.Level;
                   // Add(outfeedpoint as RigidPart);

                    dropStationConv.Name = "Drop station " + outfeedpointname;
                    dropStationConv.DropPositionGroupSide = groupside;

                    DropStationPoints.Add(outfeedpointname, dropStationConv);

                    if (MultiShuttleDriveThrough)
                        dropStationConv.LocalPosition = new Vector3(elevator.ElevatorConveyor.ConveyorLength / 2 + DriveThroughElevatorOffset + xoffset, level.Height, z_coord);
                    else
                        dropStationConv.LocalPosition = new Vector3(MultiShuttleinfo.raillength / 2 + elevator.ElevatorConveyor.ConveyorLength + (elevator.ElevatorConveyor.ConveyorLength / 2) + xoffset, level.Height, z_coord);

                    dropStationConv.LocalYaw = (float)Math.PI;

                    //TODO sort drivethrough out
                    //if (MultiShuttleDriveThrough)
                    //    outfeedpoint.LocalPosition = new Vector3(elevator.ConveyorLength / 2 + DriveThroughElevatorOffset + xoffset, level.Height, z_coord);
                    //else
                    //    outfeedpoint.LocalPosition = new Vector3(MultiShuttleinfo.raillength / 2 + elevator.ConveyorLength + xoffset, level.Height, z_coord);

                    //outfeedpoint.LocalYaw = (float)Math.PI;
                }
            }
        }

        private void CreatePickstationConveyors(MultiShuttleElevator elevator, string MSside, float z_coord, float xoffset)
        {
            foreach (LevelHeight level in MultiShuttleinfo.LevelHeightPickstations)
            {
                if (level.Side == MSside || string.IsNullOrWhiteSpace(level.Side))
                {

                    string groupside = FrontLeftOutfeedRackGroupName + MSside;
                    if (MSside == "R")
                        groupside = FrontRightOutfeedRackGroupName + MSside;

                    InfeedPickStationConveyor infeedPickStationConveyor = CreateInfeedPickStationConv(elevator, 
                                                                                                      level, 
                                                                                                      groupside,
                                                                                                      "P" + AisleNo + groupside + POS1 + level.Level,
                                                                                                      "P" + AisleNo + groupside + POS2 + level.Level);
                    Add(infeedPickStationConveyor);

                    PickStationConveyors.Add(infeedPickStationConveyor);

                    if (MultiShuttleDriveThrough)
                        infeedPickStationConveyor.LocalPosition = new Vector3(elevator.ElevatorConveyor.ConveyorLength / 2 + infeedPickStationConveyor.Length / 2 + DriveThroughElevatorOffset + xoffset, level.Height, z_coord);
                    else
                        infeedPickStationConveyor.LocalPosition = new Vector3(MultiShuttleinfo.raillength / 2 + elevator.ElevatorConveyor.ConveyorLength + infeedPickStationConveyor.Length / 2 + xoffset, level.Height, z_coord);

                    infeedPickStationConveyor.infeedFix = new FixPoint(FixPoint.Types.Start, this);
                    infeedPickStationConveyor.infeedFix.Route = infeedPickStationConveyor.Route;
                    AddPart(infeedPickStationConveyor.infeedFix);

                    if (MultiShuttleDriveThrough)
                        infeedPickStationConveyor.infeedFix.LocalPosition = new Vector3(elevator.ElevatorConveyor.ConveyorLength / 2 + infeedPickStationConveyor.Length + DriveThroughElevatorOffset + xoffset, level.Height, z_coord);
                    else
                        infeedPickStationConveyor.infeedFix.LocalPosition = new Vector3(MultiShuttleinfo.raillength / 2 + elevator.ElevatorConveyor.ConveyorLength + infeedPickStationConveyor.Length + xoffset, level.Height, z_coord);

                    infeedPickStationConveyor.infeedFix.OnSnapped += new FixPoint.SnappedEvent(infeedFix_Snapped);
                    infeedPickStationConveyor.infeedFix.OnUnSnapped += new FixPoint.UnSnappedEvent(infeedFix_UnSnapped);

                }
            }
        }

        private void SetController()
        {
            if (control != null)
                control.RemoveMultiShuttle(this);

            control = AllControllers.Find(c => c.Name == ControllerName);

            if (!string.IsNullOrWhiteSpace(ControllerName) && control == null)
                ControllerName = ""; //Controller not found reset name

            if (control != null)
                control.AddMultiShuttle(this);
        }


        void infeedFix_UnSnapped(FixPoint fixpoint)
        {
            fixpoint.Route.NextRoute = null;
        }

        void infeedFix_Snapped(FixPoint fixpoint, FixPoint.SnapEventArgs e)
        {
            fixpoint.Route.NextRoute = fixpoint.Attached.Route;
        }

        #endregion

        #region User Interface Properties

        public void DynamicPropertyMiddleElevator(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = MultiShuttleDriveThrough;
        }
        public void DynamicPropertyNotMiddleElevator(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = !MultiShuttleDriveThrough;
        }

        public void DynamicPropertyMixedInfeedOutfeed(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = MixedInfeedOutfeed;
        }
        

        [CategoryAttribute("PLC configuration")]
        [DisplayName("MSC")]
        [DescriptionAttribute("Select the MSC that this multishuttle should use")]
        [TypeConverter(typeof(MSCConverter))]
        public virtual string ControllerName
        {
            get { return MultiShuttleinfo.ControllerName; }
            set
            {
                MultiShuttleinfo.ControllerName = value;
                SetController();
            }
        }

        [CategoryAttribute("PLC configuration")]
        [PropertyOrder(1)]
        [DescriptionAttribute("Aisle No used in telegrams (not necessarily the same as the crane ID). Please make sure that this is the exact same characters that are used in the received telegrams and that the number of characters doesn't exceed the length of the this field in the MVT.")]
        [DisplayName("Aisle No.")]
        public string AisleNo
        {
            get { return MultiShuttleinfo.AisleNo; }
            set { MultiShuttleinfo.AisleNo = value; }
        }

        [CategoryAttribute("Configuration")]
        [PropertyOrder(9)]
        [DescriptionAttribute("The distane to depth in the rack (center to center)")]
        [DisplayName("Distance to rack depth (m.)")]
        public float DepthDist
        {
            get { return MultiShuttleinfo.DepthDist; }
            set 
            {
                if (value > 0)
                    MultiShuttleinfo.DepthDist = value;
            }
        }

        [CategoryAttribute("Configuration")]
        [PropertyOrder(9)]
        [DescriptionAttribute("Shuttle positioning time in seconds. When making a drop or pick this time as added.")]
        [DisplayName("Shuttle positioning time (s.)")]
        public float ShuttlePositioningTime
        {
            get { return MultiShuttleinfo.ShuttlePositioningTime; }
            set
            {
                if (value != MultiShuttleinfo.ShuttlePositioningTime && value > 0)
                {
                    MultiShuttleinfo.ShuttlePositioningTime = value;
                }
            }
        }

        [PropertyOrder(1)]
        [CategoryAttribute("Configuration")]
        [DescriptionAttribute("Rail length in meter.")]
        [DisplayName("Rail length (m.)")]
        public float Raillength
        {
            get { return MultiShuttleinfo.raillength; }
            set
            {
                if (value > 2 && value <= 2000 && MultiShuttleinfo.raillength != value)
                {
                    MultiShuttleinfo.raillength = value;
                    Rebuild();     
                }
            }
        }

        [PropertyOrder(2)]
        [CategoryAttribute("Configuration")]
        [DescriptionAttribute("Build a Multi-Shuttle Drive Through")]
        [DisplayName("Multi-Shuttle Drive Through")]
        public bool MultiShuttleDriveThrough
        {
            get { return MultiShuttleinfo.MultiShuttleDriveThrough; }
            set
            {
                if (MultiShuttleinfo.MultiShuttleDriveThrough != value)
                {
                    if (value)
                    {
                        //MS with elevator in middle uses the two "Front" elevators. Back elevators are removed.
                        MultiShuttleinfo.ElevatorFR = true;
                        MultiShuttleinfo.ElevatorFL = true;
                        MultiShuttleinfo.ElevatorBR = false;
                        MultiShuttleinfo.ElevatorBL = false;

                        MultiShuttleinfo.FrontLeftElevatorGroupName = "A";
                        MultiShuttleinfo.FrontRightElevatorGroupName = "A";

                        MultiShuttleinfo.FrontLeftInfeedRackGroupName = "B";
                        MultiShuttleinfo.FrontRightInfeedRackGroupName = "A";

                        MultiShuttleinfo.FrontLeftOutfeedRackGroupName = "A";
                        MultiShuttleinfo.FrontRightOutfeedRackGroupName = "B";

                        //Curent solution will remove multiple pick stations and drop station and create one of each 1 meter beneath the MS
                        MultiShuttleinfo.LevelHeightDropstations.Clear();
                        MultiShuttleinfo.LevelHeightPickstations.Clear();
                        MultiShuttleinfo.LevelHeightDropstations.Add(new LevelHeight() { Level = "01", Height = 0.05f });
                        MultiShuttleinfo.LevelHeightPickstations.Add(new LevelHeight() { Level = "01", Height = 0.05f });
                    }
                    if (!value)
                    {
                        MultiShuttleinfo.FrontLeftElevatorGroupName = "F";
                        MultiShuttleinfo.FrontRightElevatorGroupName = "F";
                        MultiShuttleinfo.FrontLeftInfeedRackGroupName = "F";
                        MultiShuttleinfo.FrontRightInfeedRackGroupName = "F";
                        MultiShuttleinfo.FrontLeftOutfeedRackGroupName = "F";
                        MultiShuttleinfo.FrontRightOutfeedRackGroupName = "F";
                    }

                    MultiShuttleinfo.MultiShuttleDriveThrough = value;
                    Rebuild();
                    Core.Environment.Properties.Refresh();
                }
            }
        }

        [PropertyOrder(3)]
        [CategoryAttribute("Configuration")]
        [DescriptionAttribute("Front left elevator")]
        [DisplayName("FL elevator")]
        [PropertyAttributesProvider("DynamicPropertyNotMiddleElevator")]
        public bool FrontLeftElevator
        {
            get { return MultiShuttleinfo.ElevatorFL; }
            set
            {
                if (MultiShuttleinfo.ElevatorFL != value)
                {
                    MultiShuttleinfo.ElevatorFL = value;
                    Rebuild();
                }
            }
        }

        [PropertyOrder(4)]
        [CategoryAttribute("Configuration")]
        [DescriptionAttribute("Front right elevator")]
        [DisplayName("FR elevator")]
        [PropertyAttributesProvider("DynamicPropertyNotMiddleElevator")]
        public bool FrontRightElevator
        {
            get { return MultiShuttleinfo.ElevatorFR; }
            set
            {
                if (MultiShuttleinfo.ElevatorFR != value)
                {
                    MultiShuttleinfo.ElevatorFR = value;
                    Rebuild();
                }
            }
        }

        [CategoryAttribute("Configuration")]
        [DescriptionAttribute("right elevator group name")]
        [DisplayName("Right elevator group name")]
        public string FrontRightElevatorGroupName
        {
            get { return MultiShuttleinfo.FrontRightElevatorGroupName; }
            set
            {
                if (value.Length == 1 && value != MultiShuttleinfo.FrontRightElevatorGroupName)
                {
                    MultiShuttleinfo.FrontRightElevatorGroupName = value;
                    Rebuild();
                }
            }
        }

        [CategoryAttribute("Configuration")]
        [DescriptionAttribute("left elevator group name")]
        [DisplayName("Left elevator group name")]
        public string FrontLeftElevatorGroupName
        {
            get { return MultiShuttleinfo.FrontLeftElevatorGroupName; }
            set
            {
                if (value.Length == 1 && value != MultiShuttleinfo.FrontLeftElevatorGroupName)
                {
                    MultiShuttleinfo.FrontLeftElevatorGroupName = value;
                    Rebuild();
                }
            }
        }

        [CategoryAttribute("Configuration")]
        [DescriptionAttribute("left infeed rack conveyor group name")]
        [DisplayName("Left infeed rack group name")]
        public string FrontLeftInfeedRackGroupName
        {
            get { return MultiShuttleinfo.FrontLeftInfeedRackGroupName; }
            set
            {
                if (value == MultiShuttleinfo.FrontLeftOutfeedRackGroupName)
                    Core.Environment.Log.Write("Infeed and outfeed group names must be different!");

                if (value.Length == 1 && value != MultiShuttleinfo.FrontLeftInfeedRackGroupName && value != MultiShuttleinfo.FrontLeftOutfeedRackGroupName)
                {
                    MultiShuttleinfo.FrontLeftInfeedRackGroupName = value;
                    Rebuild();
                }
            }
        }

        [CategoryAttribute("Configuration")]
        [DescriptionAttribute("right infeed rack conveyor group name")]
        [DisplayName("Right infeed rack group name")]
        public string FrontRightInfeedRackGroupName
        {
            get { return MultiShuttleinfo.FrontRightInfeedRackGroupName; }
            set
            {
                if (value == MultiShuttleinfo.FrontRightOutfeedRackGroupName)
                    Core.Environment.Log.Write("Infeed and outfeed group names must be different!");

                if (value.Length == 1 && value != MultiShuttleinfo.FrontRightInfeedRackGroupName && value != MultiShuttleinfo.FrontRightOutfeedRackGroupName)
                {
                    MultiShuttleinfo.FrontRightInfeedRackGroupName = value;
                    Rebuild();
                }
            }
        }

        [CategoryAttribute("Configuration")]
        [DescriptionAttribute("left outfeed rack conveyor group name")]
        [DisplayName("Left outfeed rack group name")]
        public string FrontLeftOutfeedRackGroupName
        {
            get { return MultiShuttleinfo.FrontLeftOutfeedRackGroupName; }
            set
            {
                if (value == MultiShuttleinfo.FrontLeftInfeedRackGroupName)
                    Core.Environment.Log.Write("Infeed and outfeed group names must be different!");

                if (value.Length == 1 && value != MultiShuttleinfo.FrontLeftOutfeedRackGroupName && value != MultiShuttleinfo.FrontLeftInfeedRackGroupName)
                {
                    MultiShuttleinfo.FrontLeftOutfeedRackGroupName = value;
                    Rebuild();
                }
            }
        }

        [CategoryAttribute("Configuration")]
        [DescriptionAttribute("right outfeed rack conveyor group name")]
        [DisplayName("Right outfeed rack group name")]
        public string FrontRightOutfeedRackGroupName
        {
            get { return MultiShuttleinfo.FrontRightOutfeedRackGroupName; }
            set
            {
                if (value == MultiShuttleinfo.FrontRightInfeedRackGroupName)
                    Core.Environment.Log.Write("Infeed and outfeed group names must be different!");

                if (value.Length == 1 && value != MultiShuttleinfo.FrontRightOutfeedRackGroupName && value != MultiShuttleinfo.FrontRightInfeedRackGroupName)
                {
                    MultiShuttleinfo.FrontRightOutfeedRackGroupName = value;
                    Rebuild();
                }
            }
        }

        [CategoryAttribute("Configuration")]
        [DescriptionAttribute("Front right elevator type")]
        [DisplayName("FR elevator type")]
        [PropertyAttributesProvider("DynamicPropertyNotMiddleElevator")]
        public MultiShuttleDirections FRElevatorType
        {
            get { return MultiShuttleinfo.ElevatorFRtype; }
            set
            {
                if (MultiShuttleinfo.ElevatorFRtype != value)
                {
                    MultiShuttleinfo.ElevatorFRtype = value;
                    Rebuild();
                }
            }
        }

        [CategoryAttribute("Configuration")]
        [DescriptionAttribute("Front left elevator type")]
        [DisplayName("FL elevator type")]
        [PropertyAttributesProvider("DynamicPropertyNotMiddleElevator")]
        public MultiShuttleDirections FLElevatorType
        {
            get { return MultiShuttleinfo.ElevatorFLtype; }
            set
            {
                if (MultiShuttleinfo.ElevatorFLtype != value)
                {
                    MultiShuttleinfo.ElevatorFLtype = value;
                    Rebuild();
                }
            }
        }

        [CategoryAttribute("Configuration")]
        [DescriptionAttribute("right elevator type")]
        [DisplayName("Right elevator type")]
        [PropertyAttributesProvider("DynamicPropertyMiddleElevator")]
        public MultiShuttleDirections RightElevatorType
        {
            get { return MultiShuttleinfo.ElevatorFRtype; }
            set
            {
                if (MultiShuttleinfo.ElevatorFRtype != value)
                {
                    MultiShuttleinfo.ElevatorFRtype = value;
                    Rebuild();
                }
            }
        }

        [CategoryAttribute("Configuration")]
        [DescriptionAttribute("left elevator type")]
        [DisplayName("Left elevator type")]
        [PropertyAttributesProvider("DynamicPropertyMiddleElevator")]
        public MultiShuttleDirections LeftElevatorType
        {
            get { return MultiShuttleinfo.ElevatorFLtype; }
            set
            {
                if (MultiShuttleinfo.ElevatorFLtype != value)
                {
                    MultiShuttleinfo.ElevatorFLtype = value;
                    Rebuild();
                }
            }
        }

        [CategoryAttribute("Configuration")]
        [DescriptionAttribute("Number of levels.")]
        [DisplayName("Number of levels.")]
        public int Levels
        {
            get { return MultiShuttleinfo.ShuttleNumber; }
            set
            {
                if (value > 0 && value <= 100 && MultiShuttleinfo.ShuttleNumber != value)
                {
                    MultiShuttleinfo.ShuttleNumber = value;
                    Rebuild();
                }
            }
        }

        [CategoryAttribute("Configuration")]
        [DescriptionAttribute("Mixed infeed/outfeed on both sides")]
        [DisplayName("Mixed infeed/outfeed")]
        [PropertyAttributesProvider("DynamicPropertyNotMiddleElevator")]
        public bool MixedInfeedOutfeed
        {
            get { return MultiShuttleinfo.MixedInfeedOutfeed; }
            set
            {
                if (MultiShuttleinfo.MixedInfeedOutfeed != value)
                {
                    MultiShuttleinfo.MixedInfeedOutfeed = value;
                    if (value)
                        MultiShuttleinfo.MultiShuttleDriveThrough = false;

                    Core.Environment.Properties.Refresh();
                    Rebuild();
                }
            }
        }

        [CategoryAttribute("Configuration")]
        [DescriptionAttribute("If true then left even levels are infeed and odd levels are outfeed. Right even levels are outfeed and odd levels infeed.")]
        [DisplayName("Left Even Infeed")]
        [PropertyAttributesProvider("DynamicPropertyMixedInfeedOutfeed")]
        public bool LeftEvenLevelInfeed
        {
            get { return MultiShuttleinfo.LeftEvenLevelInfeed; }
            set
            {
                if (MultiShuttleinfo.LeftEvenLevelInfeed != value)
                {
                    MultiShuttleinfo.LeftEvenLevelInfeed = value;
                    Core.Environment.Properties.Refresh();
                    Rebuild();
                }
            }
        }

        [CategoryAttribute("Configuration")]
        [DescriptionAttribute("Distance between levels. (meter)")]
        [DisplayName("Distance between levels.")]
        public float DistanceLevels
        {
            get { return MultiShuttleinfo.DistanceLevels; }
            set
            {
                if (value > 0 && value <= 100 && MultiShuttleinfo.DistanceLevels != value)
                {
                    MultiShuttleinfo.DistanceLevels = value;
                    Rebuild();
                }
            }
        }

        [CategoryAttribute("Configuration")]
        [DescriptionAttribute("Rack Height Offset in meters. ")]
        [DisplayName("Rack Height Offset (m).")]
        public float RackHeightOffset
        {
            get { return MultiShuttleinfo.RackHeightOffset; }
            set
            {
                if (value >= 0 && value <= 100 && MultiShuttleinfo.RackHeightOffset != value)
                {
                    MultiShuttleinfo.RackHeightOffset = value;
                    Rebuild();
                }
            }
        }

        [CategoryAttribute("Configuration")]
        [DescriptionAttribute("Drive Through Elevator Offset in meters. ")]
        [DisplayName("Drive Through Elevator Offset (m).")]
        [PropertyAttributesProvider("DynamicPropertyMiddleElevator")]
        public float DriveThroughElevatorOffset
        {
            get { return MultiShuttleinfo.DriveThroughElevatorOffset; }
            set
            {
                if (Math.Abs(value) <= Raillength / 2 - RackConveyorLength - ElevatorConveyorLength / 2 && MultiShuttleinfo.DriveThroughElevatorOffset != value)
                {
                    MultiShuttleinfo.DriveThroughElevatorOffset = value;
                    Rebuild();
                }
            }
        } 

        [CategoryAttribute("Configuration")]
        [DescriptionAttribute("Elevator conveyor length (meter)")]
        [DisplayName("Elevator conveyor length")]
        public float ElevatorConveyorLength
        {
            get { return MultiShuttleinfo.ElevatorConveyorLength; }
            set
            {
                if (value > 0 && value <= 100 && MultiShuttleinfo.ElevatorConveyorLength != value)
                {
                    MultiShuttleinfo.ElevatorConveyorLength = value;
                    Rebuild();
                }
            }
        }

        [CategoryAttribute("Configuration")]
        [DescriptionAttribute("Pick station conveyor length (meter)")]
        [DisplayName("Pick station length")]
        public float PickStationConveyorLength
        {
            get { return MultiShuttleinfo.PickStationConveyorLength; }
            set
            {
                if (value > 0 && value <= 100 && MultiShuttleinfo.PickStationConveyorLength != value)
                {
                    MultiShuttleinfo.PickStationConveyorLength = value;
                    Rebuild();
                }
            }
        }

        [CategoryAttribute("Configuration")]
        [DescriptionAttribute("Elevator conveyor width (meter)")]
        [DisplayName("Elevator conveyor width")]
        public float ElevatorConveyorWidth
        {
            get { return MultiShuttleinfo.ElevatorConveyorWidth; }
            set
            {
                if (value > 0 && value <= 100 && MultiShuttleinfo.ElevatorConveyorWidth != value)
                {
                    MultiShuttleinfo.ElevatorConveyorWidth = value;
                    Rebuild();
                }
            }
        }

        [CategoryAttribute("Configuration")]
        [DescriptionAttribute("Rack conveyor length (meter)")]
        [DisplayName("Rack conveyor length")]
        public float RackConveyorLength
        {
            get { return MultiShuttleinfo.RackConveyorLength; }
            set
            {
                if (value > 0 && value <= 100 && MultiShuttleinfo.RackConveyorLength != value)
                {
                    MultiShuttleinfo.RackConveyorLength = value;
                    Rebuild();
                }
            }
        }

        [CategoryAttribute("Configuration")]
        [DescriptionAttribute("Rack conveyor width (meter)")]
        [DisplayName("Rack conveyor width")]
        public float RackConveyorWidth
        {
            get { return MultiShuttleinfo.RackConveyorWidth; }
            set
            {
                if (value > 0 && value <= 100 && MultiShuttleinfo.RackConveyorWidth != value)
                {
                    MultiShuttleinfo.RackConveyorWidth = value;
                    Rebuild();
                }
            }
        }

        [CategoryAttribute("Configuration")]
        [PropertyOrder(5)]
        [DescriptionAttribute("The number of bays in rack (presumably evenly distributed over the length of the rack)")]
        [DisplayName("Bays in rack")]
        public int RackBays
        {
            get { return MultiShuttleinfo.RackBays; }
            set
            {
                if (MultiShuttleinfo.RackBays != value && value > 1)
                {
                    MultiShuttleinfo.RackBays = value;
                    if (!MultiShuttleDriveThrough)
                        bayLength = (Raillength - RackConveyorLength) / RackBays;
                    else
                        bayLength = (Raillength - RackConveyorLength * 2 - ElevatorConveyorLength) / RackBays;
                }
            }
        }

        [CategoryAttribute("Configuration")]
        [PropertyOrder(6)]
        [DescriptionAttribute("Conveyor Speed (m/s) for rack conveyor, elevator conveyor, pick station conveyor")]
        [DisplayName("Conveyor Speed (m/s)")]
        public float ConveyorSpeed
        {
            get { return MultiShuttleinfo.ConveyorSpeed; }
            set
            {
                if (value > 0)
                {
                    MultiShuttleinfo.ConveyorSpeed = value;

                    foreach (RackConveyor rack in RackConveyors.Values)
                        rack.ConvRoute.Motor.Speed = value;

                    foreach (MultiShuttleElevator elevator in elevators.Values)
                        elevator.ElevatorConveyorSpeed = value;

                    foreach (InfeedPickStationConveyor pickstation in PickStationConveyors)
                        pickstation.Route.Motor.Speed = value;

                }
            }
        }

        [CategoryAttribute("Configuration")]
        [PropertyOrder(7)]
        [DescriptionAttribute("Position naming convention for Drop station, Outfeed rack and Outfeed elevator. New naming convention 001 002, old naming 002 001.")]
        [DisplayName("Outfeed Naming Convention")]
        public OutFeedNamingConventions OutfeedNamingConvention
        {
            get { return MultiShuttleinfo.OutfeedNamingConvention; }
            set
            {
                if (value != MultiShuttleinfo.OutfeedNamingConvention)
                {
                    MultiShuttleinfo.OutfeedNamingConvention = value;
                    Rebuild();
                }
            }
        }

        [PropertyOrder(1)]
        [CategoryAttribute("Shuttle Car Speed")]
        [DescriptionAttribute("Elevator speed in m/s")]
        [DisplayName("Elevator speed (m/s)")]
        public float ElevatorSpeed
        {
            get { return MultiShuttleinfo.elevatorSpeed; }
            set
            {
                if (value > 0 && value <= 1000)
                {
                    MultiShuttleinfo.elevatorSpeed = value;

                    foreach (MultiShuttleElevator elevator in elevators.Values)
                        elevator.ElevatorSpeed = value;        
                }
            }
        }

        [PropertyOrder(1)]
        [CategoryAttribute("Shuttle Car Speed")]
        [DescriptionAttribute("Shuttle car speed in m/s")]
        [DisplayName("Shuttle car speed (m/s)")]
        public float ShuttleCarSpeed
        {
            get { return MultiShuttleinfo.shuttlecarSpeed; }
            set
            {
                if (value > 0 && value <= 1000)
                {
                    MultiShuttleinfo.shuttlecarSpeed = value;
                    foreach (Shuttle car in shuttlecars.Values)
                    {
                        if (car.Route.Motor.Speed > 0)
                            car.Route.Motor.Speed = MultiShuttleinfo.shuttlecarSpeed;
                        else
                            car.Route.Motor.Speed = -MultiShuttleinfo.shuttlecarSpeed;
                    }
                }
            }
        }

        [PropertyOrder(2)]
        [CategoryAttribute("Shuttle Car Speed")]
        [DescriptionAttribute("Shuttle Car Loading speed in m/s")]
        [DisplayName("Loading speed (m/s)")]
        public float LoadingSpeed
        {
            get { return MultiShuttleinfo.loadingSpeed; }
            set
            {
                if (value > 0)
                {
                    MultiShuttleinfo.loadingSpeed = value;
                }
            }
        }

        [PropertyOrder(3)]
        [CategoryAttribute("Shuttle Car Speed")]
        [DescriptionAttribute("Shuttle Car Unloading speed in m/s")]
        [DisplayName("Unloading speed (m/s)")]
        public float UnloadingSpeed
        {
            get { return MultiShuttleinfo.unloadingSpeed; }
            set
            {
                if (value > 0)
                {
                    MultiShuttleinfo.unloadingSpeed = value;
                }
            }
        }

        [CategoryAttribute("Shuttle car")]
        [DescriptionAttribute("Shuttle car Length in meter.")]
        [DisplayName("Shuttle car Length (m.)")]
        public float ShuttleCarLength
        {
            get { return MultiShuttleinfo.carlength; }
            set
            {
                if (value > 0 && value <= 100 && MultiShuttleinfo.carlength != value)
                {
                    MultiShuttleinfo.carlength = value;
                    Rebuild();
                }
            }
        }

        [CategoryAttribute("Shuttle car")]
        [DescriptionAttribute("Shuttle car width in meter.")]
        [DisplayName("Shuttle car width (m.)")]
        public float ShuttleCarWidth
        {
            get { return MultiShuttleinfo.carwidth; }
            set
            {
                if (value > 0 && value <= 100 && MultiShuttleinfo.carwidth != value)
                {
                    MultiShuttleinfo.carwidth = value;
                    Rebuild();
                }
            }
        }

        [CategoryAttribute("Totes")]
        [DescriptionAttribute("Tote width in meter.")]
        [DisplayName("Tote width (m.)")]
        public float ToteWidth
        {
            get { return MultiShuttleinfo.ToteWidth; }
            set
            {
                if (value > 0 && value <= 100 && MultiShuttleinfo.ToteWidth != value)
                {
                    MultiShuttleinfo.ToteWidth = value;
                }
            }
        }

        [CategoryAttribute("Totes")]
        [DescriptionAttribute("Tote length in meter.")]
        [DisplayName("Tote length (m.)")]
        public float ToteLength
        {
            get { return MultiShuttleinfo.ToteLength; }
            set
            {
                if (value > 0 && value <= 100 && MultiShuttleinfo.ToteLength != value)
                {
                    MultiShuttleinfo.ToteLength = value;
                }
            }
        }

        [CategoryAttribute("Totes")]
        [DescriptionAttribute("Tote height in meter.")]
        [DisplayName("Tote height (m.)")]
        public float ToteHeight
        {
            get { return MultiShuttleinfo.ToteHeight; }
            set
            {
                if (value > 0 && value <= 100 && MultiShuttleinfo.ToteHeight != value)
                {
                    MultiShuttleinfo.ToteHeight = value;
                }
            }
        }

        [CategoryAttribute("Totes")]
        [DescriptionAttribute("Tote weight in kg.")]
        [DisplayName("Tote weight (kg.)")]
        public float ToteWeight 
        {
            get { return MultiShuttleinfo.ToteWeight; }
            set
            {
                if (value > 0 && value <= 100 && MultiShuttleinfo.ToteWeight != value)
                {
                    MultiShuttleinfo.ToteWeight = value;
                }
            }
        }

        [Category("Totes")]
        [DisplayName("Tote color")]
        [Description("Tote color")]
        public Color ToteColor
        {
            get
            {
                return Color.FromArgb(MultiShuttleinfo.ToteColorArgb);
            }
            set
            {
                MultiShuttleinfo.ToteColorArgb = value.ToArgb();
            }
        }

        [CategoryAttribute("Controls")]
        [DescriptionAttribute("Time in seconds pick station 002 waits for tote nr 2.")]
        [DisplayName("Timeout pick station 002")]
        public float PickStation2Timeout
        {
            get { return MultiShuttleinfo.PickStation2Timeout; }
            set
            {
                if (value > 0 && MultiShuttleinfo.PickStation2Timeout != value)
                {
                    MultiShuttleinfo.PickStation2Timeout = value;
                }
            }
        }
        #endregion

    }

    /// <summary>
    /// Used to show a list of avialable MSC  
    /// </summary>
    public class MSCConverter : StringConverter
    {
        public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
        {
            //true means show a combobox
            return true;
        }

        public override bool GetStandardValuesExclusive(ITypeDescriptorContext context)
        {
            //true will limit to list. false will show the list, but allow free-form entry
            return true;
        }

        public override
            System.ComponentModel.TypeConverter.StandardValuesCollection
            GetStandardValues(ITypeDescriptorContext context)
        {
            return new StandardValuesCollection((from msc in DematicMultiShuttle.AllControllers select msc.Name).ToList<string>());
        }

    }
}
