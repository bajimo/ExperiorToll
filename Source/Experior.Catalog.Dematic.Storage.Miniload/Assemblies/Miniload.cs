using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using Experior.Core.Loads;
using System;
using Microsoft.DirectX;
using Experior.Core.Properties;
using Experior.Dematic.Base;
using Experior.Core.Routes;
using Experior.Catalog.Dematic.Case.Components;
using Experior.Core.Assemblies;
using Experior.Catalog.Assemblies;
using Experior.Catalog.Assemblies.Extra;
using Experior.Dematic.Base.Devices;

namespace Experior.Catalog.Dematic.Storage.Miniload.Assemblies
{
    public class Miniload : HBWMiniLoad, IControllable
    {
        public static List<Miniload> AllCranes = new List<Miniload>();

        private MiniloadInfo MiniloadInfo;
        private List<System.Windows.Forms.ToolStripItem> subMenu;

        private HashSet<Case_Load> caseloads = new HashSet<Case_Load>();
        Case_Load LHD1Load, LHD2Load;

        private StraightConveyor pickConveyor, dropConveyor;
        private StraightConveyor holdingConveyor;
        private List<DematicBox> RackingBoxes = new List<DematicBox>();

        public float BayLength;
        public float LevelHeight;
        public float RackLength;
        public float RackHeight;

        private ActionPoint pos2Pick = new ActionPoint(); 
        private ActionPoint pos1Pick = new ActionPoint();
        private ActionPoint pos2Drop = new ActionPoint();
        private ActionPoint pos1Drop = new ActionPoint();

        private ActionPoint holdingAp = new ActionPoint();

        private bool compartmentoccupied, compartmentempty;

        List<ActionPoint> pickstations = new List<ActionPoint>();
        List<ActionPoint> dropstations = new List<ActionPoint>();

        public MiniloadHalfCycle CurrentHalfCycle;

        public event EventHandler<MiniloadDropStationAvailableChangedEventArgs> OnMiniloadDropStationAvailableChanged;
        public event EventHandler<MiniloadTaskCompleteEventArgs> OnMiniloadTaskComplete;
        public event EventHandler<EventArgs> OnMiniloadReset;

        public virtual void MiniloadTaskComplete(MiniloadTaskCompleteEventArgs eventArgs)
        {
            CurrentHalfCycle = null;
            if (OnMiniloadTaskComplete != null)
            {
                OnMiniloadTaskComplete(this, eventArgs);
            }
        }
        
        public void MiniloadReset(EventArgs eventArgs)
        {
            if (OnMiniloadReset != null)
            {
                OnMiniloadReset(this, eventArgs);
            }
        }

        #region Crane constructor

