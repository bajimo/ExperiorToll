using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using Experior.Catalog.Assemblies;
using Experior.Core.Assemblies;
using Experior.Core.Loads;
using Experior.Core.Properties;
using Experior.Core.Routes;
using Experior.Dematic.Base;
using Microsoft.DirectX;
using Experior.Catalog.Assemblies.Extra;
using Experior.Catalog.Dematic.Pallet.Assemblies;
using Experior.Core.Parts;
using Experior.Core.Properties.Collections;
using Experior.Core.TransportSections;
using Experior.Dematic.Base.Devices;

namespace Experior.Catalog.Dematic.Storage.PalletCrane.Assemblies
{
    public sealed class PalletCrane : HBWMiniLoad, IControllable
    {
        public static List<PalletCrane> AllCranes = new List<PalletCrane>();

        private readonly PalletCraneInfo palletCraneInfo;
        private readonly List<System.Windows.Forms.ToolStripItem> subMenu;

        private readonly HashSet<Load> craneLoads = new HashSet<Load>();

        private readonly List<PalletStation> pickConveyors, dropConveyors;
        private readonly HashSet<IPalletRouteStatus> pickStationConveyors = new HashSet<IPalletRouteStatus>();
        private readonly StraightTransportSection holdingConveyor;
        private readonly List<DematicBox> rackingBoxes = new List<DematicBox>();

        public float BayLength;
        public float LevelHeight;
        public float RackLength;
        public float RackHeight;

        private readonly ActionPoint holdingAp = new ActionPoint();

        private bool compartmentoccupied, compartmentempty;

        public PalletCraneHalfCycle CurrentHalfCycle;

        public event EventHandler<PalletCraneDropStationAvailableChangedEventArgs> OnPalletCraneDropStationAvailableChanged;
        public event EventHandler<PalletCraneTaskCompleteEventArgs> OnPalletCraneTaskComplete;
        public event EventHandler<EventArgs> OnPalletCraneReset;
        public event EventHandler<PalletStationEventArgs> OnPalletArrivedAtPickStation;
        public event EventHandler<DropStationEventArgs> OnPalletMovingToDropStation;

        private void PalletArrivedAtPickStation(IPalletRouteStatus status, Load load)
        {
            var pickStation = pickConveyors.FirstOrDefault(p => p.PreviousConveyor == status);
            var name = pickStation?.Configuration.Name;
            OnPalletArrivedAtPickStation?.Invoke(this, new PalletStationEventArgs(status, load, name));
        }

        private void PalletMovingToDropStation(Load load, string dropStationName)
        {
            OnPalletMovingToDropStation?.Invoke(this, new DropStationEventArgs(load, dropStationName));
        }

        private void PalletCraneTaskComplete(PalletCraneTaskCompleteEventArgs eventArgs)
        {
            CurrentHalfCycle = null;
            OnPalletCraneTaskComplete?.Invoke(this, eventArgs);
        }

        private void PalletCraneReset(EventArgs eventArgs)
        {
            OnPalletCraneReset?.Invoke(this, eventArgs);
        }

