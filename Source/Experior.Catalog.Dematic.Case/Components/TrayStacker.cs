using Experior.Dematic.Base;
using System.Drawing;
using Experior.Dematic.Base.Devices;
using System;
using System.Linq;
using Experior.Core.Parts;
using Microsoft.DirectX;
using System.Xml.Serialization;
using System.ComponentModel;
using Experior.Catalog.Logistic.Track;
using Experior.Core.Loads;

namespace Experior.Catalog.Dematic.Case.Components
{
    public class TrayStacker : StraightPhotocellConveyor
    {
        private TrayStackerInfo stackerInfo;
        private bool loadActive = false;
        private bool forcePassThrough = false;
        private LoadWaitingStatus previousLoadWaitingStatus;
        private int loadStacks;
        private Cube trackLeft;
        private Cube trackRight;
        private Straight stackConveyor;
        private Tray stackedLoad;
        private Tray currentLoad;
        private Experior.Core.Timer delayTimer = new Experior.Core.Timer(1);
        private Operation currentOperation;

        public TrayStacker(TrayStackerInfo info) : base(info)
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
            stackConveyor.LocalPosition = new Vector3(0, TrayHeight + 0.005f, 0);

            // Initialise as request
            ThisRouteStatus.Available = RouteStatuses.Request;
        }

        #region Private Methods

        private enum Operation
        {
            PreviousLoadArrived,
            NextRouteStatusChanged,
            PhotocellBlocked,
            PhotocellClear,
            ChangeStatusToAvailable,
        }

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
            var stackerHeight = stackerInfo.StackLimit * stackerInfo.TrayHeight + 0.5f; // (Max stacks * tray height + little extra)
            var offCentre = Width / 2 + 0.05f;
            var distance = ((Length - stackerInfo.TrayLength) + (stackerInfo.TrayLength / 4)) / 2;
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

        private void Process_OnElaspsed(Experior.Core.Timer timer)
        {
            delayTimer.OnElapsed -= Process_OnElaspsed;
            if (currentOperation == Operation.ChangeStatusToAvailable)
            {
                ThisRouteStatus.Available = RouteStatuses.Available;
            }
            if (currentOperation == Operation.PreviousLoadArrived)
            {
                ReleasePreviousLoad();
            }
            if (currentOperation == Operation.PhotocellBlocked)
            {
                StackLoads();
                ThisRouteStatus.Available = RouteStatuses.Request;
                loadActive = false;
                ReleasePreviousLoad();
            }
            if (currentOperation == Operation.PhotocellClear)
            {
                ThisRouteStatus.Available = RouteStatuses.Request;
                loadActive = false;
                ReleasePreviousLoad();
            }
            if (currentOperation == Operation.NextRouteStatusChanged)
            {
                if (NextRouteStatus.Available == RouteStatuses.Available || NextRouteStatus.Available == RouteStatuses.Request)
                {
                    Motor = MotorStatus.Running;
                    ThisRouteStatus.Available = RouteStatuses.Request;
                    loadActive = false;
                    ReleasePreviousLoad();
                }
            }
        }

        private void StackLoads()
        {
            loadStacks++;
            stackedLoad = GetStackedLoad();
            currentLoad = GetCurrentLoad();
            if (currentLoad != null)
            {
                if (stackedLoad != null)
                {
                    loadStacks = stackedLoad.Grouped.Items.Count + 2;
                }
                // Check whether the load entering is already a stack or loaded
                // If so allow it to pass through
                if (currentLoad.Status == TrayStatus.Stacked || currentLoad.Status == TrayStatus.Loaded)
                {
                    currentLoad.UserDeletable = true;
                    loadStacks = stackerInfo.StackLimit;
                }
            }
            if (loadStacks < stackerInfo.StackLimit && !forcePassThrough)
            {
                // Switch the load from the current conveyor into the stack conveyor
                MoveLoadToStack();
            }
            else
            {
                // Move the stack back on the conveyor
                MoveLoadBackToConveyor();
            }
        }

