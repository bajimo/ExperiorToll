using Experior.Catalog.Dematic.Case.Devices;
using Experior.Core.Assemblies;
using Experior.Dematic.Base;
using Experior.Dematic.Base.Devices;
using System;
using System.Drawing;
using System.ComponentModel;
using System.Xml.Serialization;
using Experior.Core;
using Experior.Dematic.Case.Devices;

namespace Experior.Catalog.Dematic.Case.Components
{
    public class StraightPhotocellConveyor : StraightConveyor
    {
        private StraightPhotocellConveyorInfo straightInfo;
        private bool lineReleaseEventSubcribed = false;
        private Experior.Core.Timer releaseTimer = new Experior.Core.Timer(0);
        private MotorStatus motor = MotorStatus.Stopped;
        private CasePhotocell lineReleasePhotocell;

        public StraightPhotocellConveyor(StraightPhotocellConveyorInfo info): base(info)
        {
            try
            {
                straightInfo = info;
                releaseTimer.OnElapsed += ReleaseTimer_OnElapsed;

                constructDevice = new ConstructDevice(string.Empty);
                LineReleasePhotocellName = straightInfo.LineReleasePhotocellName;
                DeviceInfo deviceInfo = DeviceInfos.Find(i => i.name == LineReleasePhotocellName);
                if (deviceInfo == null)
                {
                    CasePhotocellInfo photocellInfo = new CasePhotocellInfo();
                    photocellInfo.name = "LineRelease";
                    photocellInfo.distanceFrom = PositionPoint.End;
                    photocellInfo.distance = 0.125f;
                    photocellInfo.type = constructDevice.DeviceTypes["Add Photocell"].Item1; //Item1 is the device type ...obviously!
                    DeviceInfos.Add(photocellInfo);
                }
                constructDevice.InsertDevices(this as IConstructDevice);

                SetLineReleasePhotocell();
                LineReleaseEvents(true);
            }
            catch (Exception ex)
            {
                Core.Environment.Log.Write(ex.Message);
            }
        }

        #region Public Override Methods

        public override void ThisRouteStatus_OnAvailableChanged(object sender, RouteStatusChangedEventArgs e)
        {
            if (e._available == RouteStatuses.Available)
                arrow.Color = Color.Green;
            else if (e._available == RouteStatuses.Request)
                arrow.Color = Color.Yellow;
            else
                arrow.Color = Color.Red;

            if (e._available == RouteStatuses.Available || e._available == RouteStatuses.Request)
            {
                Motor = MotorStatus.Running;
            }
            else if (e._available == RouteStatuses.Blocked)
            {
                Motor = MotorStatus.Stopped;
            }
        }

        public override void NextRouteStatus_OnAvailableChanged(object sender, RouteStatusChangedEventArgs e)
        {
            if (e._available == RouteStatuses.Available || e._available == RouteStatuses.Request)
            {
                Motor = MotorStatus.Running;
            }
        }

        #endregion

        #region Private Methods

        private void SetLineReleasePhotocell()
        {
            if (Assemblies != null && Assemblies.Count > 0)
            {
                foreach (Assembly assembly in Assemblies)
                {
                    if (assembly.Name == LineReleasePhotocellName)
                    {
                        LineReleasePhotocell = assembly as CasePhotocell;
                        LineReleaseEvents(true);
                        return;
                    }
                }
            }
        }

        private void ReleaseTimer_OnElapsed(Timer sender)
        {
            releaseTimer.Reset();
            if ((this.NextRouteStatus.Available == RouteStatuses.Available) && (LineReleasePhotocell.PhotocellStatus == PhotocellState.Blocked || LineReleasePhotocell.PhotocellStatus == PhotocellState.LoadBlocked))
            {
                this.RouteAvailable = RouteStatuses.Available;
                StartReleaseTimer();
            }
        }

