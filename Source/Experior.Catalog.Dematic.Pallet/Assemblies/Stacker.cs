using Experior.Dematic.Base;
using System.Drawing;
using Experior.Dematic.Base.Devices;
using System;
using System.Linq;
using Experior.Core.Parts;
using Microsoft.DirectX;
using System.Xml.Serialization;
using System.ComponentModel;
using Experior.Core;
using Experior.Catalog.Logistic.Track;

namespace Experior.Catalog.Dematic.Pallet.Assemblies
{
    public class Stacker : PalletStraight
    {
        private StackerInfo stackerInfo;
        private bool loadActive = false;
        private LoadWaitingStatus previousLoadWaitingStatus;
        private int loadStacks;
        private Cube trackLeft;
        private Cube trackRight;
        private Straight stackConveyor;
        private EuroPallet stackedLoad;
        private EuroPallet currentLoad;
        private static Experior.Core.Timer releaseLoadDelayTimer = new Experior.Core.Timer(2);
        private static Experior.Core.Timer stackLoadsDelayTimer = new Experior.Core.Timer(2);

        public Stacker(StackerInfo info) : base(info)
        {
            stackerInfo = info;
            DrawTracks();

            // Conveyor for stacking
            StraightInfo straightInfo = new StraightInfo
            {
                thickness = stackerInfo.thickness,
                speed = stackerInfo.speed,
                width = stackerInfo.width,
                height = stackerInfo.height,
                length = stackerInfo.length,
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
            var stackerHeight = stackerInfo.StackLimit * stackerInfo.PalletHeight + 0.5f; // (Max stacks * pallet height + little extra)
            var offCentre = Width / 2 + 0.05f;
            var distance = ((Length - stackerInfo.PalletLength) + (stackerInfo.PalletLength / 4)) / 2;
            // Left track (visual only)
            trackLeft = new Cube(Color.Gray, stackerHeight, 0.05f, 0.05f);
            Add(trackLeft);
            trackLeft.LocalPosition = new Vector3(-distance, stackerHeight / 2.0f, offCentre);
            trackLeft.LocalRoll = -(float)Math.PI / 2.0f;
            // Right track (visual only)
            trackRight = new Cube(Color.Gray, stackerHeight, 0.05f, 0.05f);
            Add(trackRight);
            trackRight.LocalPosition = new Vector3(-distance, stackerHeight / 2.0f, -offCentre);
            trackRight.LocalRoll = -(float)Math.PI / 2.0f;
        }

        private void StackLoads()
        {
            loadStacks++;
            stackedLoad = GetStackedLoad();
            currentLoad = (EuroPallet)TransportSection.Route.Loads.First();
            if (stackedLoad != null)
            {
                loadStacks = stackedLoad.Grouped.Items.Count + 2;
            }
            // Check whether the load entering is already a stack or loaded
            // If so allow it to pass through
            if (currentLoad.Status == PalletStatus.Stacked || currentLoad.Status == PalletStatus.Loaded)
            {
                loadStacks = stackerInfo.StackLimit;
            }
            if (loadStacks < stackerInfo.StackLimit)
            {
                // Add short delay before switching the load from the current conveyor into the stack conveyor
                stackLoadsDelayTimer.OnElapsed += MoveLoadToStack_OnElapsed;
                stackLoadsDelayTimer.Start();
            }
            else
            {
                MoveLoadBackToConveyor();
            }
        }

        private void MoveLoadToStack_OnElapsed(Timer sender)
        {
            stackLoadsDelayTimer.OnElapsed -= MoveLoadToStack_OnElapsed;
            if (currentLoad != null)
            {
                if (stackedLoad != null)
                {
                    currentLoad.Switch(stackConveyor.TransportSection.Route, currentLoad.Distance, true);
                    var stackY = (PalletHeight + 0.005f) * (stackedLoad.Grouped.Items.Count + 1); // create empty space between loads     
                    stackedLoad.Group(currentLoad, new Vector3(0, stackY, 0));
                }
                else
                {
                    currentLoad.Switch(stackConveyor.TransportSection.Route, currentLoad.Distance, true);
                }
            }
        }

        private void MoveLoadBackToConveyor()
        {
            currentLoad = GetCurrentLoad();
            stackedLoad = GetStackedLoad();
            if (stackedLoad != null)
            {
                var stackedLoads = stackedLoad.Grouped.Items;
                var stackedLoadsCount = stackedLoads.Count;
                stackedLoad.UnGroup();
                // First move the stacked load itself
                stackedLoad.Switch(TransportSection.Route, stackedLoad.Distance - 0.001f, true);
                if (currentLoad != null)
                {
                    currentLoad.Group(stackedLoad, new Vector3(0, PalletHeight + 0.005f, 0));
                }
                // Second move all the loads that were previously grouped to it
                for (int i = 0; i < stackedLoadsCount; i++)
                {
                    stackedLoads[i].Switch(TransportSection.Route, true);
                    if (currentLoad != null)
                    {
                        currentLoad.Group(stackedLoads[i], new Vector3(0, (PalletHeight + 0.005f) * (i + 2), 0));
                    }
                    else
                    {
                        stackedLoad.Group(stackedLoads[i], new Vector3(0, (PalletHeight + 0.005f) * (i + 1), 0));
                    }
                }
            }
            // Add short delay before releasing the load
            releaseLoadDelayTimer.OnElapsed += ReleaseLoad_OnElapsed;
            releaseLoadDelayTimer.Start();
        }

        private void ReleaseLoad_OnElapsed(Timer sender)
        {
            releaseLoadDelayTimer.OnElapsed -= ReleaseLoad_OnElapsed;
            loadStacks = 0;
            if (NextRouteStatus != null && NextRouteStatus.Available == RouteStatuses.Available) // && ThisRouteStatus.Available != RouteStatuses.Request
            {
                ThisRouteStatus.Available = RouteStatuses.Blocked;
                ThisRouteStatus.Available = RouteStatuses.Request;
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

        private void PreviousLoadWaitingStatusStraight_OnLoadWaitingChanged(object sender, LoadWaitingChangedEventArgs e)
        {
            //If a load arrives and an unexpected type then allow it to pass through
            if (e._loadWaiting)
            {
                EuroPallet previousLoad = (EuroPallet)e._waitingLoad;
                if (previousLoad.Status == PalletStatus.Stacked || previousLoad.Status == PalletStatus.Loaded)
                {
                    // Release the currently stacked load and allow previous load to pass through
                    MoveLoadBackToConveyor();
                }
                if (ThisRouteStatus != null && ThisRouteStatus.Available == RouteStatuses.Request && !loadActive)
                {
                    ThisRouteStatus.Available = RouteStatuses.Available;
                }
            }
            if (!e._loadWaiting)
            {
                //Once the load has stopped waiting, then the load will become active in the transfer
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

        public override void NextRouteStatus_OnAvailableChanged(object sender, RouteStatusChangedEventArgs e)
        {
            if (e._available == RouteStatuses.Available && ThisRouteStatus.Available == RouteStatuses.Blocked)
            {
                if (!stackLoadsDelayTimer.Running)
                {
                    ThisRouteStatus.Available = RouteStatuses.Request;
                }
            }
            else if (e._available == RouteStatuses.Blocked && ThisRouteStatus.Available != RouteStatuses.Blocked)
            {
                ThisRouteStatus.Available = RouteStatuses.Available;
            }
        }

        public override void StartFixPoint_OnSnapped(Core.Parts.FixPoint stranger, Core.Parts.FixPoint.SnapEventArgs e)
        {
            IRouteStatus PreviousConveyorStraight = stranger.Parent as IRouteStatus;
            previousLoadWaitingStatus = PreviousConveyorStraight.GetLoadWaitingStatus(stranger);
            if (previousLoadWaitingStatus != null)
            {
                previousLoadWaitingStatus.OnLoadWaitingChanged += PreviousLoadWaitingStatusStraight_OnLoadWaitingChanged;
            }
        }

        public override void StartFixPoint_OnUnSnapped(Core.Parts.FixPoint stranger)
        {
            if (previousLoadWaitingStatus != null)
            {
                previousLoadWaitingStatus.OnLoadWaitingChanged -= PreviousLoadWaitingStatusStraight_OnLoadWaitingChanged;
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
                RouteAvailable = RouteStatuses.Blocked; //Stop the motor
                StackLoads();
            }
            else if (e._PhotocellStatus == PhotocellState.Clear)
            {
                if (e._Load != null)
                {
                    e._Load.UserDeletable = true;
                }
                SetLoadWaiting(false, false, e._Load);
                ThisRouteStatus.Available = RouteStatuses.Available;
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
            releaseLoadDelayTimer.Reset();
            stackLoadsDelayTimer.Reset();
            ResetConveyorLoads(stackConveyor, true);
            ResetConveyorLoads(this, true);
            loadStacks = 0;
            loadActive = false;
            ThisRouteStatus.Available = RouteStatuses.Request;
            currentLoad = null;
            stackedLoad = null;
        }

        public override void Dispose()
        {
            releaseLoadDelayTimer.Dispose();
            stackLoadsDelayTimer.Dispose();
            stackConveyor.Dispose();
            trackLeft.Dispose();
            trackRight.Dispose();
            base.Dispose();
        }

        #endregion

        #region Properties

        [Category("Stacker Configuration")]
        [DisplayName(@"Stack Limit")]
        public int StackLimit
        {
            get { return stackerInfo.StackLimit; }
            set
            {
                stackerInfo.StackLimit = value;
                Core.Environment.Invoke(() => DrawTracks());
            }
        }

        [Category("Stacker Configuration")]
        [DisplayName(@"Pallet Length")]
        public float PalletLength
        {
            get { return stackerInfo.PalletLength; }
            set { stackerInfo.PalletLength = value; }
        }

        [Category("Stacker Configuration")]
        [DisplayName(@"Pallet Height")]
        [ReadOnly(true)]
        public float PalletHeight
        {
            get { return stackerInfo.PalletHeight; }
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
            get { return "Pallet Stacker"; }
        }

        public override Image Image
        {
            get { return Common.Icons.Get("Stacker"); }
        }

        #endregion
    }

    [Serializable]
    [XmlInclude(typeof(StackerInfo))]
    public class StackerInfo : PalletStraightInfo
    {
        public int StackLimit;
        public float PalletHeight;
        public float PalletLength;
    }
}