        private void MoveLoadToStack()
        {
            if (currentLoad != null)
            {
                if (stackedLoad != null)
                {
                    currentLoad.Switch(stackConveyor.TransportSection.Route, currentLoad.Distance, true);
                    var stackY = (currentLoad.Height + 0.005f) * (stackedLoad.Grouped.Items.Count + 1); // create empty space between loads     
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
                    currentLoad.Group(stackedLoad, new Vector3(0, TrayHeight + 0.005f, 0));
                }
                // Second move all the loads that were previously grouped to it
                for (int i = 0; i < stackedLoadsCount; i++)
                {
                    stackedLoads[i].Switch(TransportSection.Route, true);
                    if (currentLoad != null)
                    {
                        currentLoad.Group(stackedLoads[i], new Vector3(0, (TrayHeight + 0.005f) * (i + 2), 0));
                    }
                    else
                    {
                        stackedLoad.Group(stackedLoads[i], new Vector3(0, (TrayHeight + 0.005f) * (i + 1), 0));
                    }
                }
            }
            ReleaseLoad();
        }

        private void ReleaseLoad()
        {
            loadStacks = 0;
            if (NextRouteStatus != null && NextRouteStatus.Available == RouteStatuses.Available)
            {
                ThisRouteStatus.Available = RouteStatuses.Blocked;
                ThisRouteStatus.Available = RouteStatuses.Request;
            }
            else
            {
                currentLoad = GetCurrentLoad();
                if (loadActive || currentLoad != null)
                {
                    ThisRouteStatus.Available = RouteStatuses.Blocked;
                }
                else
                {
                    ChangeStatusToAvailable();
                }
            }
        }

        private Tray GetCurrentLoad()
        {
            Tray tray = null;
            if (TransportSection.Route.Loads.Count > 0)
            {
                tray = (Tray)TransportSection.Route.Loads.First();
            }
            return tray;
        }

        private Tray GetStackedLoad()
        {
            Tray tray = null;
            if (stackConveyor.TransportSection.Route.Loads.Count > 0)
            {
                tray = (Tray)stackConveyor.TransportSection.Route.Loads.First();
            }
            return tray;
        }

        private void ReleasePreviousLoad()
        {
            if (previousLoadWaitingStatus != null && previousLoadWaitingStatus.LoadWaiting)
            {
                Load previousLoad = previousLoadWaitingStatus.WaitingLoad;

                // Check if load is a tray if not let the load pass through
                if (previousLoad == null && previousLoad.GetType() != typeof(Tray))
                {
                    forcePassThrough = true;
                    StackLoads();
                }
                else
                {
                    Tray previousTrayLoad = (Tray)previousLoad;
                    if (previousTrayLoad.Status != TrayStatus.Empty)
                    {
                        forcePassThrough = true;
                        StackLoads();
                    }
                    else
                    {
                        forcePassThrough = false;
                    }
                }
                if (ThisRouteStatus != null && ThisRouteStatus.Available == RouteStatuses.Request && !loadActive)
                {
                    ChangeStatusToAvailable();
                }
            }
        }

        private void ChangeStatusToAvailable()
        {
            if (!delayTimer.Running)
            {
                // Only change to available if the timer is not running
                // otherwise allow the currently running operation to complete
                currentOperation = Operation.ChangeStatusToAvailable;
                delayTimer.OnElapsed += Process_OnElaspsed;
                delayTimer.Start();
            }
        }

