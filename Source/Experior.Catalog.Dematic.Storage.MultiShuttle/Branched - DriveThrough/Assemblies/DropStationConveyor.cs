using Experior.Catalog.Dematic.Case.Components;
using Experior.Core.Loads;
using Experior.Core.Routes;
using Experior.Dematic.Base;
using Microsoft.DirectX;
using System.ComponentModel;
using System.Linq;
using System.Drawing;
using Experior.Dematic.Base.Devices;

namespace Experior.Catalog.Dematic.Storage.MultiShuttle.Assemblies
{
    public class DropStationConveyor : StraightAccumulationConveyor, IPickDropConv
    {
        private DropStationConveyorInfo dropStationConveyorInfo;
        public DematicActionPoint Location1, Location2;

        public DropStationConveyor(DropStationConveyorInfo info) : base(info)
        {
            dropStationConveyorInfo                 = info;
            ParentMultishuttle = info.parentMultiShuttle;
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
            foreach (AccumulationSensor sensor in sensors)
            {
                int convPosName;
                if (int.TryParse(sensor.sensor.Name, out convPosName) && convPosName == 0)
                {
                    sensor.sensor.OnEnter += sensor_OnEnter1;
                    sensor.sensor.OnLeave += sensor_OnLeave;
                    Location1         = sensor.sensor;
                    Location1.LocName = string.Format("{0}{1}{2}{3}{4}", AisleNumber.ToString().PadLeft(2, '0'), (char)Side, level, (char)ConveyorTypes.Drop, "B");
                    ParentMultishuttle.ConveyorLocations.Add(Location1);

                }
                else if (int.TryParse(sensor.sensor.Name, out convPosName) && convPosName == 1)
                {
                    sensor.sensor.OnEnter += sensor_OnEnter2;
                    sensor.sensor.OnLeave += sensor_OnLeave;
                    Location2         = sensor.sensor;
                    Location2.LocName = string.Format("{0}{1}{2}{3}{4}", AisleNumber.ToString().PadLeft(2, '0'), (char)Side, level, (char)ConveyorTypes.Drop, "A");

                    ParentMultishuttle.ConveyorLocations.Add(Location2);
                }
            }
        }

        public override void Reset()
        {
            foreach (Load l in TransportSection.Route.Loads)
            {
                l.Dispose();
                dropStationConvEmpty = true;
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
            SetDropStationOccupiedStatus();

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

                ParentMultishuttle.ArrivedAtDropStationConvPosA(new PickDropStationArrivalEventArgs(Location2.LocName, (Case_Load)load, Elevator, 2));
            }
            else
            {
                ParentMultishuttle.ArrivedAtDropStationConvPosA(new PickDropStationArrivalEventArgs(Location1.LocName, (Case_Load)load, Elevator, 1));
            }
        }

        public void sensor_OnEnter1(DematicSensor sender, Load load)
        {
            SetDropStationOccupiedStatus();

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

        new void sensor_OnLeave(DematicSensor sender, Load load)
        {
            if (Elevator.CurrentTask != null)
            {
                if (Elevator.ElevatorConveyor.Route.Loads.Count == 0 && (load.Identification == Elevator.CurrentTask.LoadA_ID || load.Identification == Elevator.CurrentTask.LoadB_ID))
                {
                    Elevator.CurrentTask = null;
                }
            }
            SetDropStationOccupiedStatus();
        }

        private void SetDropStationOccupiedStatus()
        {
            //Set the empty status of the drop station, and trigger an event when the drop station becomes available
            bool triggerEvent = false;
            if (!DropStationConvEmpty && TransportSection.Route.Loads.Count == 0)
            {
                triggerEvent = true;
            }

            if (TransportSection.Route.Loads.Count == 0)
            {
                dropStationConvEmpty = true;
            }
            else
            {
                dropStationConvEmpty = false;
            }

            if (triggerEvent)
            {
                Elevator.SetNewElevatorTask();
            }
        }


        [Browsable(false)]
        public MultiShuttle ParentMultishuttle;
        [Browsable(false)]
        public Elevator Elevator;

        [Browsable(false)]
        public LevelID Level
        {
            get { return dropStationConveyorInfo.level; }
            set { dropStationConveyorInfo.level = value; }
        }

        [Browsable(false)]
        public LevelID SavedLevel { get; set; }

        [Browsable(false)]
        public string DropPositionGroupSide { get; set; }

        private bool dropStationConvEmpty = true;
        [Browsable(false)]
        public bool DropStationConvEmpty { get { return dropStationConvEmpty; } }

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

        public RackSide Side{ get; set; }

        public int AisleNumber { get; set; }

        #region ITransferLoad and related methods

        private ActionPoint enterPoint = new ActionPoint();
        private ActionPoint exitPoint = new ActionPoint();

        #endregion
      
    }

    public class DropStationConveyorInfo : StraightAccumulationConveyorInfo, IPickDropConvInfo
    {
        public MultiShuttle parentMultiShuttle { get; set; }
        public Elevator elevator { get; set; }
        public LevelID level { get; set; }

        float IPickDropConvInfo.thickness
        {
            get { return thickness; }
            set { thickness = value; }
        }

        Color IPickDropConvInfo.color
        {
            get { return color; }
            set { color = value; }
        }

        string IPickDropConvInfo.name
        {
            get { return name; }
            set { name = value; }
        }
    }
}