        public PalletCrane(PalletCraneInfo info)
            : base(info)
        {
            palletCraneInfo = info;

            if (info.PickStations == null)
            {
                info.PickStations = new ExpandablePropertyList<StationConfiguration>();
            }
            if (!info.PickStations.Any())
            {
                info.PickStations.Add(new StationConfiguration { LevelHeight = 1, StationType = PalletCraneStationTypes.PickStation, Side = PalletCraneStationSides.Right, Length = 1.61f, thickness = 0.05f, Width = 0.978f, Speed = 0.7f, ConveyorType = PalletConveyorType.Roller});
            }
            if (info.DropStations == null)
            {
                info.DropStations = new ExpandablePropertyList<StationConfiguration>();
            }
            if (!info.DropStations.Any())
            {
                info.DropStations.Add(new StationConfiguration { LevelHeight = 1, StationType = PalletCraneStationTypes.DropStation, Side = PalletCraneStationSides.Left, Length = 1.61f, thickness = 0.05f, Width = 0.978f, Speed = 0.7f, ConveyorType = PalletConveyorType.Roller });
            }

            Lift.Height = 0.05f;
            Lift.Color = Color.DarkBlue;

            pickConveyors = new List<PalletStation>();
            foreach (var config in info.PickStations)
            {
                var pickConveyor = new PalletStation(config);
                Add(pickConveyor);
                pickConveyor.PositionAp.OnEnter += PosPd_OnEnter;
                pickConveyor.StartFixPoint.OnSnapped += PickConveyorStartFixPoint_OnSnapped;
                pickConveyor.StartFixPoint.OnUnSnapped += PickConveyorStartFixPoint_OnUnSnapped;
                pickConveyors.Add(pickConveyor);
            }

            dropConveyors = new List<PalletStation>();
            foreach (var config in info.DropStations)
            {
                var dropConveyor = new PalletStation(config);
                Add(dropConveyor);
                dropConveyor.ThisRouteStatus.OnRouteStatusChanged += DropConveyor_OnRouteStatusChanged;
                dropConveyors.Add(dropConveyor);
            }

            holdingConveyor = new StraightTransportSection(Color.Black, 1, 0, 0.5f);
            Add(holdingConveyor);
            holdingConveyor.Route.InsertActionPoint(holdingAp, holdingConveyor.Length / 2);
            holdingConveyor.Route.Motor.Speed = 0.7f;
            holdingConveyor.Visible = false;

            holdingAp.OnEnter += HoldingAp_OnEnter;

            UpdatePalletCrane();

            AllCranes.Add(this);

            subMenu = new List<System.Windows.Forms.ToolStripItem>();
            subMenu.Add(new System.Windows.Forms.ToolStripMenuItem("Reset", Common.Icons.Get("fault")));
            subMenu[0].Click += DematicHBW_ClickReset;
            //subMenu.Add(new System.Windows.Forms.ToolStripMenuItem("Set compartment occupied", Common.Icons.Get("fault")));
            //subMenu[1].Click += new EventHandler(DematicHBW_ClickCompartmentOccupied);
            //subMenu.Add(new System.Windows.Forms.ToolStripMenuItem("Set compartment empty", Common.Icons.Get("fault")));
            //subMenu[2].Click += new EventHandler(DematicHBW_ClickCompartmentEmpty);
            //subMenu.Add(new System.Windows.Forms.ToolStripMenuItem("Fault PalletCrane", Common.Icons.Get("fault")));
            //subMenu[3].Click += new EventHandler(DematicHBW_ClickFault);

            Control.FinishedJob += Control_FinishedJob;
            Control.LoadDropped += Control_LoadDropped;

            LoadingSpeed = 1;
            UnloadingSpeed = 1;

            ControllerProperties = StandardCase.SetMHEControl(info, this);
            Core.Environment.Scene.OnLoaded += Scene_OnLoaded;
        }

        private void PickConveyorStartFixPoint_OnUnSnapped(Core.Parts.FixPoint stranger)
        {
            var conveyor = stranger.Parent as IPalletRouteStatus;
            if (conveyor != null)
            {
                conveyor.OnLoadArrived -= Conveyor_OnLoadArrived;
                pickStationConveyors.Remove(conveyor);
            }
        }

        private void PickConveyorStartFixPoint_OnSnapped(FixPoint stranger, FixPoint.SnapEventArgs e)
        {
            var conveyor = stranger.Parent as IPalletRouteStatus;
            if (conveyor != null)
            {
                if (pickStationConveyors.Contains(conveyor))
                    return;

                conveyor.OnLoadArrived += Conveyor_OnLoadArrived;
                pickStationConveyors.Add(conveyor);
            }
        }

        private void Conveyor_OnLoadArrived(object sender, LoadArrivedEventArgs e)
        {
            PalletArrivedAtPickStation(sender as IPalletRouteStatus, e._Load);
        }

        public override bool Visible
        {
            get { return base.Visible; }
            set
            {
                base.Visible = value;
                foreach (var dropConveyor in dropConveyors)
                {
                    dropConveyor.Update();
                    dropConveyor.TransportSection.Visible = false;
                    dropConveyor.TransportSection.Route.Motor.Visible = false;
                    dropConveyor.Arrow.Visible = false;
                    dropConveyor.StartLine1.Visible = false;
                    dropConveyor.EndLine1.Visible = false;
                    dropConveyor.StartFixPoint.Visible = false;
                    dropConveyor.StartFixPoint.Enabled = false;
                    dropConveyor.LineReleasePhotocell.Visible = false;
                }
                foreach (var pickConveyor in pickConveyors)
                {
                    pickConveyor.Update();
                    pickConveyor.TransportSection.Visible = false;
                    pickConveyor.TransportSection.Route.Motor.Visible = false;
                    pickConveyor.Arrow.Visible = false;
                    pickConveyor.StartLine1.Visible = false;
                    pickConveyor.EndLine1.Visible = false;
                    pickConveyor.EndFixPoint.Visible = false;
                    pickConveyor.EndFixPoint.Enabled = false;
                    pickConveyor.LineReleasePhotocell.Visible = false;
                }
                holdingConveyor.Visible = false;
            }
        }

