using System;
using Experior.Catalog.Dematic.Case;
using Experior.Catalog.Dematic.Case.Components;
using Experior.Core.Routes;
using Microsoft.DirectX;
using Experior.Catalog.Dematic.Storage.Assemblies;
using System.ComponentModel;
using Experior.Core.Loads;
using Experior.Dematic.Base;

namespace Experior.Catalog.Assemblies
{
    public class OutfeedDropStationConveyor : StraightAccumulationConveyor, ITransferLoad
    {
        public OutfeedDropStationConveyorInfo outfeedInfo;
        private ActionPoint location1;
        private ActionPoint location2;

        public OutfeedDropStationConveyor(OutfeedDropStationConveyorInfo info) : base(info)
        {
            outfeedInfo  = info;
            Multishuttle = info.parent;
            Elevator     = info.elevator;

            this.OnConveyorLoaded += OutfeedPickStationConveyor_OnConveyorLoaded; 
        }

        void OutfeedPickStationConveyor_OnConveyorLoaded(object sender, EventArgs e)
        {
            foreach (AccumulationSensor sensor in sensors)
            {
                int convPosName;
                if (int.TryParse(sensor.sensor.Name, out convPosName) && convPosName == 0)
                {
                    sensor.sensor.OnEnter += sensor_OnEnter1;
                    sensor.sensor.OnLeave += sensor_OnLeave;
                    Location1 = sensor.sensor;
                }
                else if (int.TryParse(sensor.sensor.Name, out convPosName) && convPosName == 1)
                {
                    sensor.sensor.OnEnter += sensor_OnEnter2;
                    sensor.sensor.OnLeave += sensor_OnLeave;
                    Location2 = sensor.sensor;
                }
            }
        }

        void sensor_OnLeave(DematicSensor sender, Load load)
        {
            CheckAndDoNextElevatorOutFeedJob();
        }

        private void CheckAndDoNextElevatorOutFeedJob()
        {
            if (NextRouteStatus.Available == AvailableStatus.Available && !Location1.Active && !Location2.Active)
            {
            //    if (/*Elevator.CurrentJobData != null &&*/ Elevator.CurrentJobData.JobType == MultiShuttleDirections.Outfeed)
              //  {
                    Elevator.Control.Start();
                //}
            }
        }

        /// <summary>
        /// As the accumilation conveyor is not set conventually it is set via positions, outfeedsection
        /// infeedsection and AccPitch. We have to wait until all these are set. therefor we neen this method to place 
        /// transition points
        /// </summary>
        public void PlaceTransferPoints()
        {
            TransportSection.Route.InsertActionPoint(ExitPoint, Length);
            TransportSection.Route.InsertActionPoint(EnterPoint, 0);
        }
        /// <summary>
        /// A single load has been dropped off by the elevator
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="load"></param>
        public void sensor_OnEnter2(DematicSensor sender, Load load) 
        {
            Case_Load caseLoad = load as Case_Load;
            if (caseLoad != null)
            {
                caseLoad.Case_Data.CurrentPosition = "D" + Multishuttle.AisleNo + DropPositionGroupSide + Multishuttle.POS2OUTFEED + Level;
                if (Elevator.CurrentJobData.JobMode == MultishuttleElevatorJobData.JobModes.Unload1)
                {                    
                    Multishuttle.Control.ToteArrivedAtConvDropStation(this, Elevator, caseLoad, Multishuttle);
                }
                else if (Elevator.CurrentJobData.JobMode == MultishuttleElevatorJobData.JobModes.Unload2 && Location1.Active)
                {
                    Multishuttle.Control.ToteArrivedAtConvDropStation(this, Elevator, caseLoad, Multishuttle);       
                    Location1.Release();
                }
            }
        }

        /// <summary>
        /// A load has arrived at the second drop station point (Location 1) and it is the first of 2
        /// we can therefore assume that both loads have left the elevator
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="load"></param>
        public void sensor_OnEnter1(DematicSensor sender, Load load)
        {
            Case_Load caseLoad = load as Case_Load;
            if (caseLoad != null && Elevator.CurrentJobData !=null)
            {
                if (Elevator.CurrentJobData.JobMode == MultishuttleElevatorJobData.JobModes.Unload2)
                {
                    caseLoad.Case_Data.CurrentPosition = "D" + Multishuttle.AisleNo + DropPositionGroupSide + Multishuttle.POS1OUTFEED + Level;
                    if (TransportSection.Route.Loads.Count == 2) // this is the first load so there is another load behind
                    {
                        load.Stop();
                    }
                    if (Location2.Active)
                    {
                         Multishuttle.Control.ToteArrivedAtConvDropStation(this, Elevator, caseLoad, Multishuttle);
                    }                      
                }
            }
        }


        [Browsable(false)]
        public DematicMultiShuttle Multishuttle;
        [Browsable(false)]
        public MultiShuttleElevator Elevator;

        [Browsable(false)]
        public LevelHeight SavedLevel { get; set; }

        [Browsable(false)]
        public string DropPositionGroupSide { get; set; }

        [CategoryAttribute("Configuration")]
        [DescriptionAttribute("Level")]
        [DisplayName("Level")]
        public string Level
        {
            get { return SavedLevel.Level; }
            set { SavedLevel.Level = value; }
        }

        [CategoryAttribute("Configuration")]
        [DescriptionAttribute("Level Height in meter")]
        [DisplayName("Level Height (m.)")]
        public float LevelHeight
        {
            get { return LocalPosition.Y; }
            set
            {
                LocalPosition = new Vector3(LocalPosition.X, value, LocalPosition.Z);
                SavedLevel.Height = value;
            }
        }


        public override void NextRouteStatus_OnAvailableChanged(object sender, AvailableChangedEventArgs e)
        {
            CheckAndDoNextElevatorOutFeedJob();
        }

        #region ITransferLoad and related methods

        private ActionPoint enterPoint = new ActionPoint();
        private ActionPoint exitPoint = new ActionPoint();

        public ActionPoint EnterPoint
        {
            get { return enterPoint; }
        }

        public ActionPoint ExitPoint
        {
            get { return exitPoint; }
        }

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

        #endregion
      
    }

    public class OutfeedDropStationConveyorInfo : StraightAccumulationConveyorInfo
    {
        public Experior.Core.Parts.FixPoint.Types type;
        public DematicMultiShuttle parent;
        public MultiShuttleElevator elevator;
    }
}