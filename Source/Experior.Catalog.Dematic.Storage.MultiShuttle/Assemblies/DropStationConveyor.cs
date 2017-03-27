using Experior.Catalog.Dematic.Case;
using Experior.Catalog.Dematic.Case.Components;
using Experior.Catalog.Devices;
using Experior.Core.Loads;
using Experior.Core.Routes;
using Experior.Dematic.Base;
using Microsoft.DirectX;
using System.ComponentModel;
using System.Linq;

namespace Experior.Catalog.Dematic.Storage.MultiShuttle.Assemblies
{
    public class DropStationConveyor : StraightAccumulationConveyor//, ITransferLoad
    {
        public DropStationConveyorInfo Info;
        public DematicActionPoint Location1, Location2;
       // public ActionPoint EnterPoint = new ActionPoint();
        public int AisleNumber;

        public float Height;
        public RackSide Side;

        public DropStationConveyor(DropStationConveyorInfo info) : base(info)
        {
            Info                 = info;
            ParentMultishuttle   = info.parent;
            Elevator             = info.elevator;
            LineFullPosition     = 1;
            LineAvailableTimeout = 1;
        }

        /// <summary>
        /// As the accumilation conveyor length is not set conventually it is set via positions, outfeedsection
        /// infeedsection and AccPitch. We have to wait until all these are set. So we need this method to place 
        /// transition points.
        /// </summary>
        public void ConvLocationConfiguration(string level)
        {
           // TransportSection.Route.InsertActionPoint(ExitPoint, Length);
           // TransportSection.Route.InsertActionPoint(EnterPoint, 0);          
            foreach (AccumulationSensor sensor in sensors)
            {
                int convPosName;
                if (int.TryParse(sensor.sensor.Name, out convPosName) && convPosName == 0)
                {
                    sensor.sensor.OnEnter += sensor_OnEnter1;
                    sensor.sensor.OnLeave += sensor_OnLeave;
                    Location1         = sensor.sensor;
                    //Location1.LocName = string.Format("{0}{1}{2}{3}{4}", AisleNumber.ToString().PadLeft(2, '0'), (char)Side, Level.ToString().PadLeft(2, '0'), (char)ConveyorTypes.Drop, "B");
                    Location1.LocName = string.Format("{0}{1}{2}{3}{4}", AisleNumber.ToString().PadLeft(2, '0'), (char)Side, level, (char)ConveyorTypes.Drop, "B");
                    ParentMultishuttle.ConveyorLocations.Add(Location1);

                }
                else if (int.TryParse(sensor.sensor.Name, out convPosName) && convPosName == 1)
                {
                    sensor.sensor.OnEnter += sensor_OnEnter2;
                    sensor.sensor.OnLeave += sensor_OnLeave;
                    Location2         = sensor.sensor;
                    //Location2.LocName = string.Format("{0}{1}{2}{3}{4}", AisleNumber.ToString().PadLeft(2, '0'), (char)Side, Level.ToString().PadLeft(2, '0'), (char)ConveyorTypes.Drop, "A");
                    Location2.LocName = string.Format("{0}{1}{2}{3}{4}", AisleNumber.ToString().PadLeft(2, '0'), (char)Side, level, (char)ConveyorTypes.Drop, "A");

                    ParentMultishuttle.ConveyorLocations.Add(Location2);
                }
            }
          //  Location1.Visible = true;
        }

        public override void Reset()
        {
            foreach (Load l in TransportSection.Route.Loads)
            {
                l.Dispose();
            }

            base.Reset();
        }