        private void DropConveyor_OnRouteStatusChanged(object sender, RouteStatusChangedEventArgs e)
        {
            //When the Conveyor connected to the dropConveyor Status changes then i want to know about it
            //Set the status of the drop station 
            DropStationAvailable = e._available == RouteStatuses.Available;

            //If it goes availabe, then the transfer from the pallet crane is complete and i can start the next job
            if (e._available == RouteStatuses.Available && CurrentHalfCycle != null && CurrentHalfCycle.Cycle == PalletCraneCycle.DropDS)
            {
                PalletCraneTaskComplete(new PalletCraneTaskCompleteEventArgs(CurrentHalfCycle));
            }
        }

        private void HoldingAp_OnEnter(ActionPoint sender, Load load)
        {
            load.Stop();
        }

        private void Scene_OnLoaded()
        {
            if (ControllerProperties == null)
            {
                ControllerProperties = StandardCase.SetMHEControl(palletCraneInfo, this);
            }
        }

        private void UpdatePalletCrane()
        {
            Lift.LocalYaw = (float)Math.PI / 2;
            Lift.LocalPosition = new Vector3(Lift.LocalPosition.X, RackHeight / 2, Lift.LocalPosition.Z);

            foreach (var pickConveyor in pickConveyors)
            {
                pickConveyor.Length = palletCraneInfo.AisleWidth;
                pickConveyor.LocalPosition = new Vector3(pickConveyor.Configuration.DistanceX, pickConveyor.Configuration.LevelHeight, pickConveyor.LocalPosition.Z);
                pickConveyor.Update();
                pickConveyor.PositionAp.Distance = pickConveyor.Length / 2;
                pickConveyor.LineReleasePhotocell.Visible = false;
                pickConveyor.LineReleasePhotocell.sensor.Enabled = false;
            }

            foreach (var dropConveyor in dropConveyors)
            {
                dropConveyor.Length = palletCraneInfo.AisleWidth;
                dropConveyor.LocalPosition = new Vector3(dropConveyor.Configuration.DistanceX, dropConveyor.Configuration.LevelHeight, dropConveyor.LocalPosition.Z);
                dropConveyor.Update();
                dropConveyor.PositionAp.Distance = dropConveyor.Length / 2;
                dropConveyor.LineReleasePhotocell.Visible = false;
                dropConveyor.LineReleasePhotocell.DeviceDistance = 0.01f;
            }

            CreateRackBoxes();
        }

        private void CreateRackBoxes()
        {
            RackHeight = palletCraneInfo.Craneheight - palletCraneInfo.RackOffsetBottom + 0.5f;
            RackLength = palletCraneInfo.RailLength - palletCraneInfo.RackOffsetFront;
            LevelHeight = RackHeight / palletCraneInfo.RackLevels;
            BayLength = RackLength / palletCraneInfo.RackBays;

            foreach (var box in rackingBoxes)
            {
                Remove(box);
                Remove(box as Core.IEntity);
                box.Dispose();
            }
            rackingBoxes.Clear();

            var boxInfo = new DematicBoxInfo()
            {
                thickness = 0.01f,
                length = palletCraneInfo.RailLength - palletCraneInfo.RackOffsetFront,
                width = palletCraneInfo.RackWidth,
                height = RackHeight,
                transparency = palletCraneInfo.RackTransparency,
                boxColor = palletCraneInfo.RackColor
            };

            var rack1 = new DematicBox(boxInfo);
            rackingBoxes.Add(rack1);
            Add(rack1, new Vector3(palletCraneInfo.RackOffsetFront + boxInfo.thickness + (RackLength / 2), palletCraneInfo.RackOffsetBottom, boxInfo.thickness + palletCraneInfo.AisleWidth / 2));

            var rack2 = new DematicBox(boxInfo);
            rackingBoxes.Add(rack2);
            Add(rack2, new Vector3(palletCraneInfo.RackOffsetFront + boxInfo.thickness + (RackLength / 2), palletCraneInfo.RackOffsetBottom, -(boxInfo.width + boxInfo.thickness + palletCraneInfo.AisleWidth / 2)));
        }