        public Miniload(MiniloadInfo info)
            : base(info)
        {
            MiniloadInfo = info;

            Lift.Height = 0.05f;
            Lift.Color = Color.DarkBlue;

            StraightConveyorInfo pickConveyorInfo = new StraightConveyorInfo()
            {
                Length = 1.61f,
                thickness = 0.05f,
                Width = 0.5f,
                Speed = 0.7f
            };

            pickConveyor = new StraightConveyor(pickConveyorInfo);
            pickConveyor.endLine.Visible = false;
            pickConveyor.startLine.Visible = false;
            pickConveyor.Color = Color.Gray;
            pickConveyor.arrow.Visible = false;
            pickConveyor.TransportSection.Visible = false;
            pickConveyor.RouteAvailable = RouteStatuses.Blocked;
            pickConveyor.EndFixPoint.Visible = false;
            pickConveyor.EndFixPoint.Enabled = false;

            Add(pickConveyor);
            pickConveyor.TransportSection.Route.InsertActionPoint(pos2Pick);
            pickConveyor.TransportSection.Route.InsertActionPoint(pos1Pick);
            pos2Pick.OnEnter += pos1pd_OnEnter;
            pos1Pick.OnEnter += pos2pd_OnEnter;

            StraightConveyorInfo dropConveyorInfo = new StraightConveyorInfo()
            {
                Length = 1.61f,
                thickness = 0.05f,
                Width = 0.5f,
                Speed = 0.7f
            };

            dropConveyor = new StraightConveyor(dropConveyorInfo);
            dropConveyor.endLine.Visible = false;
            dropConveyor.startLine.Visible = false;
            dropConveyor.Color = Color.Gray;
            dropConveyor.arrow.Visible = false;
            dropConveyor.TransportSection.Visible = false;
            dropConveyor.RouteAvailable = RouteStatuses.Blocked;
            dropConveyor.StartFixPoint.Visible = false;
            dropConveyor.StartFixPoint.Enabled = false;

            Add(dropConveyor);
            dropConveyor.TransportSection.Route.InsertActionPoint(pos2Drop);
            dropConveyor.TransportSection.Route.InsertActionPoint(pos1Drop);

            dropConveyor.OnNextRouteStatusAvailableChanged += dropConveyor_OnNextRouteStatusAvailableChanged;

            StraightConveyorInfo holdingConveyorInfo = new StraightConveyorInfo()
            {
                Length = 1,
                thickness = 0.05f,
                Width = 0.5f,
                Speed = 0.7f
            };

            holdingConveyor = new StraightConveyor(holdingConveyorInfo);
            holdingConveyor.endLine.Visible = false;
            holdingConveyor.startLine.Visible = false;
            holdingConveyor.arrow.Visible = false;
            holdingConveyor.TransportSection.Visible = false;
            holdingConveyor.StartFixPoint.Visible = false;
            holdingConveyor.EndFixPoint.Visible = false;

            Add(holdingConveyor);
            holdingConveyor.TransportSection.Route.InsertActionPoint(holdingAp);

            holdingAp.OnEnter += holdingAp_OnEnter;

            UpdateMiniload();

            AllCranes.Add(this);

            subMenu = new List<System.Windows.Forms.ToolStripItem>();
            subMenu.Add(new System.Windows.Forms.ToolStripMenuItem("Reset", Common.Icons.Get("fault")));
            subMenu[0].Click += new EventHandler(DematicHBW_ClickReset);
            //subMenu.Add(new System.Windows.Forms.ToolStripMenuItem("Set compartment occupied", Common.Icons.Get("fault")));
            //subMenu[1].Click += new EventHandler(DematicHBW_ClickCompartmentOccupied);
            //subMenu.Add(new System.Windows.Forms.ToolStripMenuItem("Set compartment empty", Common.Icons.Get("fault")));
            //subMenu[2].Click += new EventHandler(DematicHBW_ClickCompartmentEmpty);
            //subMenu.Add(new System.Windows.Forms.ToolStripMenuItem("Fault Miniload", Common.Icons.Get("fault")));
            //subMenu[3].Click += new EventHandler(DematicHBW_ClickFault);

            Control.FinishedJob += Control_FinishedJob;
            Control.LoadDropped += Control_LoadDropped;

            LoadingSpeed = 1;
            UnloadingSpeed = 1;

            ControllerProperties = StandardCase.SetMHEControl(info, this);
            Experior.Core.Environment.Scene.OnLoaded += Scene_OnLoaded;
        }

        void dropConveyor_OnNextRouteStatusAvailableChanged(object sender, RouteStatusChangedEventArgs e)
        {
            //When the conveyor connected to the dropConveyor Status changes then i want to know about it
            //Set the status of the drop station 
            if (e._available == RouteStatuses.Available)
            {
                DropStationAvailable = true;
            }
            else
            {
                DropStationAvailable = false;
            }
            
            //If it goes blocked, then the transfer from the miniload is complete and i can start the next job
            if (e._available == RouteStatuses.Blocked)
            {
                if (CurrentHalfCycle != null)
                {
                    MiniloadTaskComplete(new MiniloadTaskCompleteEventArgs(CurrentHalfCycle));
                }
            }
        }

        void holdingAp_OnEnter(ActionPoint sender, Load load)
        {
            load.Stop();
        }

        void Scene_OnLoaded()
        {
            if (ControllerProperties == null)
            {
                ControllerProperties = StandardCase.SetMHEControl(MiniloadInfo, this);
            }
        }

        #endregion

        private void UpdateMiniload()
        {
            Lift.LocalYaw = (float)Math.PI / 2;
            Lift.LocalPosition = new Vector3(Lift.LocalPosition.X, MiniloadInfo.PdOffsetHeight, Lift.LocalPosition.Z);

            pickConveyor.Length = MiniloadInfo.AisleWidth;
            dropConveyor.Length = MiniloadInfo.AisleWidth;
            pickConveyor.LocalPosition = new Vector3(pickConveyor.Width / 2, MiniloadInfo.PdOffsetHeight, pickConveyor.LocalPosition.Z);
            dropConveyor.LocalPosition = new Vector3(pickConveyor.Width / 2, MiniloadInfo.PdOffsetHeight, pickConveyor.LocalPosition.Z);

            if (PickAndDropSide == PickDropSide.DropLeft_PickRight)
            {
                pickConveyor.LocalYaw = (float)Math.PI / 2;
                dropConveyor.LocalYaw = (float)Math.PI / 2;
            }
            else
            {
                pickConveyor.LocalYaw = -(float)Math.PI / 2;
                dropConveyor.LocalYaw = -(float)Math.PI / 2;
            }

            pos2Pick.Distance = MiniloadInfo.LHDLength / 2;
            pos2Drop.Distance = MiniloadInfo.LHDLength / 2;
            pos1Pick.Distance = MiniloadInfo.LHDLength + MiniloadInfo.LHDSpacing + (MiniloadInfo.LHDLength / 2);
            pos1Drop.Distance = MiniloadInfo.LHDLength + MiniloadInfo.LHDSpacing + (MiniloadInfo.LHDLength / 2);
            CreateRackBoxes();
        }

