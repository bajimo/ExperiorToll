using Experior.Core.Loads;
using Experior.Dematic.Base;
using System;

namespace Experior.Catalog.Dematic.Storage.MultiShuttle.Assemblies
{
    public class RackConveyorArrivalEventArgs : EventArgs
    {
        public readonly string _locationName;
        public readonly Case_Load _caseLoad;
        public readonly Elevator _elevator;
        public readonly RackConveyor _rackConveyor;
        public readonly Cycle? _UnloadCycle;

        public RackConveyorArrivalEventArgs(string locationName, Case_Load caseLoad, Elevator elevator, RackConveyor rackConveyor, Cycle? unloadcycle)
        {
            _locationName = locationName;
            _caseLoad = caseLoad;
            _elevator = elevator;
            _rackConveyor = rackConveyor;
            _UnloadCycle = unloadcycle;
        }
    }

    public class MultishuttleEvent : EventArgs
    {
        public MultiShuttle MultiShuttle { get; }

        public MultishuttleEvent(MultiShuttle multiShuttle)
        {
            MultiShuttle = multiShuttle;
        }
    }

    public class MultishuttleVehicleEvent : EventArgs
    {
        public MultiShuttle MultiShuttle { get; }
        public TrackVehicle Vehicle { get; }
        public ShuttleTask Task { get; }

        public MultishuttleVehicleEvent(MultiShuttle multiShuttle, TrackVehicle vehicle, ShuttleTask task)
        {
            MultiShuttle = multiShuttle;
            Vehicle = vehicle;
            Task = task;
        }
    }

    //public class RackConveyorArrivalEventArgs : EventArgs
    //{
    //    public readonly string _locationName;
    //    public readonly Case_Load _caseLoad;

    //    public RackConveyorArrivalEventArgs(string locationName, Case_Load caseLoad)
    //    {
    //        _locationName = locationName;
    //        _caseLoad = caseLoad;
    //    }
    //}
    public class LoadCreatedEventArgs : EventArgs
    {
        public readonly Case_Load _load;
        public LoadCreatedEventArgs(Case_Load load)
        {
            _load = load;
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

    public class ArrivedOnElevatorEventArgs : EventArgs
    {
        public readonly Task _task;
        public readonly Case_Load _loadA, _loadB;
        public readonly Elevator _elevator;
        public readonly string _locationName;

        public ArrivedOnElevatorEventArgs(Task task, Case_Load loadA, Case_Load loadB, Elevator elevator, string locationName)
        {
            _task         = task;
            _loadA        = loadA;
            _loadB        = loadB;
            _elevator     = elevator;
            _locationName = locationName;
        }
    }

    public class ElevatorTasksStatusChangedEventArgs : EventArgs
    {
        public readonly Elevator _elevator;
        public ElevatorTasksStatusChangedEventArgs(Elevator elevator)
        {
            _elevator = elevator;
        }
    }

    public class PickDropStationArrivalEventArgs : EventArgs
    {
        public readonly string _locationName;
        public readonly Case_Load _caseLoad;
        public readonly Elevator _elevator;
        public readonly int _numberOfLoads;
        public PickDropStationArrivalEventArgs(string locationName, Case_Load caseLoad, Elevator elevator, int NumberOfLoads)
        {
            _locationName = locationName;
            _caseLoad = caseLoad;
            _elevator = elevator;
            _numberOfLoads = NumberOfLoads;
        }
    }

    public class DropStationConvClearEventArgs : EventArgs
    {
        public readonly string _dropStationName;
        public readonly Elevator _elevator;
        public DropStationConvClearEventArgs(string dropStationName, Elevator elevator)
        {
            _dropStationName = dropStationName;
            _elevator = elevator;
        }
    }
}