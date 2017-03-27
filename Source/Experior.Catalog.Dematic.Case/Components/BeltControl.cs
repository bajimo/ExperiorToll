using Experior.Catalog.Dematic.Case.Devices;
using Experior.Core;
using Experior.Core.Assemblies;
using Experior.Core.Loads;
using Experior.Core.TransportSections;
using Experior.Dematic.Base;
using Experior.Dematic.Base.Devices;
using Experior.Dematic.Case.Devices;
using System;
using System.Collections.Generic;

namespace Experior.Catalog.Dematic.Case.Components
{
    public interface IBeltControl
    {
        IList<Assembly> Assemblies { get; }
        RouteStatus NextRouteStatus { get; set; }
        RouteStatus ThisRouteStatus { get; set; }
        RouteStatuses RouteAvailable { get; set; }
        void SetLoadWaiting(bool loadWaiting, bool loadDeleted, Load waitingLoad);
        ITransportSection TransportSection { get; set; }
        string LineReleasePhotocellName { get; set; }
        List<DeviceInfo> DeviceInfos { get; }
        float ReleaseDelay { get; set; }
        float LoadWaitingDelay { get; set; }
        bool ScriptRelease { get; set; }
        bool LoadWaiting { get; set; }
    }

    public class BeltControl
    {
        public ConstructDevice constructDevice;
        IBeltControl conveyor;
        bool LineReleaseEventSubcribed = false;
        Timer ReleaseTimer = new Timer(0);
        Timer LoadWaitingTimer = new Timer(0);
        bool LoadWaitingTimerPaused = false;

        public BeltControl(Assembly assembly)
        {
            conveyor = assembly as IBeltControl;
            ReleaseTimer.OnElapsed += ReleaseTimer_OnElapsed;
            LoadWaitingTimer.OnElapsed += LoadWaitingTimer_OnElapsed;

            if (!Core.Environment.Scene.Loading)
            {
                constructDevice = new ConstructDevice(string.Empty);
                DeviceInfo deviceInfo = conveyor.DeviceInfos.Find(i => i.name == conveyor.LineReleasePhotocellName);

                if (deviceInfo == null)
                {
                    CasePhotocellInfo photocellInfo = new CasePhotocellInfo();
                    photocellInfo.name = conveyor.LineReleasePhotocellName;
                    photocellInfo.distanceFrom = PositionPoint.End;
                    photocellInfo.distance = 0.125f;
                    photocellInfo.type = constructDevice.DeviceTypes["Add Photocell"].Item1; //Item1 is the device type ...obviously!

                    conveyor.DeviceInfos.Add(photocellInfo);
                }
                constructDevice.InsertDevices(conveyor as IConstructDevice);

                SetLineReleasePhotocell();
                LineReleaseEvents(true);
            }
        }

        public void Scene_OnLoaded()
        {
            constructDevice = new ConstructDevice(string.Empty);
            SetLineReleasePhotocell();
        }

        public void Reset()
        {
            ReleaseTimer.Reset();
            LoadWaitingTimer.Reset();
            LoadWaitingTimerPaused = false;
        }

        void SetLineReleasePhotocell()
        {
            if (conveyor.Assemblies != null && conveyor.Assemblies.Count > 0)
            {
                foreach (Assembly assembly in conveyor.Assemblies)
                {
                    if (assembly.Name == conveyor.LineReleasePhotocellName)
                    {
                        LineReleasePhotocell = assembly as CasePhotocell;
                        LineReleaseEvents(true);
                        return;
                    }
                }
            }
        }

        public List<CasePhotocell> PhotoCells()
        {
            List<CasePhotocell> retList = new List<CasePhotocell>();
            if (conveyor.Assemblies != null && conveyor.Assemblies.Count > 0)
            {
                foreach (Assembly assembly in conveyor.Assemblies)
                {
                    if (assembly is CasePhotocell)
                    {
                        retList.Add(assembly as CasePhotocell);
                    }     
                }
            }
            return retList;
        }
        
        public void LineReleaseEvents(bool subscribe)
        {
            if (LineReleasePhotocell == null)
                return;

            if (subscribe && !LineReleaseEventSubcribed) //only subscribe once
            {
                LineReleaseEventSubcribed = true;
                LineReleasePhotocell.OnPhotocellRenamed += LineReleasePhotocell_photocellRenamed;
                LineReleasePhotocell.OnDeviceDeleted += LineReleasePhotocell_photocellDeleted;
                LineReleasePhotocell.OnPhotocellStatusChanged += LineReleasePhotocell_OnPhotocellStatusChanged;
            }
            else if (!subscribe && LineReleaseEventSubcribed)
            {
                LineReleaseEventSubcribed = false;               
                LineReleasePhotocell.OnDeviceDeleted -= LineReleasePhotocell_photocellDeleted;
                LineReleasePhotocell.OnPhotocellRenamed -= LineReleasePhotocell_photocellRenamed;
                LineReleasePhotocell.OnPhotocellStatusChanged -= LineReleasePhotocell_OnPhotocellStatusChanged;
            }
        }

        void LineReleasePhotocell_photocellRenamed(object sender, PhotocellRenamedEventArgs e)
        {
            conveyor.LineReleasePhotocellName = e._NewName;
        }

        void LineReleasePhotocell_photocellDeleted(object sender, EventArgs e)
        {
            LineReleaseEvents(false);
            LineReleasePhotocell = null;
            conveyor.LineReleasePhotocellName = "";
        }

