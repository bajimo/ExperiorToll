using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Drawing;
using System.Threading.Tasks;
using Experior.Catalog.Dematic.Storage.Assemblies;
using Experior.Core.Assemblies;
using Experior.Core.Loads;
using MultiShuttle.Annotations;
using Environment = Experior.Core.Environment;
using System.Collections.Specialized;
using Experior.Dematic.Base;
using Experior.Catalog.Dematic.Case.Components;

namespace MultiShuttle.Assemblies
{
    public class Task{}

    public class ElevatorTask : Task
    {
        public Elevator Elevator { get; set; }
        public string BarcodeLoadA { get; set; }
        public string BarcodeLoadB { get; set; }
        public string SourceLoadA { get; set; }
        public string SourceLoadB { get; set; }
        public string DestinationLoadA { get; set; }
        public string DestinationLoadB { get; set; }
        public Cycle LoadCycle { get; set; }
        public Cycle UnloadCycle { get; set; }
        public TaskType Flow { get; set; }

        public StraightConveyor SourceLoadAConv, SourceLoadBConv, DestinationLoadAConv, DestinationLoadBConv;
    }

    public class ShuttleTask : Task
    {
        public int Level { get; set; }
        public string Barcode { get; set; }
        public float LoadLength { get; set; }
        public float LoadWidth { get; set; }
        public float LoadHeight { get; set; }
        public float LoadWeight { get; set; }
        public Color LoadColor { get; set; }
        public string Source { get; set; }  //Can be either rack conveyor or rack bin location
        public string Destination { get; set; } //sxxxyydd, s=Side (L or R), xxx=Xlocation (e.g. 012), yy=Level (e.g. 02), dd=Depth (01 or 02)
        public float SourcePosition, DestPosition;
      
    }

    //public class RackConveyorOutFeedArrivalEventArgs : EventArgs
    //{
    //    public readonly string _locationName;
    //    public readonly Case_Load _caseLoad;

    //    public RackConveyorInfeedArrivalEventArgs(string locationName, Case_Load caseLoad)
    //    {
    //        _locationName = locationName;
    //        _caseLoad = caseLoad;
    //    }
    //}


    public class RackConveyorOutfeedArrivalEventArgs : EventArgs
    {
        public readonly string _locationName;
        public readonly Case_Load _caseLoad;
        public readonly Elevator _elevator;

        public RackConveyorOutfeedArrivalEventArgs(string locationName, Case_Load caseLoad, Elevator elevator)
        {
            _locationName = locationName;
            _caseLoad = caseLoad;
            _elevator = elevator;
        }
    }

    public class RackConveyorArrivalEventArgs : EventArgs 
    {
        public readonly string _locationName;
        public readonly Case_Load _caseLoad;

        public RackConveyorArrivalEventArgs(string locationName, Case_Load caseLoad)
        {
            _locationName = locationName;
            _caseLoad = caseLoad;
        }
    }

    public class TaskEventArgs : EventArgs
    {
        public readonly Task _task;
        public readonly Load _load;
        public TaskEventArgs(Task task, Load load)
        {
            _task = task;
            _load = load;
        }
    }

    public class PickDropStationArrivalEventArgs : EventArgs
    {
        public readonly string _locationName;
        public readonly Case_Load _caseLoad;
        public readonly Elevator _elevator;
        public PickDropStationArrivalEventArgs(string locationName, Case_Load caseLoad, Elevator elevator)
        {
            _locationName = locationName;
            _caseLoad = caseLoad;
            _elevator = elevator;
        }
    }

    public interface IMultiShuttleControl
    {
        //ObservableCollection<ElevatorTask> ElevatorTasks { get; set; }
        //ObservableCollection<ShuttleTask> ShuttleTasks { get; set; }

    
        // Elevator Conveyor
        event EventHandler<TaskEventArgs> OnArrivedAtElevatorConvPosA;
        event EventHandler<TaskEventArgs> OnArrivedAtElevatorConvPosB;

        // Infeed Rack Conveyor
        event EventHandler<RackConveyorArrivalEventArgs> OnArrivedAtInfeedRackConvPosA;
        event EventHandler<RackConveyorArrivalEventArgs> OnArrivedAtInfeedRackConvPosB;

        // Outfeed Rack Conveyor
        event EventHandler<RackConveyorOutfeedArrivalEventArgs> OnArrivedAtOutfeedRackConvPosA;
        event EventHandler<RackConveyorOutfeedArrivalEventArgs> OnArrivedAtOutfeedRackConvPosB;

        // Pick Station Conveyor
        event EventHandler<PickDropStationArrivalEventArgs> OnArrivedAtPickStationConvPosA;
        event EventHandler<PickDropStationArrivalEventArgs> OnArrivedAtPickStationConvPosB;
        event EventHandler<PickDropStationArrivalEventArgs> OnLoadTransferingToPickStation;

        // Drop Station Conveyor
        event EventHandler<PickDropStationArrivalEventArgs> OnArrivedAtDropStationConvPosA;
        event EventHandler<PickDropStationArrivalEventArgs> OnArrivedAtDropStationConvPosB;

        // Rack location
        event EventHandler<RackConveyorArrivalEventArgs> OnArrivedAtRackLocation;

        // Drop Station Conveyor
        bool DropStationAvaiable { get; set; }

        // Shuttle
        event EventHandler<TaskEventArgs> OnArrivedOntoShuttle;
    }
}