        private void CreateRackBoxes()
        {
            RackHeight = MiniloadInfo.Craneheight - MiniloadInfo.RackOffsetBottom + 0.5f;
            RackLength = MiniloadInfo.RailLength - MiniloadInfo.RackOffsetFront;
            LevelHeight = RackHeight / MiniloadInfo.RackLevels;
            BayLength = RackLength / MiniloadInfo.RackBays;

            foreach (DematicBox box in RackingBoxes)
            {
                Remove(box);
                Remove(box as Core.IEntity);
                box.Dispose();
            }
            RackingBoxes.Clear();

            DematicBoxInfo boxInfo = new DematicBoxInfo()
            {
                thickness = 0.01f,
                length = MiniloadInfo.RailLength - MiniloadInfo.RackOffsetFront,
                width = MiniloadInfo.RackWidth,
                height = RackHeight,
                transparency = MiniloadInfo.RackTransparency,
                boxColor = MiniloadInfo.RackColor
            };

            DematicBox rack1 = new DematicBox(boxInfo);
            RackingBoxes.Add(rack1);
            Add(rack1, new Vector3(MiniloadInfo.RackOffsetFront + boxInfo.thickness + (RackLength / 2), MiniloadInfo.RackOffsetBottom, boxInfo.thickness + MiniloadInfo.AisleWidth / 2));

            DematicBox rack2 = new DematicBox(boxInfo);
            RackingBoxes.Add(rack2);
            Add(rack2, new Vector3(MiniloadInfo.RackOffsetFront + boxInfo.thickness + (RackLength / 2), MiniloadInfo.RackOffsetBottom, -(boxInfo.width + boxInfo.thickness + MiniloadInfo.AisleWidth / 2)));
        }

        #region Miniload Logic
        public bool StartMiniloadHalfCycle(MiniloadHalfCycle HalfCycle)
        {
            if (CurrentHalfCycle != null) //reject if the miniload is running, there is a CurrentHalfCycle
            {
                return false;
            }
            else
            {
                CurrentHalfCycle = HalfCycle;
                if (HalfCycle.Cycle == MiniloadCycle.PickPS) //This will pick whatever is at the pick station
                {
                    if (liftcube.LocalPosition.Y == PdOffsetHeight && liftcube.LocalPosition.Z == 0) //Miniload is already at the P&D so doesn't need to move
                    {
                        Core.Timer pickdelay = new Core.Timer(0.5f);
                        pickdelay.OnElapsed += pickdelay_OnElapsed;
                        pickdelay.Start();
                        return true;
                    }

                    Control.Goto(PdOffsetHeight, 0, "GotoPS");
                    Control.JobQueue[0].UserData = HalfCycle;
                    Control.StartCrane();
                    return true;
                }
                else if (HalfCycle.Cycle == MiniloadCycle.DropDS)
                {
                    if (liftcube.LocalPosition.Y == PdOffsetHeight && liftcube.LocalPosition.Z == 0) //Miniload is already at the P&D so doesn't need to move
                    {
                        Core.Timer dropdelay = new Core.Timer(0.5f);
                        dropdelay.OnElapsed += dropdelay_OnElapsed;
                        dropdelay.UserData = HalfCycle;
                        dropdelay.Start();
                        return true;
                    }

                    Control.Goto(PdOffsetHeight, 0, "GotoDS");
                    Control.JobQueue[0].UserData = HalfCycle;
                    Control.StartCrane();
                    return true;
                }
                else if (HalfCycle.Cycle == MiniloadCycle.DropRack)
                {
                    Control.Goto(HalfCycle.Height, HalfCycle.Length, "GotoRack");
                    Control.JobQueue[0].UserData = HalfCycle;
                    Control.DropLoad(DepthTime(HalfCycle.Depth), true, "DropRack", HalfCycle.LHD == 1 ? HBWMiniLoadJob.LHDs.LHD1 : HBWMiniLoadJob.LHDs.LHD2);
                    Control.JobQueue[1].UserData = HalfCycle;
                    Control.StartCrane();
                    return true;
                }
                else if (HalfCycle.Cycle == MiniloadCycle.PickRack)
                {
                    float depth = (AisleWidth / 2) + ((RackWidth / (DepthsInRack + 1)) * HalfCycle.Depth);
                    holdingConveyor.LocalPosition = new Vector3(HalfCycle.Length - (LHDWidth /2), HalfCycle.Height, HalfCycle.RackSide == Side.Left ? depth : -depth);

                    Control.Goto(HalfCycle.Height, HalfCycle.Length, "GotoRack");
                    Control.JobQueue[0].UserData = HalfCycle;

                    Case_Load caseLoad = CreateCaseLoad(HalfCycle.CaseData, HalfCycle.LHD);

                    Control.PickLoad(DepthTime(HalfCycle.Depth), new List<Load>() { caseLoad }, true, "PickRack", HalfCycle.LHD == 1 ? HBWMiniLoadJob.LHDs.LHD1 : HBWMiniLoadJob.LHDs.LHD2);
                    Control.JobQueue[1].UserData = HalfCycle;
                    Control.StartCrane();
                    return true;
                }
            }
            return false;
        }