        private void LineReleaseEvents(bool subscribe)
        {
            if (LineReleasePhotocell == null)
                return;

            if (subscribe && !lineReleaseEventSubcribed) //only subscribe once
            {
                lineReleaseEventSubcribed = true;
                LineReleasePhotocell.OnPhotocellRenamed += LineReleasePhotocell_photocellRenamed;
                LineReleasePhotocell.OnDeviceDeleted += LineReleasePhotocell_PhotocellDeleted;
                LineReleasePhotocell.OnPhotocellStatusChanged += LineReleasePhotocell_OnPhotocellStatusChanged;
            }
            else if (!subscribe && lineReleaseEventSubcribed)
            {
                lineReleaseEventSubcribed = false;
                LineReleasePhotocell.OnDeviceDeleted -= LineReleasePhotocell_PhotocellDeleted;
                LineReleasePhotocell.OnPhotocellRenamed -= LineReleasePhotocell_photocellRenamed;
                LineReleasePhotocell.OnPhotocellStatusChanged -= LineReleasePhotocell_OnPhotocellStatusChanged;
            }
        }

        private void StartReleaseTimer()
        {
            if (this.ReleaseDelay != 0)
            {
                releaseTimer.Timeout = this.ReleaseDelay;
                releaseTimer.Start();
            }
        }
        
        private void LineReleasePhotocell_photocellRenamed(object sender, PhotocellRenamedEventArgs e)
        {
            LineReleasePhotocellName = e._NewName;
        }

        private void LineReleasePhotocell_PhotocellDeleted(object sender, EventArgs e)
        {
            LineReleaseEvents(false);
            LineReleasePhotocell = null;
            LineReleasePhotocellName = "";
        }

        #endregion

        public virtual void LineReleasePhotocell_OnPhotocellStatusChanged(object sender, PhotocellStatusChangedEventArgs e)
        {
            if (e._PhotocellStatus == PhotocellState.Blocked)
            {
                SetLoadWaiting(true, false, e._Load);

                //Always set this conveyor blocked whenever the load arrives at the photocell, this tells the 
                //previous conveyor that it has arrived and therefore the next load can be released into it
                if (NextRouteStatus != null && NextRouteStatus.Available == RouteStatuses.Available && ThisRouteStatus.Available != RouteStatuses.Request)
                {
                    ThisRouteStatus.Available = RouteStatuses.Blocked;
                    ThisRouteStatus.Available = RouteStatuses.Request;
                }
                else
                {
                    ThisRouteStatus.Available = RouteStatuses.Blocked;
                }
            }
            else if (e._PhotocellStatus == PhotocellState.Clear)
            {
                SetLoadWaiting(false, false, e._Load);
                ThisRouteStatus.Available = RouteStatuses.Request;

                //If the load was deleted then, it does not need to wait to transfer to the next conveyor
                if (e._LoadDeleted)
                {
                    ThisRouteStatus.Available = RouteStatuses.Available;
                }
            }
        }

        #region Public Properties

        /// <summary>
        /// Motor is running if in request or available
        /// Request = Transferring Load Out (Running)
        /// Available = Transferring Load In (Running)
        /// Blocked = Cannot release to next conveyor (Stopped)
        /// </summary>
        [Browsable(false)]
        public MotorStatus Motor
        {
            get { return motor; }
            set
            {
                if (value == MotorStatus.Running)
                {
                    TransportSection.Route.Motor.Start();

                }
                else if (value == MotorStatus.Stopped)
                {
                    TransportSection.Route.Motor.Stop();
                }
            }
        }

        public enum MotorStatus
        {
            Running,
            Stopped
        }

        [Category("Straight Configuration")]
        [DisplayName("Line Release Photocell")]
        [TypeConverter(typeof(PhotocellConverter))]
        public string LineReleasePhotocellName { get; set; }

        [Category("Straight Configuration")]
        public float ReleaseDelay { get; set; }
                
        [Browsable(false)]
        public CasePhotocell LineReleasePhotocell
        {
            get { return lineReleasePhotocell; }
            set
            {
                lineReleasePhotocell = value;
                LineReleaseEvents(true);
            }
        }

        #endregion
    }

    [Serializable]
    [XmlInclude(typeof(StraightPhotocellConveyorInfo))]
    public class StraightPhotocellConveyorInfo : StraightConveyorInfo
    {
        //Specific case straight belt conveyor info to be added here
        public string LineReleasePhotocellName = "LineRelease";
        public float ReleaseDelay = 0;
        public float LoadWaitingDelay = 0;
        public bool ScriptRelease = false;
    }
}