        #region PalletCrane Logic
        public bool StartPalletCraneHalfCycle(PalletCraneHalfCycle halfCycle)
        {
            if (CurrentHalfCycle != null) //reject if the crane is running, there is a CurrentHalfCycle
            {
                return false;
            }

            CurrentHalfCycle = halfCycle;
            if (halfCycle.Cycle == PalletCraneCycle.PickPS) //This will pick whatever is at the pick station
            {
                Control.Goto(halfCycle.Height, halfCycle.Length, "GotoPS");
                Control.JobQueue[0].UserData = halfCycle;
                Control.StartCrane();
                return true;
            }

            if (halfCycle.Cycle == PalletCraneCycle.DropDS)
            {
                Control.Goto(halfCycle.Height, halfCycle.Length, "GotoDS");
                Control.JobQueue[0].UserData = halfCycle;
                Control.StartCrane();
                return true;
            }

            if (halfCycle.Cycle == PalletCraneCycle.DropRack)
            {
                Control.Goto(halfCycle.Height, halfCycle.Length, "GotoRack");
                Control.JobQueue[0].UserData = halfCycle;
                Control.DropLoad(DepthTime(halfCycle.Depth), true, "DropRack", halfCycle.Lhd == 1 ? HBWMiniLoadJob.LHDs.LHD1 : HBWMiniLoadJob.LHDs.LHD2);
                Control.JobQueue[1].UserData = halfCycle;
                Control.StartCrane();
                return true;
            }

            if (halfCycle.Cycle == PalletCraneCycle.PickRack)
            {
                float depth = (AisleWidth / 2) + ((RackWidth / (DepthsInRack + 1)) * halfCycle.Depth);
                holdingConveyor.LocalPosition = new Vector3(halfCycle.Length + (LHDWidth / 2), halfCycle.Height, halfCycle.RackSide == Side.Left ? depth : -depth);

                Control.Goto(halfCycle.Height, halfCycle.Length, "GotoRack");
                Control.JobQueue[0].UserData = halfCycle;

                var load = CreateLoad(halfCycle.PalletData);

                Control.PickLoad(DepthTime(halfCycle.Depth), new List<Load>() { load }, true, "PickRack", halfCycle.Lhd == 1 ? HBWMiniLoadJob.LHDs.LHD1 : HBWMiniLoadJob.LHDs.LHD2);
                Control.JobQueue[1].UserData = halfCycle;
                Control.StartCrane();
                return true;
            }

            return false;
        }

        private float DepthTime(int depth)
        {
            switch (depth)
            {
                case 1: return TimeToDepth1;
                case 2: return TimeToDepth2;
                case 3: return TimeToDepth3;
            }
            return 10;
        }

        private void Control_FinishedJob(HBWMiniLoad hbw, HBWMiniLoadJob job)
        {
            if (job.JobType == HBWMiniLoadJob.JobTypes.Goto) { }

            switch (job.ID)
            {
                case "GotoPS":
                    var halfCycle = job.UserData as PalletCraneHalfCycle;
                    var pickConveyor = pickConveyors.FirstOrDefault(p => p.Configuration.Name == halfCycle.StationName);
                    if (pickConveyor == null)
                    {
                        Core.Environment.Log.Write(Name + " Error in GotoPS job: No pick station found: " + halfCycle.StationName);
                        Core.Environment.Scene.Pause();
                        return;
                    }
                    pickConveyor.RouteAvailable = RouteStatuses.Available;
                    break;
                case "GotoDS":
                    DropDropStation((PalletCraneHalfCycle)job.UserData);
                    break;
                case "PickPS":
                case "DropRack":
                case "PickRack":
                    if (control.JobQueue.Count == 0 && CurrentHalfCycle != null) //Load has been picked or dropped
                    {
                        PalletCraneTaskComplete(new PalletCraneTaskCompleteEventArgs(CurrentHalfCycle));
                    }
                    break;
            }
        }

        private void DropDropStation(PalletCraneHalfCycle halfCycle)
        {
            if (halfCycle.TuIdent != null)
            {
                Control.DropLoad(0, false, "DropDS", HBWMiniLoadJob.LHDs.LHD1);
                control.JobQueue.Last().UserData = halfCycle;
            }
            Control.StartCrane();
        }

        private void Control_LoadDropped(HBWMiniLoad hbw, Load load, bool rack, string id, int lhd, HBWMiniLoadJob job)
        {
            if (job.Rack)
                return;
            if (lhd == 1)
            {
                var halfCycle = job.UserData as PalletCraneHalfCycle;
                var dropConveyor = dropConveyors.FirstOrDefault(d => d.Configuration.Name == halfCycle.StationName);
                if (dropConveyor == null)
                {
                    Core.Environment.Log.Write(Name + " error in Control_LoadDropped: No drop station found: " + halfCycle.StationName);
                    Core.Environment.Scene.Pause();
                    return;
                }
                load.Switch(dropConveyor.PositionAp, true);
                PalletMovingToDropStation(load, dropConveyor.Configuration.Name);
            }
            load.Movable = true;
            load.UserDeletable = true;
            craneLoads.Remove(load);
        }

