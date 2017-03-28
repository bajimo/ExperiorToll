using System;
using System.Drawing;
using System.Linq;
using System.Xml.Serialization;
using Experior.Dematic.Base;
using Experior.Dematic.Base.Devices;
using Experior.Core.Parts;
using Microsoft.DirectX;
using Experior.Core;
using System.ComponentModel;
using Experior.Catalog.Logistic.Track;

namespace Experior.Catalog.Dematic.Pallet.Assemblies
{
    public class Destacker : PalletStraight
    {
        private DestackerInfo destackerInfo;
        private bool loadActive = false;
        private LoadWaitingStatus previousLoadWaitingStatus;
        private Cube trackLeft;
        private Cube trackRight;
        private Straight stackConveyor;
        private EuroPallet stackedLoad;
        private EuroPallet currentLoad;
        private static Experior.Core.Timer releaseLoadTimer = new Experior.Core.Timer(2);
        private static Experior.Core.Timer repositionLoadsTimer = new Experior.Core.Timer(2);

        public Destacker(DestackerInfo info) : base(info)
        {
            destackerInfo = info;
            DrawTracks();

            // Conveyor for stacking
            StraightInfo straightInfo = new StraightInfo
            {
                thickness = destackerInfo.thickness,
                speed = destackerInfo.speed,
                width = destackerInfo.width,
                height = destackerInfo.height,
                length = destackerInfo.length,
            };
            stackConveyor = new Straight(straightInfo);
            stackConveyor.Color = Color.Violet;
            Add(stackConveyor);
            stackConveyor.Visible = false;
            stackConveyor.StartFixPoint.Dispose();
            stackConveyor.EndFixPoint.Dispose();
            stackConveyor.TransportSection.Route.Motor.Stop();
            stackConveyor.LocalPosition = new Vector3(0, PalletHeight + 0.005f, 0);

            // Initialise as request
            ThisRouteStatus.Available = RouteStatuses.Request;
        }

        #region Private Methods

        private void DrawTracks()
        {
            if (trackLeft != null)
            {
                Remove(trackLeft);
            }
            if (trackRight != null)
            {
                Remove(trackRight);
            }
            var stackerHeight = destackerInfo.StackLimit * destackerInfo.PalletHeight + 0.5f; // (Max stacks * pallet height + little extra)
            var offCentre = Width / 2 + 0.05f;
            var distance = ((Length - destackerInfo.PalletLength) + (destackerInfo.PalletLength / 4)) / 2;
            // Left track (visual only)
            trackLeft = new Cube(Color.Gray, stackerHeight, 0.05f, 0.05f);
            Add(trackLeft);
            trackLeft.LocalPosition = new Vector3(-distance, stackerHeight / 2, offCentre);
            trackLeft.LocalRoll = -(float)Math.PI / 2.0f;
            // Right track (visual only)
            trackRight = new Cube(Color.Gray, stackerHeight, 0.05f, 0.05f);
            Add(trackRight);
            trackRight.LocalPosition = new Vector3(-distance, stackerHeight / 2, -offCentre);
            trackRight.LocalRoll = -(float)Math.PI / 2.0f;
        }

        private void DestackLoads()
        {
            // Check whether the load entering is loaded and if so allow it to pass through
            currentLoad = GetCurrentLoad();
            currentLoad.UserDeletable = true;
            if (currentLoad.Status != PalletStatus.Loaded)
            {
                var currentStackedLoads = currentLoad.Grouped.Items;
                currentLoad.DetachStackedPallets();
                currentLoad.Status = PalletStatus.Empty;
                stackedLoad = GetStackedLoad();
                if (stackedLoad == null)
                {
                    // Take first from stack and add to stacker conveyor
                    if (currentStackedLoads.Count > 0)
                    {
                        currentStackedLoads[0].Switch(stackConveyor.TransportSection.Route, currentLoad.Distance, true);
                        stackedLoad = GetStackedLoad();
                        // Then group the rest with the load just added
                        for (int i = 1; i < currentStackedLoads.Count; i++)
                        {
                            currentStackedLoads[i].Switch(stackConveyor.TransportSection.Route, currentLoad.Distance, true);
                            var stackY = (PalletHeight + 0.005f) * i;
                            stackedLoad.Group(currentStackedLoads[i], new Vector3(0, stackY, 0));
                        }
                    }
                }
            }
            //// Add short delay before releasing first load from the stack
            releaseLoadTimer.OnElapsed += ReleaseLoad_OnElapsed;
            releaseLoadTimer.Start();
        }