        private float DepthTime(int Depth)
        {
            switch(Depth)
            {
                case 1: return TimeToDepth1;
                case 2: return TimeToDepth2;
                case 3: return TimeToDepth3;
            }
            return 10;
        }

        void pickdelay_OnElapsed(Core.Timer sender)
        {
            sender.OnElapsed -= pickdelay_OnElapsed;
            pickConveyor.RouteAvailable = RouteStatuses.Available;
        }

        void dropdelay_OnElapsed(Core.Timer sender)
        {
            sender.OnElapsed -= dropdelay_OnElapsed;
            DropDropStation((MiniloadHalfCycle)sender.UserData);
        }

        void Control_FinishedJob(HBWMiniLoad hbw, HBWMiniLoadJob job)
        {
            if (job.JobType == HBWMiniLoadJob.JobTypes.Goto) { }
            
            if (job.ID == "GotoPS")
            {
                pickConveyor.RouteAvailable = RouteStatuses.Available;
            }
            else if (job.ID == "GotoDS")
            {
                DropDropStation((MiniloadHalfCycle)job.UserData);
            }
            else if (job.ID == "PickPS" || job.ID == "DropRack" || job.ID == "PickRack")// || job.ID == "DropDS")
            {
                if (control.JobQueue.Count == 0) //Load has been picked or dropped
                {
                    MiniloadTaskComplete(new MiniloadTaskCompleteEventArgs(CurrentHalfCycle));
                }
            }
        }

        void DropDropStation(MiniloadHalfCycle halfCycle)
        {
            if (halfCycle.TuIdentPos1 != null)
            {
                Control.DropLoad(0, false, "DropDS", HBWMiniLoadJob.LHDs.LHD1);
            }
            if (halfCycle.TuIdentPos2 != null)
            {
                Control.DropLoad(0, false, "DropDS", HBWMiniLoadJob.LHDs.LHD2);
            }
            Control.StartCrane();
        }

        void Control_LoadDropped(HBWMiniLoad hbw, Load load, bool Rack, string ID, int LHD, HBWMiniLoadJob job)
        {
            if (!job.Rack)
            {
                if (LHD == 1)
                {
                    load.Switch(pos1Drop, true);
                }
                else if (LHD == 2)
                {
                    load.Switch(pos2Drop, true);
                }
                load.Movable = true;
                load.UserDeletable = true;
                caseloads.Remove((Case_Load)load);
            }
        }

        int PickRouteCount()
        {
            StraightConveyor pickStation = pickConveyor.PreviousConveyor as StraightConveyor;
            if (pickStation != null)
            {
                return pickConveyor.TransportSection.Route.Loads.Count + pickStation.TransportSection.Route.Loads.Count;
            }
            else 
            {
                return pickConveyor.TransportSection.Route.Loads.Count;
            }
        }

        void pos1pd_OnEnter(ActionPoint sender, Load load) //First position the load comes to
        {
            if (pickConveyor.TransportSection.Route.Loads.Count == 2 && pickConveyor.TransportSection.Route.Loads.Last().Distance > load.Distance)
            {
                load.Stop();
            }

            if (PickRouteCount() == 2 && pos1Pick.Active)
            {
                LoadsArrivedAtPickStation(pos1Pick.ActiveLoad, load);
            }
            else if (PickRouteCount() == 1)
            {
                LoadsArrivedAtPickStation(null, load);
            }
        }

        void pos2pd_OnEnter(ActionPoint sender, Load load) //Second position the load comes to
        {
            load.Stop();
            if (PickRouteCount() == 2 && pos2Pick.Active)
            {
                LoadsArrivedAtPickStation(load, pos2Pick.ActiveLoad);        
            }
            else if (PickRouteCount() == 1)
            {
                LoadsArrivedAtPickStation(load, null);
            }
        }

        private void LoadsArrivedAtPickStation(Load load1, Load load2)
        {
            //Allow the next loads to transfer into the PS
            pickConveyor.RouteAvailable = RouteStatuses.Blocked;
            
            //Switch the loads onto the miniload
            if (load1 != null)
            {
                Control.PickLoad(0, new List<Load> { load1 }, false, "PickPS", HBWMiniLoadJob.LHDs.LHD1);
            }
            if (load2 != null)
            {
                Control.PickLoad(0, new List<Load> { load2 }, false, "PickPS", HBWMiniLoadJob.LHDs.LHD2);
            }
            Control.StartCrane();
        }
        #endregion