        private void PosPd_OnEnter(ActionPoint sender, Load load)
        {
            load.Stop();
            var pickConveyor = sender.Parent.Parent.Parent as BaseStraight;
            LoadArrivedAtPickStation(pickConveyor, load);
        }

        private void LoadArrivedAtPickStation(BaseStraight pickConveyor, Load load)
        {
            //Allow the next loads to transfer into the PS
            pickConveyor.RouteAvailable = RouteStatuses.Blocked;

            //Switch the loads onto the LHD
            if (load != null)
            {
                Control.PickLoad(0, new List<Load> { load }, false, "PickPS", HBWMiniLoadJob.LHDs.LHD1);
            }

            Control.StartCrane();
        }
        #endregion

        #region Pick Station Methods

        public void GetPickStationLocation(string pickStationName, out float x, out float y)
        {
            var pickConveyor = pickConveyors.FirstOrDefault(p => p.Configuration.Name == pickStationName);
            if (pickConveyor == null)
            {
                Core.Environment.Log.Write(Name + " error in GetPickStationLocation: No pick station found: " + pickStationName);
                Core.Environment.Scene.Pause();
                x = 0;
                y = 0;
                return;
            }
            x = pickConveyor.LocalPosition.X;
            y = pickConveyor.LocalPosition.Y;
        }

        [Browsable(false)]
        public void GetDropStationLocation(string dropStationName, out float x, out float y)
        {
            var dropConveyor = dropConveyors.FirstOrDefault(p => p.Configuration.Name == dropStationName);
            if (dropConveyor == null)
            {
                Core.Environment.Log.Write(Name + " error in GetDropStationLocation: No drop station found: " + dropStationName);
                Core.Environment.Scene.Pause();
                x = 0;
                y = 0;
                return;
            }
            x = dropConveyor.LocalPosition.X;
            y = dropConveyor.LocalPosition.Y;
        }

        #endregion

        #region Helper Methods
        private Load CreateLoad(BasePalletData loadData)
        {
            var load = Controller.CreateEuroPallet(loadData);
            load.UserDeletable = false;
            load.Movable = false;
            Load.Items.Add(load);
            load.Switch(holdingAp);
            load.Yaw = (float)Math.PI / 2;
            craneLoads.Add(load);

            return load;
        }

        public float CalculateHeightFromYLoc(string yLoc)
        {
            int result;
            int.TryParse(yLoc, out result);
            return CalculateHeightFromYLoc(result);
        }

        public float CalculateHeightFromYLoc(int yLoc)
        {
            if (yLoc > RackLevels || yLoc == 0)
            {
                Log.Write($"PalletCrane {Name}: Cannot send to Y Location {yLoc}, not enough levels or sent to position 0, crane will travel to height 0");
                return 0;
            }

            var pitch = RackHeight / RackLevels;
            return (pitch * (yLoc - 1)) + RackOffsetBottom;
        }

        public float CalculateLengthFromXLoc(string xLoc)
        {
            int result;
            int.TryParse(xLoc, out result);
            return CalculateLengthFromXLoc(result);
        }

        public float CalculateLengthFromXLoc(string xLoc, string raster, string position)
        {
            int resultX, resultR, resultP;
            int.TryParse(xLoc, out resultX);
            int.TryParse(raster, out resultR);
            int.TryParse(position, out resultP);

            if (resultX == 0 || resultR == 0 || resultP == 0)
            {
                return CalculateLengthFromXLoc(0);
            }

            return CalculateLengthFromXLoc(((resultX - 1) * resultR) + resultP);
        }

        public float CalculateLengthFromXLoc(int xLoc)
        {
            if (xLoc > RackBays || xLoc == 0)
            {
                Log.Write($"PalletCrane {Name}: Cannot send to X Location {xLoc}, not enough bays or sent to position 0, crane will travel to length 0");
                return 0;
            }
            var pitch = RackLength / RackBays;
            return (pitch * (xLoc - 1)) + RackOffsetFront;
        }

        public Load PickStationPallet(string pickStationName)
        {
            var pickConveyor = pickConveyors.FirstOrDefault(p => p.Configuration.Name == pickStationName);
            if (pickConveyor == null)
            {
                Core.Environment.Log.Write(Name + " error in PickStationPallet: No pick station found: " + pickStationName);
                Core.Environment.Scene.Pause();
                return null;
            }
            return pickConveyor.PreviousConveyor?.GetLoadWaitingStatus(pickConveyor.StartFixPoint.Attached)?.WaitingLoad;
        }

        #endregion

        public override string Category => "Dematic Pallet Crane";

