using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Experior.Catalog.Dematic.Case;
using Experior.Core.Parts;
using Experior.Core.TransportSections;
using System.Drawing;
using Experior.Core.Routes;
using Experior.Dematic;
using System.ComponentModel;
using Microsoft.DirectX;
using Experior.Dematic.Storage.Base;
using Experior.Catalog.Dematic.Case.Components;
using Experior.Core.Loads;
using Experior.Dematic.Base;

namespace Experior.Catalog.Dematic.Storage.Assemblies
{

    /// <summary>
    /// poroerties needed for an infeed or rack conveyor to release loads onto the elevator
    /// </summary>
    public interface IConvToElevator
    {
        ActionPoint Location1{ get; set; }
        ActionPoint Location2{ get; set; }
        Vector3 LocalPosition { get;}
        Route ConvRoute { get; }     
    }

    public class InfeedPickStationConveyor : StraightAccumulationConveyor, IConvToElevator, ITransferLoad// StraightConveyor, IConvToElevator
    {
        
        public InfeedPickStationConveyor3Info infeedInfo;
        private ActionPoint location1, location2;
        private bool infeedInProgress;
        private AccumulationSensor sensor0, sensor1;
        private ActionPoint enterPoint = new ActionPoint();
        private ActionPoint exitPoint = new ActionPoint();

       // public List<AccumulationSensor> sensors = new List<AccumulationSensor>(); 

        public InfeedPickStationConveyor(InfeedPickStationConveyor3Info info) : base(info)
        {
            infeedInfo   = info;
            Name         = info.name;
            OnConveyorLoaded += InfeedPickStationConveyor_OnConveyorLoaded;           //Any conveyor adjustments must all be made in OnConveyorLoaded
        }

        public override void StartFixPoint_OnSnapped(FixPoint stranger, FixPoint.SnapEventArgs e)
        {
            base.StartFixPoint_OnSnapped(stranger, e);
        }

        /// <summary>
        /// As the accumilation conveyor is not set conventually it is set via positions, outfeedsection
        /// infeedsection and AccPitch. We have to wait until all these are set. therefor we neen this method to place 
        /// transition points
        /// </summary>
        public void PlaceTransferPoints()
        {
            Route.InsertActionPoint(ExitPoint, Length);
            Route.InsertActionPoint(EnterPoint, 0);
            ExitPoint.OnEnter += ExitPoint_OnEnter;
            exitPoint.Visible = true;            
        }

        void InfeedPickStationConveyor_OnConveyorLoaded(object sender, EventArgs e)
        {
            foreach (AccumulationSensor sensor in sensors)
            {
                //The accumilation conveyor PEC last (wrt the load travel direction) position is called "0", this is position 2 for the multishuttle.
                //The accumilation conveyor PEC second from last position (wrt the load travel direction) is called "1", this is position 1 for the multishuttle.

                int convPosName;
                if (int.TryParse(sensor.sensor.Name, out convPosName) && convPosName == 0)
                {
                    sensor.locName = infeedInfo.location2Name;
                    Location2 = sensor.sensor;
                    MultiShuttle.PickStationNameToActionPoint.Remove(infeedInfo.location2Name);
                    MultiShuttle.PickStationNameToActionPoint.Add(infeedInfo.location2Name, sensor.sensor);
                    sensor.sensor.OnEnter -= sensor_OnEnter2;
                    sensor.sensor.OnEnter += sensor_OnEnter2;
                    sensor.sensor.OnLeave += sensor_OnLeave2;

                    sensor.sensor.leaving.Edge = ActionPoint.Edges.Leading;
                    sensor.sensor.leaving.Distance = sensor.sensor.leaving.Distance + 0.1f;
                    //sensor.sensor.trainling.position = move to the end of the conveyor

                }
                else if (int.TryParse(sensor.sensor.Name, out convPosName) && convPosName == 1)
                {
                    sensor.locName = infeedInfo.location1Name;
                    Location1 = sensor.sensor;
                    MultiShuttle.PickStationNameToActionPoint.Remove(infeedInfo.location1Name);
                    MultiShuttle.PickStationNameToActionPoint.Add(infeedInfo.location1Name, sensor.sensor);
                    sensor.sensor.OnEnter -= sensor_OnEnter1;
                    sensor.sensor.OnEnter += sensor_OnEnter1;
                }
            }
        }

        public RouteAvailableStatus infeedRouteAvailableStatus = new RouteAvailableStatus();

        public override RouteAvailableStatus GetAvailableStatus(FixPoint startFixPoint)
        {
            return infeedRouteAvailableStatus;
        }