        private void RepositionLoads_OnElapsed(Timer sender)
        {
            repositionLoadsTimer.OnElapsed -= RepositionLoads_OnElapsed;
            // Now switch the stacked loads back on to the main conveyor
            if (stackedLoad != null)
            {
                stackedLoad.Switch(TransportSection.Route, stackedLoad.Distance - 0.005f, true);
            }
            ThisRouteStatus.Available = RouteStatuses.Request;
        }

        private void ReleaseLoad_OnElapsed(Timer sender)
        {
            releaseLoadTimer.OnElapsed -= ReleaseLoad_OnElapsed;
            ResetConveyorLoads(this, false);
            if (NextRouteStatus != null && NextRouteStatus.Available == RouteStatuses.Available)
            {
                if (ThisRouteStatus.Available != RouteStatuses.Available)
                {
                    ThisRouteStatus.Available = RouteStatuses.Blocked;
                    ThisRouteStatus.Available = RouteStatuses.Request;
                }
            }
            else
            {
                ThisRouteStatus.Available = RouteStatuses.Blocked;
            }
        }

        private EuroPallet GetCurrentLoad()
        {
            EuroPallet pallet = null;
            if (TransportSection.Route.Loads.Count > 0)
            {
                pallet = (EuroPallet)TransportSection.Route.Loads.First();
            }
            return pallet;
        }

        private EuroPallet GetStackedLoad()
        {
            EuroPallet pallet = null;
            if (stackConveyor.TransportSection.Route.Loads.Count > 0)
            {
                pallet = (EuroPallet)stackConveyor.TransportSection.Route.Loads.First();
            }
            return pallet;
        }

        private void PreviousLoadWaitingStatus_OnLoadWaitingChanged(object sender, LoadWaitingChangedEventArgs e)
        {
            //If a load arrives and there is nothing else in destacker make it available
            if (e._loadWaiting)
            {
                bool hasExistingStack = false;
                if (TransportSection.Route.Loads.Count > 0)
                {
                    hasExistingStack = TransportSection.Route.Loads.First().Grouped.Items.Count > 0;
                }
                if (ThisRouteStatus != null && ThisRouteStatus.Available == RouteStatuses.Request && !loadActive
                    && !hasExistingStack)
                {
                    ThisRouteStatus.Available = RouteStatuses.Available;
                }
            }
            if (!e._loadWaiting)
            {
                //Once the load has stopped waiting, then the load will become active in the destacker
                if (ThisRouteStatus.Available == RouteStatuses.Available)
                {
                    ThisRouteStatus.Available = RouteStatuses.Request;

                    if (!e._loadDeleted)
                        loadActive = true;
                    else
                    {
                        loadActive = false;
                    }
                }
            }
        }

        private void ResetConveyorLoads(Straight conveyor, bool delete)
        {
            if (conveyor.TransportSection.Route.Loads.Count > 0)
            {
                foreach (var routeLoad in conveyor.TransportSection.Route.Loads)
                {
                    foreach (var load in routeLoad.Grouped.Items)
                    {
                        load.UserDeletable = true;
                        if (delete)
                        {
                            load.Dispose();
                        }
                    }
                    routeLoad.UserDeletable = true;
                    if (delete)
                    {
                        routeLoad.Dispose();
                    }
                }
            }
        }

        #endregion

        #region Public Override Methods

