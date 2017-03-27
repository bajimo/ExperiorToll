using Experior.Catalog.Dematic.Case.Components;
using Experior.Core.Loads;
using Experior.Core.Routes;
using Experior.Dematic;
using Experior.Dematic.Base;
using Experior.Dematic.Base.Devices;
using Microsoft.DirectX;
using System;

namespace Experior.Catalog.Dematic.Storage.MultiShuttle.Assemblies
{
    public class RackConveyor : StraightConveyor
    {
        private MultiShuttleDirections rackConveyorType;
        public DematicActionPoint LocationA = new DematicActionPoint();
        public DematicActionPoint LocationB = new DematicActionPoint();
        public Elevator Elevator;
        public int Level;
        //public float Height;
        public RackSide Side;
        public int AisleNumber;

        public RackConveyor(RackConveyorInfo info) : base(info)
        {
            Selectable = true;
            RackConveyorType = info.RackType;
            Length = info.length;
            Color = info.color;

            Leaving.Name = "ExitPoint";
            Entering.Name = "EnterPoint";
            Leaving.OnEnter += Leaving_OnEnter;
            ParentMultiShuttle = info.parentMultishuttle;
        }

        public override void Scene_OnLoaded()
        {
            if (ParentMultiShuttle.MultiShuttleinfo.DriveThrough)
            {
                Elevator = ParentMultiShuttle.elevators.Find(x => x.Side == Side);
            }
            base.Scene_OnLoaded();
        }

        void Leaving_OnEnter(ActionPoint sender, Core.Loads.Load load)
        {
            load.Switch(Elevator.ElevatorConveyor.Entering);
        }

        public void ConfigureRackConveyor(Elevator elevator, float zCoord, int level, RackSide side) //TODO remove xoffet
        {
            Level = level;

            if (ParentMultiShuttle.MultiShuttleinfo.DriveThrough)
            {
                Side = side;
            }
            else
            {
                Elevator = elevator;
            }

            TransportSection.Route.InsertActionPoint(LocationA, Length / 2 - Length / 4);
            TransportSection.Route.InsertActionPoint(LocationB, Length / 2 + Length / 4);

            //LocationA.Visible = true;
            //LocationB.Visible = true;
            LocationA.LocName = string.Format("{0}{1}{2}{3}{4}", ParentMultiShuttle.AisleNumber.ToString().PadLeft(2, '0'), (char)side, level.ToString().PadLeft(2, '0'), (char)RackConveyorType, "A");
            LocationB.LocName = string.Format("{0}{1}{2}{3}{4}", ParentMultiShuttle.AisleNumber.ToString().PadLeft(2, '0'), (char)side, level.ToString().PadLeft(2, '0'), (char)RackConveyorType, "B");

            ParentMultiShuttle.ConveyorLocations.Add(LocationA);
            ParentMultiShuttle.ConveyorLocations.Add(LocationB);
            //Position the rack conveyors based on the position of the shuttles
            if (ParentMultiShuttle.MultiShuttleinfo.DriveThrough)
            {
                float lengthSide = Length;
                if (rackConveyorType == MultiShuttleDirections.Infeed)
                {
                    lengthSide = Length * -1;
                }
                LocalPosition = ParentMultiShuttle.shuttlecars[level].LocalPosition + new Vector3(lengthSide + ParentMultiShuttle.ElevatorOffset /*+ xoffset*/, 0, zCoord);
            }
            else  //outfeed wll always travel in the same direction so point the conveyor in the correct direction and not reverse the conveyor
            {
                Height = ParentMultiShuttle.shuttlecars[level].LocalPosition.Y;
                LocalPosition = ParentMultiShuttle.shuttlecars[level].LocalPosition + new Vector3(ParentMultiShuttle.MultiShuttleinfo.raillength / 2 - ParentMultiShuttle.shuttlecars[level].Vehicle.DestAP.Distance + LocationA.Distance, 0, zCoord);
            }

            if (RackConveyorType == MultiShuttleDirections.Outfeed && !ParentMultiShuttle.MultiShuttleinfo.DriveThrough)
            {
               LocalYaw = (float)Math.PI;
            }
            ParentMultiShuttle.RackConveyors.Add(this);
            LocationA.OnEnter += rackConvLocA_Enter;
            LocationB.OnEnter += rackConvLocB_Enter;

        }


        public void rackConvLocA_Enter(ActionPoint sender, Core.Loads.Load load)
        {
            if (LocationA.LocName.ConvType() == ConveyorTypes.OutfeedRack)
            {
                Elevator.ParentMultiShuttle.ArrivedAtOutfeedRackConvPosA(new RackConveyorArrivalEventArgs(LocationA.LocName, (Case_Load)load, Elevator, this, null));
                Elevator.ParentMultiShuttle.shuttlecars[Level].CurrentTask = null; //If outfeed then shuttle must have finished its current task

                if (TransportSection.Route.Loads.Count == 2)
                {
                    load.Stop();
                }
                else
                {
                    load.Release();
                }
            }
            else if (LocationA.LocName.ConvType() == ConveyorTypes.InfeedRack)
            {
                Cycle? unloadCycle = null;

                if (RelevantElevatorTask(LocationA, LocationB))
                {
                    unloadCycle = Elevator.CurrentTask.UnloadCycle;
                }

                TryClearElevatorTask(Elevator.CurrentTask);
                Elevator.ParentMultiShuttle.ArrivedAtInfeedRackConvPosA(new RackConveyorArrivalEventArgs(LocationA.LocName, (Case_Load)load, Elevator, this, unloadCycle));

                if (TransportSection.Route.Loads.Count > 1)
                {
                    load.Stop();
                }

                if (Elevator.ElevatorConveyor.TransportSection.Route.Loads.Count > 0) //release the elevator load from A 
                {
                    Elevator.ElevatorConveyor.UnLoading = true;
                    Elevator.ElevatorConveyor.LocationA.Release();
                }
            }
        }