        private void PreviousLoadWaitingStatusStraight_OnLoadWaitingChanged(object sender, LoadWaitingChangedEventArgs e)
        {
            //If a load arrives and there is nothing else in the divert make it available
            if (e._loadWaiting)
            {
                if (previousLoadWaitingStatus == null)
                {
                    previousLoadWaitingStatus = new LoadWaitingStatus();
                }
                previousLoadWaitingStatus.LoadWaiting = true;
                previousLoadWaitingStatus.WaitingLoad = (Load)e._waitingLoad;

                if (!delayTimer.Running)
                {
                    ThisRouteStatus.Available = RouteStatuses.Request;
                    currentOperation = Operation.PreviousLoadArrived;
                    delayTimer.OnElapsed += Process_OnElaspsed;
                    delayTimer.Start();
                }
            }
            if (!e._loadWaiting)
            {
                previousLoadWaitingStatus = null;
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
            }
            Reset();
        }

        public override void NextRouteStatus_OnAvailableChanged(object sender, RouteStatusChangedEventArgs e)
        {
            if (!delayTimer.Running)
            {
                ThisRouteStatus.Available = RouteStatuses.Request;
                currentOperation = Operation.NextRouteStatusChanged;
                delayTimer.OnElapsed += Process_OnElaspsed;
                delayTimer.Start();
            }
        }

        public override void LineReleasePhotocell_OnPhotocellStatusChanged(object sender, PhotocellStatusChangedEventArgs e)
        {
            if (e._PhotocellStatus == PhotocellState.Blocked)
            {
                e._Load.UserDeletable = false;
                ThisRouteStatus.Available = RouteStatuses.Blocked;
                RouteAvailable = RouteStatuses.Blocked; //Stop the motor
                if (!delayTimer.Running)
                {
                    currentOperation = Operation.PhotocellBlocked;
                    delayTimer.OnElapsed += Process_OnElaspsed;
                    delayTimer.Start();
                }
               
            }
            else if (e._PhotocellStatus == PhotocellState.Clear)
            {
                if (e._Load != null)
                {
                    e._Load.UserDeletable = true;
                }
                SetLoadWaiting(false, false, e._Load);
                if (!delayTimer.Running)
                {
                    ThisRouteStatus.Available = RouteStatuses.Request;
                    currentOperation = Operation.PhotocellClear;
                    delayTimer.OnElapsed += Process_OnElaspsed;
                    delayTimer.Start();
                }
            }
        }

        public override void UpdateLength(float length)
        {
            base.UpdateLength(length);
            stackConveyor.Length = length;
            stackConveyor.LocalPosition = new Vector3(0, TrayHeight + 0.005f, 0);
            DrawTracks();
            Core.Environment.Properties.Refresh();
        }

        public override void UpdateWidth()
        {
            base.UpdateWidth();
            DrawTracks();
            Core.Environment.Properties.Refresh();
        }

        public override void Reset()
        {
            base.Reset();
            ResetConveyorLoads(stackConveyor, true);
            ResetConveyorLoads(this, true);
            loadStacks = 0;
            loadActive = false;
            ThisRouteStatus.Available = RouteStatuses.Request;
            previousLoadWaitingStatus = null;
            currentLoad = null;
            stackedLoad = null;
        }

        public override void Dispose()
        {
            stackConveyor.Dispose();
            trackLeft.Dispose();
            trackRight.Dispose();
            base.Dispose();
        }

        #endregion

        #region Public Properties

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
        [DisplayName(@"Tray Length")]
        public float TrayLength
        {
            get { return stackerInfo.TrayLength; }
            set { stackerInfo.TrayLength = value; }
        }

        [Category("Stacker Configuration")]
        [DisplayName(@"Tray Height")]
        [ReadOnly(true)]
        public float TrayHeight
        {
            get { return stackerInfo.TrayHeight; }
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
            get { return "Tray Stacker"; }
        }

        public override Image Image
        {
            get { return Common.Icons.Get("Stacker"); }
        }

        #endregion
    }

    [Serializable]
    [XmlInclude(typeof(TrayStackerInfo))]
    public class TrayStackerInfo : StraightPhotocellConveyorInfo
    {
        public int StackLimit;
        public float TrayHeight;
        public float TrayLength;
    }
}