        #region Pick Station Methods

        /// <summary>
        /// Stop more loads travelling into the pick station when picking single load
        /// </summary>
        public void  LockPickStation()
        {
            MiniloadPickStation pickStation = pickConveyor.PreviousConveyor as MiniloadPickStation;
            pickStation.LockPickStation();
        }

        #endregion

        #region Helper Methods
        Case_Load CreateCaseLoad(BaseCaseData caseData, int lHD)
        {
            Case_Load caseLoad = Controller.CreateCaseLoad(caseData);
            caseLoad.UserDeletable = false;
            caseLoad.Movable = false;
            Case_Load.Items.Add(caseLoad);
            caseLoad.Switch(holdingAp);
            caseLoad.Yaw = (float)Math.PI / 2;
            caseloads.Add(caseLoad);

            if (lHD == 1)
            {
                LHD1Load = caseLoad;
            }
            else
            {
                LHD2Load = caseLoad;
            }

            return caseLoad;
        }
        
        public float CalculateHeightFromYLoc(string yLoc)
        {
            int result = 0;
            int.TryParse(yLoc, out result);
            return CalculateHeightFromYLoc(result);
        }

        public float CalculateHeightFromYLoc(int yLoc)
        {
            if (yLoc > RackLevels || yLoc == 0)
            {
                Log.Write(string.Format("Miniload {0}: Cannot send to Y Location {1}, not enough levels or sent to position 0, crane will travel to height 0", Name, yLoc));
                return 0;
            }

            float pitch = RackHeight / RackLevels;
            return (pitch * (yLoc - 1)) + RackOffsetBottom; 
        }

        public float CalculateLengthFromXLoc(string xLoc)
        {
            int result = 0;
            int.TryParse(xLoc, out result);
            return CalculateLengthFromXLoc(result);
        }

        public float CalculateLengthFromXLoc(string xLoc, string raster, string position)
        {
            int resultX = 0, resultR = 0, resultP = 0;
            int.TryParse(xLoc, out resultX);
            int.TryParse(raster, out resultR);
            int.TryParse(position, out resultP);

            if (resultX == 0 || resultR == 0 || resultP == 0)
            {
                return CalculateLengthFromXLoc(0);
            }
            else
            {
                return CalculateLengthFromXLoc(((resultX - 1) * resultR) + resultP);
            }
        }

        public float CalculateLengthFromXLoc(int xLoc)
        {
            if (xLoc > RackBays || xLoc == 0)
            {
                Log.Write(string.Format("Miniload {0}: Cannot send to X Location {1}, not enough bays or sent to position 0, crane will travel to length 0", Name, xLoc));
                return 0;
            }
            float pitch = RackLength / RackBays;
            return (pitch * (xLoc - 1)) + RackOffsetFront;
        }

        public Case_Load Position1Load()
        {
            MiniloadPickStation pickStation = pickConveyor.PreviousConveyor as MiniloadPickStation;
            if (pickStation != null)
            {
                return pickStation.Position1Load as Case_Load;
            }
            else
            {
                return null;
            }
        }

        public Case_Load Position2Load()
        {
            MiniloadPickStation pickStation = pickConveyor.PreviousConveyor as MiniloadPickStation;
            if (pickStation != null)
            {
                return pickStation.Position2Load as Case_Load;
            }
            else
            {
                return null;
            }
        }

        #endregion

        public override string Category
        {
            get { return "DematicHBWMiniLoad"; }
        }

        public override Image Image
        {
            get
            {
                return Experior.Catalog.Common.Icons.Get("DematicHBWMiniLoad");
            }
        }

        #region Overriding Assembly methods

        public override List<System.Windows.Forms.ToolStripItem> ShowContextMenu()
        {
            //return base.ShowContextMenu();
            //if (compartmentoccupied)
            //{
            //    subMenu[1].Text = "Set compartment free";
            //}
            //else
            //{
            //    subMenu[1].Text = "Set compartment occupied";
            //}

            //if (compartmentempty)
            //{
            //    subMenu[2].Text = "Set compartment normal";
            //}
            //else
            //{
            //    subMenu[2].Text = "Set compartment empty";
            //}
            return new List<System.Windows.Forms.ToolStripItem>(subMenu);
        }

        public override void Reset()
        {
            foreach (Case_Load c in caseloads)
            {
                c.UserDeletable = true;
                c.Deletable = true;
                c.Dispose();
                if (c.Route != null)
                    c.Route.Remove(c);
            }

            caseloads.Clear();

            compartmentempty = false;
            compartmentoccupied = false;

            base.Reset();
            pickConveyor.RouteAvailable = RouteStatuses.Blocked;
            CurrentHalfCycle = null;
            MiniloadReset(new EventArgs());
        }
       