        public override Image Image => Common.Icons.Get("DematicHBWMiniLoad");

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
            foreach (var c in craneLoads)
            {
                c.UserDeletable = true;
                c.Deletable = true;
                c.Dispose();
                c.Route?.Remove(c);
            }

            craneLoads.Clear();

            compartmentempty = false;
            compartmentoccupied = false;

            base.Reset();
            Core.Environment.Invoke(() =>
            {
                Core.Environment.Invoke(() =>
                {
                    foreach (var pickConveyor in pickConveyors)
                    {
                        pickConveyor.RouteAvailable = RouteStatuses.Blocked;
                        pickConveyor.ThisRouteStatus.Available = RouteStatuses.Blocked;
                    }
                });
            });

            CurrentHalfCycle = null;
            PalletCraneReset(new EventArgs());
        }

        public override void Dispose()
        {
            foreach (var conveyor in pickStationConveyors)
            {
                conveyor.OnLoadArrived -= Conveyor_OnLoadArrived;
            }
            pickStationConveyors.Clear();
            holdingAp.OnEnter -= HoldingAp_OnEnter;
            foreach (var pickConveyor in pickConveyors)
            {
                pickConveyor.StartFixPoint.OnSnapped -= PickConveyorStartFixPoint_OnSnapped;
                pickConveyor.StartFixPoint.OnUnSnapped -= PickConveyorStartFixPoint_OnUnSnapped;
                pickConveyor.PositionAp.OnEnter -= PosPd_OnEnter;
                pickConveyor.Dispose();
            }
            Control.FinishedJob -= Control_FinishedJob;
            Control.LoadDropped -= Control_LoadDropped;
            foreach (var dropConveyor in dropConveyors)
            {
                dropConveyor.ThisRouteStatus.OnRouteStatusChanged -= DropConveyor_OnRouteStatusChanged;
                dropConveyor.Dispose();
            }
            Core.Environment.Scene.OnLoaded -= Scene_OnLoaded;
            subMenu[0].Click -= DematicHBW_ClickReset;
            //subMenu[1].Click -= new EventHandler(DematicHBW_ClickCompartmentOccupied);
            //subMenu[2].Click -= new EventHandler(DematicHBW_ClickCompartmentEmpty);
            //subMenu[3].Click -= new EventHandler(DematicHBW_ClickFault);
            subMenu.Clear();
            craneLoads.Clear();
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
            var dateTime = DateTime.Now.ToString(CultureInfo.InvariantCulture);
            Core.Environment.Log.Write(dateTime + ": " + Name + " | " + text, color);
        }

        [Browsable(false)]
        public override Core.Assemblies.EventCollection Events => base.Events;

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

        [Category("Pick and Drop Stations")]
        [PropertyOrder(2)]
        [Description("Width of aisle and LHD")]
        [DisplayName(@"Aisle Width")]
        public float AisleWidth
        {
            get { return palletCraneInfo.AisleWidth; }
            set
            {
                if (palletCraneInfo.AisleWidth != value)
                {
                    palletCraneInfo.AisleWidth = value;
                }
                Core.Environment.Invoke(UpdatePalletCrane);
            }
        }

