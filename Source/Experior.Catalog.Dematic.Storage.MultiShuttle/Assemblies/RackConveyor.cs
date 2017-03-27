using Experior.Catalog.Dematic.Case.Components;
using Experior.Catalog.Devices;
using Experior.Core.Loads;
using Experior.Core.Routes;
using Experior.Dematic;
using Experior.Dematic.Base;
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
        public float Height;
        public RackSide Side;
        public int AisleNumber;

        public RackConveyor(RackConveyorInfo info) : base(info)
        {
            Selectable = true;
            RackConveyorType = info.RackType;
            Length           = info.length;
            Color            = Core.Environment.Scene.DefaultColor;
           
            Leaving.Name  = "ExitPoint";
            Entering.Name = "EnterPoint";
            Leaving.OnEnter += Leaving_OnEnter;                     
        }

        void Leaving_OnEnter(ActionPoint sender, Core.Loads.Load load)
        {
            load.Switch(Elevator.ElevatorConveyor.Entering);
        }

        public void ConfigureRackConveyor(Elevator elevator, float zCoord, int level, float xoffset)
        {
            Elevator = elevator;
            MultiShuttle ParentMultiShuttle = Elevator.ParentMultiShuttle;
               
            if (ParentMultiShuttle.MultiShuttleinfo.MultiShuttleDriveThrough)
            {
                LocalPosition = ParentMultiShuttle.shuttlecars[level].LocalPosition + new Vector3(-Length + ParentMultiShuttle.DriveThroughElevatorOffset + xoffset, 0, zCoord);
            }
            else  //outfeed wll always travel in the same direction so point the conveyor in the correct direction and not reverse the conveyor
            {
                Height = ParentMultiShuttle.shuttlecars[level].LocalPosition.Y;
                LocalPosition = ParentMultiShuttle.shuttlecars[level].LocalPosition + new Vector3(ParentMultiShuttle.MultiShuttleinfo.raillength / 2 - Length / 2 + xoffset, /*0.025f*/0, zCoord);
            }

            if (RackConveyorType == MultiShuttleDirections.Outfeed)
            {
                LocalYaw = (float)Math.PI;
            }
            //    TransportSection.Route.InsertActionPoint(LocationA, Length/ 2 + Length / 4);
            //    TransportSection.Route.InsertActionPoint(LocationB, Length/ 2 - Length / 4);
            //}
            //else if (RackConveyorType == MultiShuttleDirections.Infeed)
            //{
                TransportSection.Route.InsertActionPoint(LocationA, Length / 2 - Length / 4);
                TransportSection.Route.InsertActionPoint(LocationB, Length / 2 + Length / 4);
           // }
            //LocationA.Visible = true;
            //LocationB.Visible = true;
            LocationA.LocName = string.Format("{0}{1}{2}{3}{4}", Elevator.AisleNumber.ToString().PadLeft(2, '0'), (char)Elevator.Side, level.ToString().PadLeft(2, '0'), (char)RackConveyorType, "A");
            LocationB.LocName = string.Format("{0}{1}{2}{3}{4}", Elevator.AisleNumber.ToString().PadLeft(2, '0'), (char)Elevator.Side, level.ToString().PadLeft(2, '0'), (char)RackConveyorType, "B");

            ParentMultiShuttle.ConveyorLocations.Add(LocationA);
            ParentMultiShuttle.ConveyorLocations.Add(LocationB);

            Level = level;
            Elevator = elevator;
           // RackName = rackname;
           // ms.RackConveyors.Add(rackname, this);
            ParentMultiShuttle.RackConveyors.Add(this);
            LocationA.OnEnter += rackConvLocA_Enter;
            LocationB.OnEnter += rackConvLocB_Enter;
            //Location1.UserData = elevator;
            //Location2.UserData = elevator;

            //if (ms.MultiShuttleDriveThrough)
            //{
            //    RackConveyorInfo rackConvInfo = new RackConveyorInfo();

            //    //rackConvInfo.length = RackConveyorLength;
            //    //rackConvInfo.width = RackConveyorWidth;
            //   // conv = new RackConveyor(rackConvInfo);
            //    //ElevatorConveyor = new RackConveyor(Color.Gray, RackConveyorLength, RackConveyorWidth);
            //    //AddPart(ElevatorConveyor);
            //    AddAssembly(this);
            //    ConvRoute.Motor.Speed = ms.ConveyorSpeed;
            //    LocalPosition = ms.shuttlecars[level].LocalPosition + new Vector3(Length + ms.DriveThroughElevatorOffset + xoffset, 0, zCoord);
            //    LocalYaw = (float)Math.PI;
            //    _level = level.ToString();
            //    _level = _level.PadLeft(2, '0');

            //    if (rackConveyorType == MultiShuttleDirections.Infeed)
            //    {
            //        if (mSside == "L")
            //        {
            //            name1 = ms.FrontLeftOutfeedRackGroupName + mSside + ms.POS1OUTFEED + _level; // Front, Left, pos 001, level level
            //            name2 = ms.FrontLeftOutfeedRackGroupName + mSside + ms.POS2OUTFEED + _level; // Front, Left, pos 001, level level
            //            rackname = ms.FrontLeftOutfeedRackGroupName + mSside + _level;
            //        }
            //        else
            //        {
            //            name1 = ms.FrontRightOutfeedRackGroupName + mSside + ms.POS1OUTFEED + _level; // Front, Left, pos 001, level level
            //            name2 = ms.FrontRightOutfeedRackGroupName + mSside + ms.POS2OUTFEED + _level; // Front, Left, pos 001, level level
            //            rackname = ms.FrontRightOutfeedRackGroupName + mSside + _level;
            //        }
            //    }
            //    else
            //    {
            //        if (mSside == "L")
            //        {
            //            name1 = ms.FrontLeftInfeedRackGroupName + mSside + ms.POS1 + _level; // Front, Left, pos 001, level level
            //            name2 = ms.FrontLeftInfeedRackGroupName + mSside + ms.POS2 + _level; // Front, Left, pos 001, level level'
            //            rackname = ms.FrontLeftInfeedRackGroupName + mSside + _level;
            //        }
            //        else
            //        {
            //            name1 = ms.FrontRightInfeedRackGroupName + mSside + ms.POS1 + _level; // Front, Left, pos 001, level level
            //            name2 = ms.FrontRightInfeedRackGroupName + mSside + ms.POS2 + _level; // Front, Left, pos 001, level level
            //            rackname = ms.FrontRightInfeedRackGroupName + mSside + _level;
            //        }
            //    }

            //    if (rackConveyorType == MultiShuttleDirections.Infeed)
            //    {
            //        ConvRoute.Motor.Backward();
            //        //ElevatorConveyor.Exit = ElevatorConveyor.Route.InsertActionPoint(0);
            //        //ElevatorConveyor.Exit.OnEnter += new ActionPoint.EnterEvent(Exit_Enter);
            //        //ElevatorConveyor.RackConveyorType = RackConveyor.RackConveyorTypes.Outfeed;
            //    }
            //    Location1 = ConvRoute.InsertActionPoint(Length / 2 + Length / 4 + 0.01f);
            //    Location1.Name = name1;
            //    Location2 = ConvRoute.InsertActionPoint(Length / 2 - Length / 4);
            //    Location2.Name = name2;
            //    ms.conveyorlocations.Add(Location1.Name, Location1);
            //    ms.conveyorlocations.Add(Location2.Name, Location2);
            //    Level = _level;
            //    Elevator = elevator;
            //    RackName = rackname;
            //    ms.RackConveyors.Add(rackname, this);
            //    ms.RackConveyorsList.Add(this);
            //    Location1.OnEnter += rackConvLoc1_Enter;
            //    Location2.OnEnter += rackConvLoc2_Enter;
            //    Location1.UserData = elevator;
            //    Location2.UserData = elevator;

            //}
        }

        public void rackConvLocB_Enter(ActionPoint sender, Core.Loads.Load load)
        {
            if (LocationB.LocName.ConvType() == ConveyorTypes.InfeedRack)
            {
                load.Stop();
                Cycle? unloadCycle = null;

                if (RelevantElevatorTask(LocationA,LocationB))
                {
                    unloadCycle = Elevator.CurrentTask.UnloadCycle;
                }

                Elevator.ParentMultiShuttle.ArrivedAtInfeedRackConvPosB(new RackConveyorArrivalEventArgs(LocationB.LocName, (Case_Load)load, Elevator, this, unloadCycle));
                TryClearElevatorTask(Elevator.CurrentTask);

            }
            else if (LocationB.LocName.ConvType() == ConveyorTypes.OutfeedRack) //don't care what loc we use to find the conveyor
            {
                if (!RelevantElevatorTask(load)) //If no task then stop, if a relevant task then must be a double load onto the elevator and this is the second load so don't stop

                //if (Elevator.CurrentTask == null || (Elevator.CurrentTask != null && !Elevator.CurrentTask.RelevantElevatorTask(load))) //If no task then stop, if a relevant task then must be a double load onto the elevator and this is the second load so don't stop
                {
                    load.Stop();
                }

                Elevator.ParentMultiShuttle.ArrivedAtOutfeedRackConvPosB(new RackConveyorArrivalEventArgs(LocationA.LocName, (Case_Load)load, Elevator, this, null));
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
            else if (task.UnloadCycle == Cycle.Single && Elevator.ElevatorConveyor.Route.Loads.Count == 0 && RelevantElevatorTask(LocationA,LocationB))
            {
                //if the elevator task is double pick and single drop and the elevator has no load then kill the elevator task or single pick and single drop in both cases the elevator conveyor will be empty
                Elevator.CurrentTask = null; 
            }
        }

        public void rackConvLocA_Enter(ActionPoint sender, Core.Loads.Load load)
        {
            if (LocationA.LocName.ConvType() == ConveyorTypes.OutfeedRack)
            {
                Elevator.ParentMultiShuttle.ArrivedAtOutfeedRackConvPosA(new RackConveyorArrivalEventArgs(LocationA.LocName, (Case_Load)load, Elevator,this,null));
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

                //if (RelevantElevatorTask(Elevator.CurrentTask))
                if (RelevantElevatorTask(LocationA,LocationB))
                {
                    unloadCycle = Elevator.CurrentTask.UnloadCycle;
                }

                Elevator.ParentMultiShuttle.ArrivedAtInfeedRackConvPosA(new RackConveyorArrivalEventArgs(LocationA.LocName, (Case_Load)load, Elevator, this, unloadCycle));
                TryClearElevatorTask(Elevator.CurrentTask);

                if (TransportSection.Route.Loads.Count > 1)
                {
                    load.Stop();                  
                }

                if (Elevator.ElevatorConveyor.TransportSection.Route.Loads.Count > 0) //release the elevator load from A 
                {
                    Elevator.ElevatorConveyor.UnLoading = true;
                    Elevator.ElevatorConveyor.LocationA.Release();
                    //Elevator.ReleaseLocationAFFS(Elevator.ElevatorConveyor.LocationA.ActiveLoad, "RackConveyor 309");
                }
              
                //if ((Elevator.CurrentTask != null && Elevator.CurrentTask.UnloadCycle == Cycle.Single && (Elevator.CurrentTask.BarcodeLoadA == ((Case_Load)load).SSCCBarcode || Elevator.CurrentTask.BarcodeLoadB == ((Case_Load)load).SSCCBarcode)) && Elevator.ElevatorConveyor.Route.Loads.Count == 0 )
                //                    {
                //    Elevator.CurrentTask = null;  // Elevator task is now complete
                //}
            }
        }

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
            set { rackConveyorType = value;}
        }   

    }

    public class RackConveyorInfo : StraightConveyorInfo
    {
        public MultiShuttleDirections RackType;
    }

}