        public void Dispose()
        {
            if (LineReleasePhotocell != null)
            {
                LineReleaseEvents(false);
                LineReleasePhotocell.Dispose();
            }
        }

        #region Control Logic

        public void EnableRelease(Load load) //This is called whenever the load is trying to be released by the routing script
        {
            conveyor.SetLoadWaiting(true, false, load);
        }

        void LineReleasePhotocell_OnPhotocellStatusChanged(object sender, PhotocellStatusChangedEventArgs e)
        {
            if (e._PhotocellStatus == PhotocellState.Blocked || e._PhotocellStatus == PhotocellState.LoadBlocked)
            {
                //[BG] Mod to allow the Routing script to control the release of the loads from a belt conveyor
                if (!conveyor.ScriptRelease)
                {
                    if (conveyor.LoadWaitingDelay != 0)
                    {
                        StartLoadWaitingTimer(e._Load);
                    }
                    else
                    {
                        SetLoadWaiting(e._Load);
                    }
                }

                if (conveyor.NextRouteStatus == null || conveyor.NextRouteStatus.Available != RouteStatuses.Available || ReleaseTimer.Running || LoadWaitingTimer.Running)
                {
                    conveyor.RouteAvailable = RouteStatuses.Blocked; //This will also stop the motor
                }
                else
                {
                    //Load has been released and i should start the delay timer if there is one
                    StartReleaseTimer();
                }
            }
            else if (e._PhotocellStatus == PhotocellState.Clear || e._PhotocellStatus == PhotocellState.LoadClear)
            {
                conveyor.RouteAvailable = RouteStatuses.Available;
                if (e._Load != null)
                {
                    conveyor.SetLoadWaiting(false, e._Load.StartDisposing, e._Load);
                }
                else
                {
                    conveyor.SetLoadWaiting(false, false, e._Load);
                }
            }
        }

        private void SetLoadWaiting(Load load)
        {
            conveyor.SetLoadWaiting(true, false, load);
        }

        public void NextRouteStatus_OnAvailableChanged(object sender, RouteStatusChangedEventArgs e)
        {
            if (e._available == RouteStatuses.Available)
            {
                if (!ReleaseTimer.Running || !LoadWaitingTimer.Running)
                {
                    conveyor.RouteAvailable = RouteStatuses.Available; //Restart the motor
                    StartReleaseTimer();
                }
            }
            else
            { //If the line release photocell is null then the belt conveyor will become the overdriver of the front conveyor
                if ((LineReleasePhotocell != null && LineReleasePhotocell.PhotocellStatus == PhotocellState.Blocked) || LineReleasePhotocell == null)
                {
                    conveyor.RouteAvailable = RouteStatuses.Blocked;
                }
            }

            if (e._available == RouteStatuses.Available || e._available == RouteStatuses.Request)
            {
                if (LoadWaitingTimerPaused)
                {
                    LoadWaitingTimerPaused = false;
                    LoadWaitingTimer.Start();
                }
            }
            else
            {
                if (LoadWaitingTimer.Running)
                {
                    LoadWaitingTimerPaused = true;
                    LoadWaitingTimer.Stop();
                }
            }

            //if (!LoadWaitingTimer.Running && !LoadWaitingTimerPaused && !conveyor.LoadWaiting)
            //{
                

            //}
        }

        void StartReleaseTimer()
        {
            if (conveyor.ReleaseDelay != 0)
            {
                ReleaseTimer.Timeout = conveyor.ReleaseDelay;
                ReleaseTimer.Start();
            }
        }

        void ReleaseTimer_OnElapsed(Timer sender)
        {
            ReleaseTimer.Reset();
            if ((conveyor.NextRouteStatus.Available == RouteStatuses.Available) && (LineReleasePhotocell.PhotocellStatus == PhotocellState.Blocked || LineReleasePhotocell.PhotocellStatus == PhotocellState.LoadBlocked))
            {
                conveyor.RouteAvailable = RouteStatuses.Available;
                StartReleaseTimer();
            }
        }

        void StartLoadWaitingTimer(Load load)
        {
            if (!LoadWaitingTimer.Running && !LoadWaitingTimerPaused && conveyor.LoadWaitingDelay != 0)
            {
                LoadWaitingTimer.Timeout = conveyor.LoadWaitingDelay;
                LoadWaitingTimer.UserData = load;
                if (conveyor.NextRouteStatus.Available == RouteStatuses.Blocked)
                {
                    LoadWaitingTimerPaused = true;
                }
                else
                {
                    LoadWaitingTimer.Start();
                }
            }
        }

        private void LoadWaitingTimer_OnElapsed(Timer sender)
        {
            SetLoadWaiting(sender.UserData as Load);
        }
        
        public void SetRouteAvailable(RouteStatuses value)
        {
            if (value == RouteStatuses.Available)
            {
                conveyor.TransportSection.Route.Motor.Start();
            }
            else
            {
                conveyor.TransportSection.Route.Motor.Stop();
            }

            if (conveyor.ThisRouteStatus != null)
                conveyor.ThisRouteStatus.Available = value;
        }
        #endregion

        #region Properties

        private CasePhotocell _LineReleasePhotocell;
        public CasePhotocell LineReleasePhotocell
        {
            get { return _LineReleasePhotocell; }
            set
            {
                _LineReleasePhotocell = value;
                LineReleaseEvents(true);
            }
        }
        #endregion
    }
}