        [Category("Racking")]
        [PropertyOrder(3)]
        [Description("The distance from the rail begins until the rack begins")]
        [DisplayName(@"Rack offset, front (m.)")]
        public float RackOffsetFront
        {
            get { return palletCraneInfo.RackOffsetFront; }
            set
            {
                if (palletCraneInfo.RackOffsetFront != value)
                {
                    if ((value + 1) > Raillength)
                    {
                        Log.Write($"PalletCrane {Name}: Rack offset front set too high");
                        return;
                    }

                    palletCraneInfo.RackOffsetFront = value;
                    Core.Environment.Invoke(UpdatePalletCrane);
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

        [Category("Racking")]
        [PropertyOrder(5)]
        [Description("The distance from the bottom of the rail to bottom of the rack")]
        [DisplayName(@"Rack offset, bottom (m.)")]
        public float RackOffsetBottom
        {
            get { return palletCraneInfo.RackOffsetBottom; }
            set
            {
                palletCraneInfo.RackOffsetBottom = value;
                Core.Environment.Invoke(UpdatePalletCrane);
            }
        }

        [Category("Racking")]
        [PropertyOrder(6)]
        [Description("The number of bays in rack (presumably evenly distributed over the length of the rack)")]
        [DisplayName(@"Bays in rack")]
        public int RackBays
        {
            get { return palletCraneInfo.RackBays; }
            set
            {
                if (palletCraneInfo.RackBays != value)
                {
                    if (value < 1)
                    {
                        WriteTextToLog("User tried to define an illegal number of bays (" + value + "). Setting the value back to the previous one (" + palletCraneInfo.RackBays + ")", Color.Red);
                        value = palletCraneInfo.RackBays;
                    }
                    palletCraneInfo.RackBays = value;
                    BayLength = RackLength / palletCraneInfo.RackBays;
                }
            }
        }

        [Category("Racking")]
        [PropertyOrder(8)]
        [Description("The width of each rack on both sides of the crane")]
        [DisplayName(@"Rack width (m.)")]
        public float RackWidth
        {
            get { return palletCraneInfo.RackWidth; }
            set
            {
                palletCraneInfo.RackWidth = value;
                CreateRackBoxes();
            }
        }

        [Category("Racking")]
        [PropertyOrder(9)]
        [Description("The number of levels in rack (presumably evenly distributed over the height of the rack)")]
        [DisplayName(@"Levels in rack")]
        public int RackLevels
        {
            get { return palletCraneInfo.RackLevels; }
            set
            {
                if (palletCraneInfo.RackLevels != value)
                {
                    if (value < 1)
                    {
                        WriteTextToLog("User tried to define an illegal number of levels (" + value + "). Setting the value back to the previous one (" + palletCraneInfo.RackLevels + ")", Color.Red);
                        value = palletCraneInfo.RackLevels;
                    }
                    palletCraneInfo.RackLevels = value;
                    LevelHeight = RackHeight / palletCraneInfo.RackLevels;
                }
            }
        }

        [Category("Racking")]
        [PropertyOrder(10)]
        [Description("Color of the rack")]
        [DisplayName(@"Racking Color")]
        public Color RackColor
        {
            get { return palletCraneInfo.RackColor; }
            set
            {
                palletCraneInfo.RackColor = value;
                CreateRackBoxes();
            }
        }

        [Category("Racking")]
        [PropertyOrder(11)]
        [Description("Set the transparency of the racking")]
        [DisplayName(@"Racking Transparency")]
        public int RackTransparency
        {
            get { return palletCraneInfo.RackTransparency; }
            set
            {
                if (value > 255) { value = 255; }
                else if (value < 0) { value = 0; }
                palletCraneInfo.RackTransparency = value;
                CreateRackBoxes();
            }
        }

        [Category("LHD Times")]
        [DisplayName(@"Depths in Rack")]
        [Description("Number of depths in the racking to configure times for")]
        public int DepthsInRack
        {
            get { return palletCraneInfo.DepthsInRack; }
            set
            {
                if (value > 0 && value < 4)
                {
                    palletCraneInfo.DepthsInRack = value;
                    Core.Environment.Properties.Refresh();
                }
            }
        }

        [Category("LHD Times")]
        [DisplayName(@"Time to depth 1 (s)")]
        [Description("How long it takes to load or unload from depth 1")]
        public float TimeToDepth1
        {
            get { return palletCraneInfo.TimeToDepth1; }
            set
            {
                palletCraneInfo.TimeToDepth1 = value;
            }
        }

        [Category("LHD Times")]
        [DisplayName(@"Time to depth 2 (s)")]
        [Description("How long it takes to load or unload from depth 2")]
        [PropertyAttributesProvider("DynamicPropertyShowDepth2")]
        public float TimeToDepth2
        {
            get { return palletCraneInfo.TimeToDepth2; }
            set
            {
                palletCraneInfo.TimeToDepth2 = value;
            }
        }

        [Category("LHD Times")]
        [DisplayName(@"Time to depth 3 (s)")]
        [Description("How long it takes to load or unload from depth 3")]
        [PropertyAttributesProvider("DynamicPropertyShowDepth3")]
        public float TimeToDepth3
        {
            get { return palletCraneInfo.TimeToDepth3; }
            set
            {
                palletCraneInfo.TimeToDepth3 = value;
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
                {   //If the PLC is deleted then any Conveyor referencing the PLC will need to remove references to the deleted PLC.
                    value.OnControllerDeletedEvent += Controller_OnControllerDeletedEvent;
                    value.OnControllerRenamedEvent += Controller_OnControllerRenamedEvent;
                }
                else if (controller != null)
                {
                    controller.OnControllerDeletedEvent -= Controller_OnControllerDeletedEvent;
                    controller.OnControllerRenamedEvent -= Controller_OnControllerRenamedEvent;
                }
                controller = value;
                Core.Environment.Properties.Refresh();
            }
        }

        private void Controller_OnControllerRenamedEvent(object sender, EventArgs e)
        {
            palletCraneInfo.ControllerName = ((Assembly)sender).Name;
            Core.Environment.Properties.Refresh();
        }

        private void Controller_OnControllerDeletedEvent(object sender, EventArgs e)
        {
            controller.OnControllerDeletedEvent -= Controller_OnControllerDeletedEvent;
            ControllerName = "No Controller";
            Controller = null;
            ControllerProperties = null;
        }

        [Category("Configuration")]
        [DisplayName(@"Controller")]
        [Description("Controller name that handles this crane")]
        [TypeConverter(typeof(PalletControllerConverter))] 
        public string ControllerName
        {
            get
            {
                return palletCraneInfo.ControllerName;
            }
            set
            {
                if (!value.Equals(palletCraneInfo.ControllerName))
                {
                    ControllerProperties = null;
                    palletCraneInfo.ProtocolInfo = null;
                    Controller = null;
                }

                palletCraneInfo.ControllerName = value;
                if (value != null)
                {
                    ControllerProperties = StandardCase.SetMHEControl(palletCraneInfo, this);

                    if (ControllerProperties == null)
                    {
                        palletCraneInfo.ControllerName = "No Controller";
                    }
                }
            }
        }

        /// <summary>
        /// Generic property for a PLC of any type, DatCom, DCI etc it is set when the ControllerName is set
        /// </summary>
        [Category("Configuration")]
        [DisplayName(@"Controller Setup")]
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
                Core.Environment.Properties.Refresh();
            }
        }

        [Category("Configuration")]
        [DisplayName(@"Pick Stations")]
        [TypeConverter(typeof(ExpandableObjectConverter))]
        public ExpandablePropertyList<StationConfiguration> PickStations
        {
            get { return palletCraneInfo.PickStations; }
            set { palletCraneInfo.PickStations = value; }
        }

        [Category("Configuration")]
        [DisplayName(@"Drop Stations")]
        [TypeConverter(typeof(ExpandableObjectConverter))]
        public ExpandablePropertyList<StationConfiguration> DropStations
        {
            get { return palletCraneInfo.DropStations; }
            set { palletCraneInfo.DropStations = value; }
        }

        public void DynamicPropertyAssemblyPLCconfig(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = Controller != null;
        }
        #endregion

        #region Interface to controller object
        [Browsable(false)]
        public HashSet<Load> CraneLoads
        {
            get { return craneLoads; }
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
                OnPalletCraneDropStationAvailableChanged?.Invoke(this, new PalletCraneDropStationAvailableChangedEventArgs(value));
            }
        }

