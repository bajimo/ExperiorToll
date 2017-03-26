using Experior.Dematic.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using SimonPG.WaitCore;
using Experior.Catalog.Dematic.Storage.Assemblies;
using MultiShuttle.Assemblies;

namespace Experior.Catalog.Assemblies
{
    class MHEControl_FlowController : MHEControl
    {
        private FlowControllerInfo multiShuttleSimInfo;
        Dematic.Storage.Assemblies.MultiShuttle theMultishuttle;
        private static Random rand = new Random();
        private Elevator elevator;

        public MHEControl_FlowController(FlowControllerInfo info, Dematic.Storage.Assemblies.MultiShuttle cPoint)
        {
            theMultishuttle = cPoint;
            theMultishuttle.OnArrivedAtPickStationConvPosB += theMultishuttle_OnArrivedAtPickStationConvPosB;
            elevator = theMultishuttle.elevators["L01"];
            elevator.OnElevatorArrived += elevator_OnElevatorArrived;
        }

        void elevator_OnElevatorArrived(object sender, EventArgs e)
        {
            //throw new NotImplementedException();
        }

        /// <summary>
        /// Generate a random level
        /// </summary>
        /// <returns> String padded left with 0 if needed for a string length of 2</returns>
        private string GetRandomLevel()
        {
            return rand.Next(1, theMultishuttle.Levels + 1).ToString().PadLeft(2, '0');
        }

        void theMultishuttle_OnArrivedAtPickStationConvPosB(object sender, MultiShuttle.Assemblies.PickDropStationArrivalEventArgs e)
        {
            var locB = theMultishuttle.ConveyorLocations.Find(x => x.LocName == e._locationName);
            PickStationConveyor psConv = locB.Parent.Parent.Parent as PickStationConveyor;

            if (psConv.TransportSection.Route.Loads.Count == 1)
            {
                ElevatorTask elevatorTask = new ElevatorTask()
                {
                    BarcodeLoadB = e._caseLoad.SSCCBarcode,
                    DestinationLoadB = string.Format("01L{0}IB", GetRandomLevel()),
                    SourceLoadB = e._locationName,
                    LoadCycle = Cycle.Single,
                    UnloadCycle = Cycle.Single,
                    Flow = TaskType.Infeed
                };
                e._elevator.ElevatorTasks.Add(elevatorTask);
            }

            new EventWaiter(TestFunc);
        }

        IEnumerable<bool> TestFunc(EventWaiter waiter)
        { 
            
            Console.WriteLine("...Process A..."); // ...Process A...

            Elevator elv = theMultishuttle.elevators["L01"];

            yield return waiter.Wait(elv, "OnElevatorArrived"); // [C]

            Console.WriteLine("...Process B..."); // ...Process B...
        }

    }

    [Serializable]
    [XmlInclude(typeof(FlowControllerInfo))]
    public class FlowControllerInfo : ProtocolInfo
    {

    }
}