        public override void Reset()
        {
            infeedRouteAvailableStatus.Available = AvailableStatus.Available;
            base.Reset();
            enterPoint.Visible = true;
            exitPoint.Visible = true;
        }

        void sensor_OnLeave2(DematicSensor sender, Load load)
        {
            if (infeedInProgress)
            {
               infeedRouteAvailableStatus.Available = AvailableStatus.Blocked;
            }
        }

        public void sensor_OnEnter1(DematicSensor sender, Load load)
        {
            Core.Environment.Log.Write(load.ToString() + "sensor_OnEnter1");
            Case_Load caseload1 = load as Case_Load;
            if (caseload1 == null)
            {
                
                Core.Environment.Log.Write(Name + ": Error. Load is not a caseload " + load + " entering MS", Color.Red);
                Core.Environment.Scene.Pause();
                return;
            }

            if (string.IsNullOrWhiteSpace(caseload1.SSCCBarcode))
            {
                Core.Environment.Log.Write(Name + ": Error. Tote " + load + " entering MS has no SSCCBarcode!", Color.Red);
                Core.Environment.Scene.Pause();
            }

            if (MultiShuttle.caseloads.Contains(caseload1))
            {
                Core.Environment.Log.Write(Name + ": Error. Tote ULID: " + load + " enters MS, but ULID is already known by MS. Did MS recieve type 20?", Color.Red);
                return;
                Core.Environment.Scene.Pause();
            }
            else
            {
                MultiShuttle.caseloads.Add(caseload1);
            }

            load.UserDeletable = false;
            load.Deletable = true;

            //InfeedPickStationConveyor ElevatorConveyor = sender.Parent.Parent.Parent as InfeedPickStationConveyor;
            Case_Load caseload2 = null;

            ActionPoint ap2 = location2;
            if (ap2 != null)
            {
                caseload2 = ap2.ActiveLoad as Case_Load;
            }

            //Reset wmswait in case BK10 forgot...
            caseload1.Case_Data.RoutingTableUpdateWait = false;
            caseload1.Case_Data.OriginalPosition       = "";
            caseload1.Case_Data.CurrentPosition        = "";
            caseload1.Case_Data.DestinationPosition    = "";
            caseload1.Case_Data.PLCName                = "";
            caseload1.Case_Data.MissionTelegram        = null;

            caseload1.CurrentPosition = infeedInfo.location1Name; //Update current location

            if (caseload2 != null && caseload2.Waiting)           //A tote at location2 and waiting for this tote
            {
                load.Stop();
                caseload2.WaitingTime = 0;                        //Stop waiting. This triggers Tote_PickStation2TimeOut
                return;
            }

            if (Route.Loads.Count == 2 && caseload2 == null) //A caseload should arrive after this on location 2 or is being moved on to the elevator...  
            {
                if (Route.Loads.Last.Value.Distance < MultiShuttle.PickStationNameToActionPoint[infeedInfo.location2Name].Distance)
                {
                    if (Route.Loads.Last.Value.UserData is MultishuttleElevatorJobData)//case is moving on to elevator. Let this one pass
                        return;

                    load.Stop();
                }
                return;
            }
            if (Route.Loads.Count > 2 && caseload2 == null)
            {
                load.Stop();
                return;
            }
            if (caseload2 != null && !caseload2.Waiting)                    //caseload2 has already sent 25 arrival telegram.
            {
                load.Stop();
            }

        }

        private void sensor_OnEnter2(DematicSensor sender, Load load)
        {
            if (load.UserData is MultishuttleElevatorJobData) //caseload is moving on to elevator. Ignore.
            {
                load.Release();
                return;
            }

            load.Stop();

            //InfeedPickStationConveyor ElevatorConveyor = sender.Parent.Parent.Parent as InfeedPickStationConveyor;

            if (!MultiShuttle.PickStationNameToActionPoint[infeedInfo.location1Name].Active)//No caseload on location 1. Wait...
            {
                load.WaitingTime = MultiShuttle.PickStation2Timeout; //Wait for tote number 2
                load.OnFinishedWaitingEvent += Tote_PickStation2TimeOut;
            }
            else
            {
                //A caseload waits on location 1
                Tote_PickStation2TimeOut(load);
            }
        }