        public void rackConvLocB_Enter(ActionPoint sender, Core.Loads.Load load)
        {
            if (LocationB.LocName.ConvType() == ConveyorTypes.InfeedRack)
            {
                load.Stop();
                Cycle? unloadCycle = null;

                if (RelevantElevatorTask(LocationA, LocationB))
                {
                    unloadCycle = Elevator.CurrentTask.UnloadCycle;
                }

                TryClearElevatorTask(Elevator.CurrentTask);
                Elevator.ParentMultiShuttle.ArrivedAtInfeedRackConvPosB(new RackConveyorArrivalEventArgs(LocationB.LocName, (Case_Load)load, Elevator, this, unloadCycle));
            }
            else if (LocationB.LocName.ConvType() == ConveyorTypes.OutfeedRack) //don't care what loc we use to find the conveyor
            {
                if (!RelevantElevatorTask(load) || (RelevantElevatorTask(load) && Elevator.CurrentTask.NumberOfLoadsInTask == 1 && Elevator.Lift.Route.Motor.Running))
                {
                    //If no task then stop, if a relevant task then must be a double load onto the elevator and this is the second load so don't stop
                    //Or the task has been created when load was at A and therefore it is relevant
                    load.Stop();
                    //When the load arrives at the front position also see if the elevator wants to select a new task as it will not select a sequenced task unless it is at the front position
                    Elevator.SetNewElevatorTask();
                }
                if (Elevator.ParentMultiShuttle.shuttlecars[Level].CurrentTask == null)
                {
                    Elevator.ParentMultiShuttle.shuttlecars[Level].SetNewShuttleTask();
                }
                Elevator.ParentMultiShuttle.ArrivedAtOutfeedRackConvPosB(new RackConveyorArrivalEventArgs(LocationB.LocName, (Case_Load)load, Elevator, this, null));
            }
        }

        /// <summary>
        /// Checks the loads at A nd B to see if they are relevant to the task.
        /// Can be called if loads are not active and can be called if there is no current task
        /// </summary>
        /// <returns>True if loads at locations are relevant to task</returns>
        private bool RelevantElevatorTask(ActionPoint LocA, ActionPoint LocB)
        {
            if (Elevator.CurrentTask != null && Elevator.CurrentTask.RelevantElevatorTask(LocationA, LocationB))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Checks a load to see if it is relevant to task, can be called if task is null
        /// </summary>
        private bool RelevantElevatorTask(Load load)
        {
            if (Elevator.CurrentTask != null && Elevator.CurrentTask.RelevantElevatorTask(load))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Trys to clear the elevator task if it is now complete
        /// </summary>
        private void TryClearElevatorTask(ElevatorTask task)
        {
            if (task == null) return;

            if (task.UnloadCycle == Cycle.Double && LocationA.Active && LocationB.Active)
            {
                //if (Elevator.ElevatorConveyor.Route.Loads.Count == 0 && RelevantElevatorTask(task)) //No loads on the elevator and the loads at location A and Location B are part of the current task
                if (Elevator.ElevatorConveyor.Route.Loads.Count == 0 && RelevantElevatorTask(LocationA, LocationB)) //No loads on the elevator and the loads at location A and Location B are part of the current task
                {
                    Elevator.CurrentTask = null;  // Elevator task is now complete
                }
            }
            //else if (task.UnloadCycle == Cycle.Single && Elevator.ElevatorConveyor.Route.Loads.Count == 0 && RelevantElevatorTask(task))
            else if (task.UnloadCycle == Cycle.Single && Elevator.ElevatorConveyor.Route.Loads.Count == 0 && RelevantElevatorTask(LocationA, LocationB))
            {
                //if the elevator task is double pick and single drop and the elevator has no load then kill the elevator task or single pick and single drop in both cases the elevator conveyor will be empty
                Elevator.CurrentTask = null;
            }
        }

        public MultiShuttle ParentMultiShuttle { get; set; }

        public override void Reset()
        {

            foreach (Load l in TransportSection.Route.Loads)
            {
                l.Dispose();
            }


            base.Reset();
        }

        public MultiShuttleDirections RackConveyorType
        {
            get { return rackConveyorType; }
            set { rackConveyorType = value; }
        }

    }

    public class RackConveyorInfo : StraightConveyorInfo
    {
        public MultiShuttleDirections RackType;
        public MultiShuttle parentMultishuttle;
    }

}