        public override void Dispose()
        {
            subMenu[0].Click -= new EventHandler(DematicHBW_ClickReset);
            //subMenu[1].Click -= new EventHandler(DematicHBW_ClickCompartmentOccupied);
            //subMenu[2].Click -= new EventHandler(DematicHBW_ClickCompartmentEmpty);
            //subMenu[3].Click -= new EventHandler(DematicHBW_ClickFault);
            subMenu.Clear();

            AllCranes.Remove(this);

            base.Dispose();
        }
        #endregion

        #region User interface
        void DematicHBW_ClickCompartmentOccupied(object sender, EventArgs e)
        {
            compartmentoccupied = !compartmentoccupied;
        }

        void DematicHBW_ClickCompartmentEmpty(object sender, EventArgs e)
        {
            compartmentempty = !compartmentempty;
        }

        void DematicHBW_ClickReset(object sender, EventArgs e)
        {
            Core.Environment.Invoke(() => Reset());
        }

        void DematicHBW_ClickFault(object sender, EventArgs e)
        {
            Control.StopCrane();
        }

        private void WriteTextToLog(string text, Color color)
        {
            string dateTime = System.DateTime.Now.ToString();
            Core.Environment.Log.Write(dateTime + ": " + Name + " | " + text, color);
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
        public override float LoadingSpeed
        {
            get
            {
                return base.LoadingSpeed;
            }
            set
            {
                base.LoadingSpeed = value;
            }
        }

        [Browsable(false)]
        public override float UnloadingSpeed
        {
            get
            {
                return base.UnloadingSpeed;
            }
            set
            {
                base.UnloadingSpeed = value;
            }
        }

        [Browsable(false)]
        public override float PositioningTime
        {
            get
            {
                return base.PositioningTime;
            }
            set
            {
                base.PositioningTime = value;
            }
        }

        [CategoryAttribute("Pick and Drop Stations")]
        [PropertyOrder(1)]
        [DescriptionAttribute("Offset height of pick and drop station from bottom of rail")]
        [DisplayName("P&D Station Height")]
        public float PdOffsetHeight
        {
            get { return MiniloadInfo.PdOffsetHeight; }
            set 
            {
                if (MiniloadInfo.PdOffsetHeight != value)
                {
                    if (value > RackHeight)
                    {
                        WriteTextToLog("User tried to set the pick and drop station height higher than the rack, which is not allowed. Setting the rack back to the previous value (" + MiniloadInfo.PdOffsetHeight + ")", Color.Red);
                        value = MiniloadInfo.PdOffsetHeight;
                    }
                    MiniloadInfo.PdOffsetHeight = value;
                    Core.Environment.Invoke(() => UpdateMiniload());

                }
            }
        }

        [CategoryAttribute("Pick and Drop Stations")]
        [PropertyOrder(2)]
        [DescriptionAttribute("Width of aisle and LHD")]
        [DisplayName("Aisle Width")]
        public float AisleWidth
        {
            get { return MiniloadInfo.AisleWidth; }
            set
            {
                if (MiniloadInfo.AisleWidth != value)
                {
                    MiniloadInfo.AisleWidth = value;
                }
                Core.Environment.Invoke(() => UpdateMiniload());
            }
        }

        [CategoryAttribute("Pick and Drop Stations")]
        [PropertyOrder(2)]
        [DescriptionAttribute("Which side of the aisle the pick and drop stations are relative to the front of the aisle")]
        [DisplayName("Pick and Drop Sides")]
        public Miniload.PickDropSide PickAndDropSide
        {
            get { return MiniloadInfo.PickAndDropSide; }
            set
            {
                if (MiniloadInfo.PickAndDropSide != value)
                {
                    MiniloadInfo.PickAndDropSide = value;
                }
                Core.Environment.Invoke(() => UpdateMiniload());
            }
        }

        [CategoryAttribute("Racking")]
        [PropertyOrder(3)]
        [DescriptionAttribute("The distance from the rail begins until the rack begins")]
        [DisplayName("Rack offset, front (m.)")]
        public float RackOffsetFront
        {
            get { return MiniloadInfo.RackOffsetFront; }
            set
            {
                if (MiniloadInfo.RackOffsetFront != value)
                {
                    if ((value + 1) > Raillength)
                    {
                        Log.Write(string.Format("Miniload {0}: Rack offset front set too high", Name));
                        return;
                    }

                    MiniloadInfo.RackOffsetFront = value;
                    Core.Environment.Invoke(new Action(UpdateMiniload));

                }
            }
        }

        public override float Raillength
        {
            get
            {
                return base.Raillength;
            }
            set
            {
                base.Raillength = value;
                CreateRackBoxes();
            }
        }

        public override float Craneheight
        {
            get
            {
                return base.Craneheight;
            }
            set
            {
                base.Craneheight = value;
                CreateRackBoxes();
            }
        }

        [CategoryAttribute("Racking")]
        [PropertyOrder(5)]
        [DescriptionAttribute("The distance from the bottom of the rail to bottom of the rack")]
        [DisplayName("Rack offset, bottom (m.)")]
        public float RackOffsetBottom
        {
            get { return MiniloadInfo.RackOffsetBottom; }
            set 
            { 
                MiniloadInfo.RackOffsetBottom = value;
                Core.Environment.Invoke(new Action(UpdateMiniload));
            }
        }

        [CategoryAttribute("Racking")]
        [PropertyOrder(6)]
        [DescriptionAttribute("The number of bays in rack (presumably evenly distributed over the length of the rack)")]
        [DisplayName("Bays in rack")]
        public int RackBays
        {
            get { return MiniloadInfo.RackBays; }
            set
            {
                if (MiniloadInfo.RackBays != value)
                {
                    if (value < 1)
                    {
                        WriteTextToLog("User tried to define an illegal number of bays (" + value + "). Setting the value back to the previous one (" + MiniloadInfo.RackBays + ")", Color.Red);
                        value = MiniloadInfo.RackBays;
                    }
                    MiniloadInfo.RackBays = value;
                    BayLength = RackLength / MiniloadInfo.RackBays;
                }
            }
        }

        [Category("Racking")]
        [PropertyOrder(8)]
        [Description("The width of each rack on both sides of the miniload")]
        [DisplayName("Rack width (m.)")]
        public float RackWidth
        {
            get { return MiniloadInfo.RackWidth; }
            set
            {
                MiniloadInfo.RackWidth = value;
                CreateRackBoxes();
            }
        }

        [CategoryAttribute("Racking")]
        [PropertyOrder(9)]
        [DescriptionAttribute("The number of levels in rack (presumably evenly distributed over the height of the rack)")]
        [DisplayName("Levels in rack")]
        public int RackLevels
        {
            get { return MiniloadInfo.RackLevels; }
            set
            {
                if (MiniloadInfo.RackLevels != value)
                {
                    if (value < 1)
                    {
                        WriteTextToLog("User tried to define an illegal number of levels (" + value + "). Setting the value back to the previous one (" + MiniloadInfo.RackLevels + ")", Color.Red);
                        value = MiniloadInfo.RackLevels;
                    }
                    MiniloadInfo.RackLevels = value;
                    LevelHeight = RackHeight / MiniloadInfo.RackLevels;
                }
            }
        }

        [Category("Racking")]
        [PropertyOrder(10)]
        [Description("Color of the rack")]
        [DisplayName("Racking Color")]
        public Color RackColor
        {
            get { return MiniloadInfo.RackColor; }
            set
            {
                MiniloadInfo.RackColor = value;
                CreateRackBoxes();
            }
        }

        [Category("Racking")]
        [PropertyOrder(11)]
        [Description("Set the transparency of the racking")]
        [DisplayName("Racking Transparency")]
        public int RackTransparency
        {
            get { return MiniloadInfo.RackTransparency; }
            set
            {
                if (value > 255) { value = 255; }
                else if (value < 0) { value = 0; }
                MiniloadInfo.RackTransparency = value;
                CreateRackBoxes();
            }
        }

        [Category("LHD Times")]
        [DisplayName("Depths in Rack")]
        [Description("Number of depths in the racking to configure times for")]
        public int DepthsInRack
        {
            get { return MiniloadInfo.DepthsInRack; }
            set
            {
                if (value > 0 && value < 4) 
                {
                    MiniloadInfo.DepthsInRack = value;
                    Core.Environment.Properties.Refresh();
                }
            }
        }

        [Category("LHD Times")]
        [DisplayName("Time to depth 1 (s)")]
        [Description("How long it takes to load or unload from depth 1")]
        public float TimeToDepth1
        {
            get { return MiniloadInfo.TimeToDepth1; }
            set
            {
                MiniloadInfo.TimeToDepth1 = value;
            }
        }

        [Category("LHD Times")]
        [DisplayName("Time to depth 2 (s)")]
        [Description("How long it takes to load or unload from depth 2")]
        [PropertyAttributesProvider("DynamicPropertyShowDepth2")]
        public float TimeToDepth2
        {
            get { return MiniloadInfo.TimeToDepth2; }
            set
            {
                MiniloadInfo.TimeToDepth2 = value;
            }
        }

        [Category("LHD Times")]
        [DisplayName("Time to depth 3 (s)")]
        [Description("How long it takes to load or unload from depth 3")]
        [PropertyAttributesProvider("DynamicPropertyShowDepth3")]
        public float TimeToDepth3
        {
            get { return MiniloadInfo.TimeToDepth3; }
            set
            {
                MiniloadInfo.TimeToDepth3 = value;
            }
        }

        public void DynamicPropertyShowDepth2(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = DepthsInRack > 1;
        }

        public void DynamicPropertyShowDepth3(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = DepthsInRack > 2;
        }
        #endregion

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

        void controller_OnControllerRenamedEvent(object sender, EventArgs e)
        {
            MiniloadInfo.ControllerName = ((Assembly)sender).Name;
            Core.Environment.Properties.Refresh();
        }

        void controller_OnControllerDeletedEvent(object sender, EventArgs e)
        {
            controller.OnControllerDeletedEvent -= controller_OnControllerDeletedEvent;
            ControllerName = "No Controller";
            Controller = null;
            ControllerProperties = null;
        }

        [Category("Configuration")]
        [DisplayName("Controller")]
        [Description("Controller name that handles this miniload")]
        [TypeConverter(typeof(CaseControllerConverter))] //TODO CaseControllerConverter is in the communication point move to a base class
        public string ControllerName
        {
            get
            {
                return MiniloadInfo.ControllerName;
            }
            set
            {
                if (!value.Equals(MiniloadInfo.ControllerName))
                {
                    ControllerProperties = null;
                    MiniloadInfo.ProtocolInfo = null;
                    Controller = null;
                }

                MiniloadInfo.ControllerName = value;
                if (value != null)
                {
                    ControllerProperties = StandardCase.SetMHEControl(MiniloadInfo, this);

                    if (ControllerProperties == null)
                    {
                        MiniloadInfo.ControllerName = "No Controller";
                    }
                }
            }
        }

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

        public void DynamicPropertyAssemblyPLCconfig(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = Controller != null;
        }
        #endregion

        #region Interface to controller object
        [Browsable(false)]
        public HashSet<Case_Load> Caseloads
        {
            get { return caseloads; }
        }

        public enum PickDropSide
        {
            PickLeft_DropRight,
            DropLeft_PickRight
        }

        [Browsable(false)]
        public Load[] CurrentLoads
        {
            get { return currentLoads; }
        }

        [Browsable(false)]
        public bool Compartmentoccupied
        {
            get { return compartmentoccupied; }
            set { compartmentoccupied = value; }
        }

        [Browsable(false)]
        public bool Compartmentempty
        {
            get { return compartmentempty; }
            set { compartmentempty = value; }
        }

        [Browsable(false)]
        public float LHDCurrentHeight
        {
            get { return liftcube.LocalPosition.Y; }
        }

        [Browsable(false)]
        public float LHDCurrentLength
        {
            get { return liftcube.LocalPosition.Z; }
        }

        private bool dropStationAvailable = true;
        [Browsable(false)]
        public bool DropStationAvailable
        {
            get { return dropStationAvailable; }
            set 
            { 
                dropStationAvailable = value;
                if (OnMiniloadDropStationAvailableChanged != null)
                {
                    OnMiniloadDropStationAvailableChanged(this, new MiniloadDropStationAvailableChangedEventArgs(value));
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// Used to show a list of avialable miniloadcontrollers  
    /// </summary>
    public class SCSConverter : StringConverter
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
    }

    public enum MiniloadCycle
    {
        PickRack,
        DropRack,
        PickPS,
        DropDS,
        None
    }

    public enum MiniloadTaskType
    {
        Storage,    //pick load(s) from pick station and store in the racking
        Retreival,  //pick load(s) from racking and drop at the drop station
        Relocation, //pick load(s) from racking and store at another location in racking
        Reject,     //pick load(s) from pick station and drop at the drop station
    }

    public class MiniloadHalfCycle
    {
        public float Height;
        public float Length;
        public int Depth;
        public Side? RackSide;
        public int LHD;
        public MiniloadCycle Cycle;
        public string TuIdentPos1;
        public string TuIdentPos2;
        public BaseCaseData CaseData;
    }

    public class MiniloadTask
    {
        private List<object> _MissionData;
        public List<object> MissionData
        {
            get { return _MissionData; }
        }
        public MiniloadTaskType TaskType;
        public List<MiniloadHalfCycle> HalfCycles;

        public MiniloadTask(List<object> missionData)
        {
            _MissionData = missionData;
            HalfCycles = new List<MiniloadHalfCycle>();
        }
    }

    public class MiniloadTaskCompleteEventArgs : EventArgs
    {
        public readonly MiniloadHalfCycle _miniloadTask;
        public MiniloadTaskCompleteEventArgs(MiniloadHalfCycle task)
        {
            _miniloadTask = task;
        }
    }

    public class MiniloadDropStationAvailableChangedEventArgs : EventArgs
    {
        public readonly bool _available;
        public MiniloadDropStationAvailableChangedEventArgs(bool available)
        {
            _available = available;
        }
    }
}