        /// <summary>
        /// The first tote to arrive and wait for an elevator sets a timer this timer has either expired or a second tote has arrived
        /// and stopped the timer triggering this method to be called via the OnFinishedWaitingEvent.
        /// </summary>
        /// <param name="load"></param>
        void Tote_PickStation2TimeOut(Load load)
        {
            load.OnFinishedWaitingEvent -= Tote_PickStation2TimeOut;
            load.Stop();

            //InfeedPickStationConveyor ElevatorConveyor = load.CurrentActionPoint.Parent.Parent.Parent as InfeedPickStationConveyor;
            Case_Load caseload1 = location1.ActiveLoad as Case_Load;
            Case_Load caseload2 = location2.ActiveLoad as Case_Load;
            if (caseload1 != null)
            {
                caseload1.CurrentPosition = infeedInfo.location1Name;
            }
            if (caseload2 != null)
            {
                caseload2.CurrentPosition = infeedInfo.location2Name;
            }

            if (caseload2.UserData is MultishuttleElevatorJobData)
            {
                Core.Environment.Log.Write("Error. caseload2 on pick station already has elevatorjob!", Color.Red);
                Core.Environment.Scene.Pause();
                return;
            }

            if (caseload1 != null && caseload1.UserData is MultishuttleElevatorJobData)
            {
                Core.Environment.Log.Write("Error. caseload1 on pick station already has elevatorjob!", Color.Red);
                Core.Environment.Scene.Pause();
                return;
            }

            if (caseload2.Case_Data.RoutingTableUpdateWait)
            {
                Core.Environment.Log.Write("Error. caseload2 on pick station already has WMSWait (25 arrival already sent)! Barcode: " + caseload2.SSCCBarcode, Color.Red);
                Core.Environment.Scene.Pause();
                return;
            }

            if (caseload1 != null && caseload1.Case_Data.RoutingTableUpdateWait)
            {
                Core.Environment.Log.Write("Error. caseload1 on pick station already has WMSWait (25 arrival already sent)! Barcode: " + caseload1.SSCCBarcode, Color.Red);
                Core.Environment.Scene.Pause();
                return;
            }

            //RouteAvailable = Case.AvailableStatus.Blocked;
            infeedRouteAvailableStatus.Available = AvailableStatus.Blocked;
            infeedInProgress = true;
            MultiShuttle.Control.InfeedTimeOut(caseload2, caseload1);
        }

        private Route route;

        public Route Route
        {
          get { return TransportSection.Route; }
          set { route = value; }
        }

        [Browsable(false)]
        public Core.Parts.FixPoint infeedFix { get; set; }

        //[Browsable(false)]
        //public ActionPoint  Location1, Location2;//,  Exit, Entry;

        [Browsable(false)]
        public MultiShuttleElevator Elevator 
        {
            get { return infeedInfo.elevator; }
            set { infeedInfo.elevator = value; }
        }

        [Browsable(false)]
        public DematicMultiShuttle MultiShuttle
        {
            get { return infeedInfo.multiShuttle; }
            set { infeedInfo.multiShuttle = value; }
        }

        [Browsable(false)]
        public LevelHeight SavedLevel 
        {
            get {return infeedInfo.levelHeight; }
            set { infeedInfo.levelHeight = value; } 
        }

        [CategoryAttribute("Configuration")]
        [DescriptionAttribute("Level")]
        [DisplayName("Level")]
        public string Level
        {
            get { return SavedLevel.Level; }
            set { SavedLevel.Level = value; }
        }

        #region IConvToElevator

        public ActionPoint Location1
        {
            get { return location1; }
            set { location1 = value; }
        }

        public ActionPoint Location2
        {
            get { return location2; }
            set { location2 = value; }
        }

        //public Vector3 localPosition
        //{
        //    get{ return LocalPosition; }
        //}

        public Route ConvRoute
        {
            get{ return Route; }
        }



        #endregion IConvToElevator

        #region ITransferLoad and related to it

        public ActionPoint EnterPoint
        {
            get { return enterPoint; }
        }

        public ActionPoint ExitPoint
        {
            get { return exitPoint; }
        }

        private void ExitPoint_OnEnter(ActionPoint sender, Load load)
        {
            exitPoint.Visible = true;
            MultishuttleElevatorJobData job = load.UserData as MultishuttleElevatorJobData;
            if (job != null)
            {
                ITransferLoad tL = job.Parent.ElevatorConveyor;
                load.Switch(tL.EnterPoint);
            }
        }

        #endregion
    }

    public class InfeedPickStationConveyor3Info:StraightAccumulationConveyorInfo
    {
        public MultiShuttleElevator elevator;
        public LevelHeight levelHeight;
        public DematicMultiShuttle multiShuttle;
        public string location1Name, location2Name;       
    }

}