        #endregion
    }

    public enum PalletCraneCycle
    {
        PickRack,
        DropRack,
        PickPS,
        DropDS,
        None
    }

    public enum PalletCraneTaskType
    {
        Storage,    //pick load(s) from pick station and store in the racking
        Retrieval,  //pick load(s) from racking and drop at the drop station
        Relocation, //pick load(s) from racking and store at another location in racking
        Reject,     //pick load(s) from pick station and drop at the drop station
    }

    public class PalletCraneHalfCycle
    {
        public float Height;
        public float Length;
        public int Depth;
        public Side? RackSide;
        public int Lhd;
        public PalletCraneCycle Cycle;
        public string TuIdent;
        public BasePalletData PalletData;
        public string StationName;
    }

    public class PalletCraneTask
    {
        public List<object> MissionData { get; private set; }
        public PalletCraneTaskType TaskType { get; set; }
        public List<PalletCraneHalfCycle> HalfCycles { get; private set; }

        public PalletCraneTask(List<object> missionData)
        {
            MissionData = missionData;
            HalfCycles = new List<PalletCraneHalfCycle>();
        }
    }

    public class PalletStationEventArgs : EventArgs
    {
        public readonly IRouteStatus Status;
        public readonly Load Load;
        public readonly string PickStationName;
        public PalletStationEventArgs(IRouteStatus status, Load load, string pickStation)
        {
            Status = status;
            Load = load;
            PickStationName = pickStation;
        }
    }

    public class DropStationEventArgs : EventArgs
    {
        public readonly Load Load;
        public readonly string DropStationName;
        public DropStationEventArgs(Load load, string dropStation)
        {
            Load = load;
            DropStationName = dropStation;
        }
    }

    public class PalletCraneTaskCompleteEventArgs : EventArgs
    {
        public readonly PalletCraneHalfCycle PalletCraneTask;
        public PalletCraneTaskCompleteEventArgs(PalletCraneHalfCycle task)
        {
            PalletCraneTask = task;
        }
    }

    public class PalletCraneDropStationAvailableChangedEventArgs : EventArgs
    {
        public readonly bool Available;
        public PalletCraneDropStationAvailableChangedEventArgs(bool available)
        {
            Available = available;
        }
    }
}