        public override void StartFixPoint_OnSnapped(Core.Parts.FixPoint stranger, Core.Parts.FixPoint.SnapEventArgs e)
        {
            PreviousConveyor = stranger.Parent as IRouteStatus;
            previousLoadWaitingStatus = PreviousConveyor.GetLoadWaitingStatus(stranger);
            if (previousLoadWaitingStatus != null)
            {
                previousLoadWaitingStatus.OnLoadWaitingChanged += PreviousLoadWaitingStatus_OnLoadWaitingChanged;
            }
        }

        public override void StartFixPoint_OnUnSnapped(Core.Parts.FixPoint stranger)
        {
            if (previousLoadWaitingStatus != null)
            {
                previousLoadWaitingStatus.OnLoadWaitingChanged -= PreviousLoadWaitingStatus_OnLoadWaitingChanged;
                previousLoadWaitingStatus = null;
                Reset();
            }
        }

        public override void LineReleasePhotocell_OnPhotocellStatusChanged(object sender, PhotocellStatusChangedEventArgs e)
        {
            if (e._PhotocellStatus == PhotocellState.Blocked)
            {
                e._Load.UserDeletable = false;
                ThisRouteStatus.Available = RouteStatuses.Blocked;
                DestackLoads();
                SetLoadWaiting(true, false, e._Load);
            }
            else if (e._PhotocellStatus == PhotocellState.Clear)
            {
                if (e._Load != null)
                {
                    e._Load.UserDeletable = true;
                }
                stackedLoad = GetStackedLoad();
                if (stackedLoad != null)
                {
                    // Add short delay before making next available load available
                    ThisRouteStatus.Available = RouteStatuses.Blocked;
                    repositionLoadsTimer.OnElapsed += RepositionLoads_OnElapsed;
                    repositionLoadsTimer.Start();
                }
                else
                {
                    SetLoadWaiting(false, false, e._Load);
                    ThisRouteStatus.Available = RouteStatuses.Available;
                }
            }
        }
                
        public override void UpdateLength()
        {
            base.UpdateLength();
            stackConveyor.Length = Length;
            stackConveyor.LocalPosition = new Vector3(0, PalletHeight + 0.005f, 0);
            DrawTracks();
            Core.Environment.Properties.Refresh();
        }

        public override void Reset()
        {
            base.Reset();
            releaseLoadTimer.Reset();
            repositionLoadsTimer.Reset();
            ResetConveyorLoads(stackConveyor, true);
            ResetConveyorLoads(this, true);
            loadActive = false;
            ThisRouteStatus.Available = RouteStatuses.Request;
            previousLoadWaitingStatus = null;
            currentLoad = null;
            stackedLoad = null;
        }

        public override void Dispose()
        {
            releaseLoadTimer.Dispose();
            repositionLoadsTimer.Dispose();
            stackConveyor.Dispose();
            trackLeft.Dispose();
            trackRight.Dispose();
            base.Dispose();
        }

        #endregion

        #region Public Properties

        [Category("Destacker Configuration")]
        [DisplayName(@"Pallet Length")]
        public float PalletLength
        {
            get { return destackerInfo.PalletLength; }
            set { destackerInfo.PalletLength = value; }
        }

        [Category("Stacker Configuration")]
        [DisplayName(@"Pallet Height")]
        [ReadOnly(true)]
        public float PalletHeight
        {
            get { return destackerInfo.PalletHeight; }
        }

        [Category("Destacker Configuration")]
        [DisplayName(@"Stack Limit")]
        public int StackLimit
        {
            get { return destackerInfo.StackLimit; }
            set
            {
                destackerInfo.StackLimit = value;
                Core.Environment.Invoke(() => DrawTracks());
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

        #region Implement Assembly Properties

        public override string Category
        {
            get { return "Pallet Destacker"; }
        }

        public override Image Image
        {
            get { return Common.Icons.Get("Destacker"); }
        }

        #endregion

    }

    [Serializable]
    [XmlInclude(typeof(DestackerInfo))]
    public class DestackerInfo : PalletStraightInfo
    {
        public int StackLimit;
        public float PalletHeight;
        public float PalletLength;
    }

}