        /// <summary>
        /// A single load has been dropped off by the elevator
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="load"></param>
        public void sensor_OnEnter2(DematicSensor sender, Load load) 
        {
            if (TransportSection.Route.Loads.Count == 2 ) 
            {
                string id = TransportSection.Route.Loads.ToList()[0].Identification;
                if (id != load.Identification)
                {
                    ((Case_Load)load).UserData = id + "," + load.Identification;
                }
                else
                {
                    ((Case_Load)load).UserData = load.Identification + "," + id;
                }

                ParentMultishuttle.ArrivedAtDropStationConvPosA(new PickDropStationArrivalEventArgs(Location2.LocName, (Case_Load)load, Elevator, 2));
            }
            else
            {
                ParentMultishuttle.ArrivedAtDropStationConvPosA(new PickDropStationArrivalEventArgs(Location1.LocName, (Case_Load)load, Elevator, 1));
            }


            //ParentMultishuttle.ArrivedAtDropStationConvPosA(new PickDropStationArrivalEventArgs(Location2.LocName, (Case_Load)load, Elevator, 1));
            
            Case_Load caseLoad = load as Case_Load;
            if (caseLoad != null)
            {
                //caseLoad.Case_Data.CurrentPosition = "D" + ParentMultishuttle.AisleNo + DropPositionGroupSide + ParentMultishuttle.POS2OUTFEED + Level;
                //if (Elevator.CurrentJobData.JobMode == MultishuttleElevatorJobData.JobModes.Unload1)
                //{                    
                //    ParentMultishuttle.Control.ToteArrivedAtConvDropStation(this, Elevator, caseLoad, ParentMultishuttle);
                //}
                //else if (Elevator.CurrentJobData.JobMode == MultishuttleElevatorJobData.JobModes.Unload2 && Location1.Active)
                //{
                //    ParentMultishuttle.Control.ToteArrivedAtConvDropStation(this, Elevator, caseLoad, ParentMultishuttle);       
                //    Location1.Release();
                //}
            }
        }

        public void sensor_OnEnter1(DematicSensor sender, Load load)
        {
            Case_Load caseLoad = load as Case_Load;

            if (TransportSection.Route.Loads.Count == 2)
            {
                string id = TransportSection.Route.Loads.ToList()[0].Identification;
                if (id != load.Identification)
                {
                    ((Case_Load)load).UserData = id + "," + load.Identification;
                }
                else
                {
                    ((Case_Load)load).UserData = load.Identification + "," + id;
                }

                ParentMultishuttle.ArrivedAtDropStationConvPosB(new PickDropStationArrivalEventArgs(Location2.LocName, (Case_Load)load, Elevator, 2));
            }
            else
            {
                ParentMultishuttle.ArrivedAtDropStationConvPosB(new PickDropStationArrivalEventArgs(Location1.LocName, (Case_Load)load, Elevator, 1));
            }
        }

        void sensor_OnLeave(DematicSensor sender, Load load)
        {
            if (Elevator.CurrentTask != null)
            {
                if (Elevator.ElevatorConveyor.Route.Loads.Count == 0 && (load.Identification == Elevator.CurrentTask.LoadA_ID || load.Identification == Elevator.CurrentTask.LoadB_ID))
                {
                    Elevator.CurrentTask = null;
                }
            }
        }

        [Browsable(false)]
        public MultiShuttle ParentMultishuttle;
        [Browsable(false)]
        public Elevator Elevator;

        [Browsable(false)]
        public LevelID SavedLevel { get; set; }

        [Browsable(false)]
        public string DropPositionGroupSide { get; set; }

        //[CategoryAttribute("Configuration")]
        //[DescriptionAttribute("Level")]
        //[DisplayName("Level")]
        //public string Level
        //{
        //    get { return SavedLevel.Level; }
        //    set { SavedLevel.Level = value; }
        //}

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


        ////public override void NextRouteStatus_OnAvailableChanged(object sender, RouteStatusChangedEventArgs e)
        ////{
        ////  //  CheckAndDoNextElevatorOutFeedJob();
        ////}

        #region ITransferLoad and related methods

        private ActionPoint enterPoint = new ActionPoint();
        private ActionPoint exitPoint = new ActionPoint();


        //public ActionPoint Location1
        //{
        //    get { return location1; }
        //    set { location1 = value; }
        //}

        //public ActionPoint Location2
        //{
        //    get { return location2; }
        //    set { location2 = value; }
        //}

        #endregion
      
    }

    public class DropStationConveyorInfo : StraightAccumulationConveyorInfo
    {
        public Experior.Core.Parts.FixPoint.Types type;
        public Elevator elevator;
        public MultiShuttle parent;
    }
}