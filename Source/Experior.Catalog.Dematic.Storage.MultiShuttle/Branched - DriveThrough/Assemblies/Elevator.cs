using Experior.Dematic.Base.Devices;
﻿using Experior.Catalog.Dematic.Case;
using Experior.Catalog.Dematic.Case.Components;
using Experior.Core.Assemblies;
using Experior.Core.Loads;
using Experior.Core.Routes;
using Experior.Dematic.Base;
using Microsoft.DirectX;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Xml.Serialization;
using System.Linq;
using System.Collections.Generic;

namespace Experior.Catalog.Dematic.Storage.MultiShuttle.Assemblies
{
    public class Elevator : Assembly
    {
        public ObservableCollection<ElevatorTask> ElevatorTasks = new ObservableCollection<ElevatorTask>();
        private ElevatorTask lastTask = null;
        public MultiShuttle ParentMultiShuttle;
        public ElevatorConveyor ElevatorConveyor;
        public TrackRail Lift;
        public RackSide Side;
        public int AisleNumber;
        private StraightConveyor moveToConvLevel;
        public string GroupName;
        public TrackVehicle Vehicle;

        public event EventHandler<EventArgs> OnElevatorArrived;

        public MultiShuttleDirections ElevatorType
        {
            get { return multishuttleElevatorInfo.ElevatorType; }
            set
            {
                multishuttleElevatorInfo.ElevatorType = value;
            }
        }


        ElevatorInfo multishuttleElevatorInfo;

        //[Browsable(false)]
        //public ElevatorConveyor LiftConveyor
        //{
        //    get { return ElevatorConveyor; }
        //}

        public float ElevatorSpeed
        {
            get { return Lift.Route.Motor.Speed; }
            set { Lift.Route.Motor.Speed = value; }
        }

        public float ElevatorConveyorSpeed //TODO move this to the elevator coveyor
        {
            get { return ElevatorConveyor.Route.Motor.Speed; }
            set { ElevatorConveyor.Route.Motor.Speed = value; }
        }

        public string ElevatorName
        {
            get { return multishuttleElevatorInfo.ElevatorName; }
            set
            {
                multishuttleElevatorInfo.ElevatorName = value;
            }
        }

        public Elevator(ElevatorInfo info) : base(info)
        {
            multishuttleElevatorInfo = info;
            Embedded = true;
            ParentMultiShuttle = info.Multishuttle;
            Side = info.Side;
            AisleNumber = ParentMultiShuttle.AisleNumber;
            GroupName = info.groupName;

            ElevatorConveyor = new ElevatorConveyor(new ElevatorConveyorInfo
            {
                length = info.multishuttleinfo.ElevatorConveyorLength,
                width = info.multishuttleinfo.ElevatorConveyorWidth,
                thickness = 0.05f,
                color = Core.Environment.Scene.DefaultColor,
                Elevator = this
            }
            );

            Add(ElevatorConveyor);
            ElevatorConveyor.Visible = false;
            ElevatorConveyor.Route.Motor.Speed = info.multishuttleinfo.ConveyorSpeed;
            ElevatorConveyor.LocalYaw = -(float)Math.PI;

            Lift = new TrackRail(new TrackRailInfo() { parentMultiShuttle = ParentMultiShuttle, level = 0, shuttlecarSpeed = ParentMultiShuttle.ShuttleCarSpeed, controlAssembly = this });
            Vehicle = new TrackVehicle(new TrackVehicleInfo() { trackRail = Lift, moveToDistance = 0, controlAssembly = this });

            Vehicle.Length = info.multishuttleinfo.ElevatorConveyorLength;
            Vehicle.Width = info.multishuttleinfo.ElevatorConveyorWidth;
            //Vehicle.Color = ElevatorConveyor.Color;
            Vehicle.Color = Color.Silver;

            Vehicle.OnVehicleArrived += ElevatorOnArrived;
            Vehicle.OnPositionChanged += Car_PositionChanged;
            Add((Core.Parts.RigidPart)Lift, new Vector3(-0.025f, 0, 0));
            Lift.LocalRoll = -(float)Math.PI / 2;
            Lift.Route.Motor.Speed = multishuttleElevatorInfo.multishuttleinfo.elevatorSpeed;
            Lift.Route.Motor.Stop();
            //Vehicle.Visible        = false;
            Vehicle.Roll = (float)Math.PI / 2;
            Vehicle.Movable = false;
            ElevatorTasks.Clear();
            ElevatorTasks.CollectionChanged += ElevatorTasks_CollectionChanged;
        }

        void ElevatorTasks_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                if (e.NewItems != null)
                {
                    ElevatorTask etNew = null;

                    foreach (ElevatorTask newTask in e.NewItems)
                    {
                        newTask.SourceLoadAConv = ParentMultiShuttle.GetConveyorFromLocName(newTask.SourceLoadA);
                        newTask.SourceLoadBConv = ParentMultiShuttle.GetConveyorFromLocName(newTask.SourceLoadB);
                        newTask.DestinationLoadAConv = ParentMultiShuttle.GetConveyorFromLocName(newTask.DestinationLoadA);
                        newTask.DestinationLoadBConv = ParentMultiShuttle.GetConveyorFromLocName(newTask.DestinationLoadB);
                        newTask.Elevator = this;
                        etNew = newTask;
                    }

                    ///// Check for repeated tasks This shouldn't happen however the controller/WMS may issue repeated tasks //////
                    List<ElevatorTask> listET = ElevatorTasks.ToList();

                    if (CurrentTask != null)
                    {
                        listET.Add(CurrentTask);
                    }

                    listET.Remove(etNew);

                    foreach (ElevatorTask st in listET)
                    {
                        if (st.Equals(etNew)) // Already have this task
                        {
                            ElevatorTasks.Remove(etNew);
                            //etNew = null;
                        }
                    }

                    //BG Changing this so that the controller and control have full control over what the elevator chooses to do next
                    //ParentMultiShuttle.ElevatorTasksStatusChanged(new ElevatorTasksStatusChangedEventArgs(this));
                    SetNewElevatorTask();
                }
            }
        }

        public void SetNewElevatorTask()
        {
            if (CurrentTask == null)
            {
                if (ElevatorTasks.Any())
                {
                    //Look for any type but make sure that the drop station is available before sending any missions
                    var oTasks = (ElevatorTasks.Where(task => task.Flow == TaskType.Outfeed && ((DropStationConveyor)task.DestinationLoadBConv).DropStationConvEmpty));
                    if (oTasks.Any())
                    {
                        //ElevatorTask lowestTask = oTasks.OrderBy(x => x.DropIndexLoadB).FirstOrDefault(x => x.DropIndexLoadB != 0); //Exclude unsequenced loads
                        var sTasks = OrderTasks(oTasks); //Does a special sort to allow for wrap around of the Drop Index
                        ElevatorTask lowestTask = sTasks.FirstOrDefault(x => x.DropIndexLoadB != 0); //Exclude unsequenced loads

                        ElevatorTask frontTask = null;
                        if (lowestTask != null && lowestTask.TaskLoadLocationBack())
                        {
                            //Check if there is a load in the front location with the same drop index
                            frontTask = oTasks.FirstOrDefault(x => x != lowestTask && x.SourceLoadBConv == lowestTask.SourceLoadBConv && x.DropIndexLoadB == lowestTask.DropIndexLoadB);
                        }
                        //Is the next task on the same level the next sequence number
                        //ElevatorTask nextLowestTask = oTasks.OrderBy(x => x.DropIndexLoadB).FirstOrDefault(x => x != lowestTask && x.DropIndexLoadB != 0); //Exclude unsequenced loads, are they on the same level?
                        ElevatorTask nextLowestTask = sTasks.FirstOrDefault(x => x != lowestTask && x.DropIndexLoadB != 0);

                        if (frontTask != null)
                        {
                            frontTask.CombineTasks(this, lowestTask);
                            lowestTask = frontTask;
                        }
                        else if (lowestTask != null && nextLowestTask != null && lowestTask.SourceLoadBConv == nextLowestTask.SourceLoadBConv)
                        {
                            //Combine the two tasks...
                            lowestTask.CombineTasks(this, nextLowestTask);
                        }
                        else if (lowestTask != null)
                        {
                            //Need to workout which load is in which position on the conveyor
                            //Does the lowest seq have an unsequenced load in front of it?
                            if (((RackConveyor)lowestTask.SourceLoadBConv).LocationA.Active && ((RackConveyor)lowestTask.SourceLoadBConv).LocationA.ActiveLoad.Identification == lowestTask.LoadB_ID)
                            {
                                //Is there a 0 sequnced load on the same level as a sequenced load?
                                var zTasks = ElevatorTasks.Where(task => task.DropIndexLoadB == 0 && task.SourceLoadBConv == lowestTask.SourceLoadBConv);
                                if (zTasks.Any())
                                {
                                    ElevatorTask zTask = zTasks.First();
                                    zTask.CombineTasks(this, lowestTask);
                                    lowestTask = zTask;
                                }
                            }
                        }
                        else
                        {
                            //Deal with unsequenced loads...
                            //Look for paired loads first...
                            var dTasks = oTasks.Where(x => ((RackConveyor)x.SourceLoadBConv).LocationB.Active && ((RackConveyor)x.SourceLoadBConv).LocationA.Active && ((RackConveyor)x.SourceLoadBConv).LocationB.ActiveLoad.Identification == x.LoadB_ID);
                            //Have found tasks that could be paired because there is a load behind (Load behind may not have an onward destination) 
                            foreach (ElevatorTask task in dTasks)
                            {
                                ElevatorTask dTask = oTasks.FirstOrDefault(x => x.SourceLoadBConv == task.SourceLoadBConv && x.LoadB_ID != task.LoadB_ID);
                                if (dTask != null)
                                {
                                    task.CombineTasks(this, dTask);
                                    lowestTask = task;
                                    break;
                                }
                            }

                            if (lowestTask == null) //Check to see if these is a grouped task and execute
                            {
                                lowestTask = oTasks.FirstOrDefault(x => x.NumberOfLoadsInTask == 2);                  
                            }

                            if (lowestTask == null) //A paired load has not been found so choose a single (will be paired with another load from another level using optimise task)
                            {
                                //Only choose loads that are on the front position (B)
                                lowestTask = oTasks.FirstOrDefault(x => ((RackConveyor)x.SourceLoadBConv).LocationB.Active && ((RackConveyor)x.SourceLoadBConv).LocationB.ActiveLoad.Identification == x.LoadB_ID);
                                if (lowestTask != null)
                                {
                                    //Is there also a load behind on the same level that can be paired to it?
                                    nextLowestTask = oTasks.FirstOrDefault(x => x.SourceLoadBConv == lowestTask.SourceLoadBConv && x != lowestTask);
                                    if (nextLowestTask != null)
                                    {
                                        lowestTask.CombineTasks(this, nextLowestTask);
                                    }
                                }
                            }
                        }

                        //Set the current task and start the elevator moving
                        CurrentTask = lowestTask;
                    }
                    else //When the drop station is not available then try to find one on the infeed station
                    {
                        //look for inbound tasks
                        var iTasks = ElevatorTasks.Where(task => task.Flow == TaskType.Infeed || task.Flow == TaskType.HouseKeep); //TODO: For house keeping moves this needs to be updated to check that the rack in conveyor is available ideally
                        if (iTasks.Any())
                        {
                            CurrentTask = iTasks.First();
                        }
                    }
                }
                if (CurrentTask != null)
                {
                    ElevatorTask t = CurrentTask;
                    //Log.Write(string.Format("New Task: LoadB: {0}_{1}_{2}, LoadA {3}_{4}_{5}", t.LoadB_ID, t.DropIndexLoadB, t.SourceLoadB, t.LoadA_ID, t.DropIndexLoadA, t.SourceLoadA));
                }
            }
        }

        public static IEnumerable<ElevatorTask> OrderTasks(IEnumerable<ElevatorTask> Value)
        {
            ObservableCollection<ElevatorTask> specialSort = new ObservableCollection<ElevatorTask>();

            var sortedClasses = Value.OrderBy(x => x.DropIndexLoadB).ToList();

            foreach (ElevatorTask special in sortedClasses)
            {
                if (special.DropIndexLoadB > (sortedClasses[0].DropIndexLoadB + 5000))
                {
                    specialSort.Add(special);
                }
            }
            foreach (ElevatorTask special in sortedClasses)
            {
                if (special.DropIndexLoadB <= (sortedClasses[0].DropIndexLoadB + 5000))
                {
                    specialSort.Add(special);
                }
            }
            return specialSort;
        }

        void Car_PositionChanged(Load load, Vector3 position)//TODO move this to the elevator coveyor
        {
            //ElevatorConveyor.LocalPosition = new Vector3(0, load.Distance - Lift.Length / 2, 0);
            ElevatorConveyor.Route.TranslationVector = new Vector3(0, load.Distance - Lift.Length / 2, 0);
        }

        public float ElevatorHeight
        {
            get { return Lift.Length; }
            set
            {
                if (value <= 0) { return; }
                Lift.Length = value;
            }
        }

        internal void MoveElevator(StraightConveyor conv = null)
        {
            if (CurrentTask.TasksLoadsArrivedOnElevator == CurrentTask.NumberOfLoadsInTask)
            {
                ElevatorConveyor.UnLoading = true;
            }
            else if (CurrentTask.TasksLoadsArrivedOnElevator < CurrentTask.NumberOfLoadsInTask)
            {
                ElevatorConveyor.UnLoading = false;
            }

            //A different points the locations might be disabled to make controlling loads so make sure that they are enabled before moving
            ElevatorConveyor.LocationA.Enabled = true;
            ElevatorConveyor.LocationB.Enabled = true;

            //var psConv = ParentMultiShuttle.PickStationConveyors[0];  //TODO infeed only single PS only

            if (conv != null) //supplied with a destination
            {
                moveToConvLevel = conv;
            }
            else if (ElevatorConveyor.TransportSection.Route.Loads.Count == 0) //get the first load to pickup, don't care if outfeed or infeed
            {
                ElevatorConveyor.Route.Motor.Forward(); // At the start of move when the elevator is empty set the conveyor in the forward direction
                moveToConvLevel = CurrentTask.SourceLoadBConv ?? CurrentTask.SourceLoadAConv;
            }
            else if (CurrentTask.Flow == TaskType.Outfeed) // an outfeed so there must be a load at LocationB, the load at B is the first drop off
            {
                if (CurrentTask.NumberOfLoadsInTask == 2)
                {
                    if (ElevatorConveyor.Route.Loads.Count == 1 && !ElevatorConveyor.UnLoading) //Have already got 1 load on board get the second pickup conveyor
                    {
                        moveToConvLevel = CurrentTask.GetSourceConvOfLoad((Case_Load)ElevatorConveyor.Route.Loads.ElementAt(0), true); // regardless of what load is on board get the dest of the load not picked up
                    }
                    else if (ElevatorConveyor.Route.Loads.Count == 1 && ElevatorConveyor.UnLoading) //Unloading second load
                    {
                        moveToConvLevel = CurrentTask.GetDestConvOfLoad((Case_Load)ElevatorConveyor.Route.Loads.ElementAt(0));
                    }
                    else //2 loads going to 1 or 2 dropstations..currently always choose location probably will not work with drivethrought or where the DS is not infront of the MS as it may not be the destination of the B load that is needed
                    {
                        moveToConvLevel = CurrentTask.DestinationLoadBConv;
                    }
                }
                else if (CurrentTask.NumberOfLoadsInTask == 1)
                {
                    moveToConvLevel = CurrentTask.GetDestConvOfLoad((Case_Load)ElevatorConveyor.LocationB.ActiveLoad);
                }
            }
            else if (CurrentTask.Flow == TaskType.Infeed)// && psConv.Route.Loads.Count == 0)
            {
                var ps = ParentMultiShuttle.PickStationConveyors.Find(x => x.RouteAvailable == RouteStatuses.Blocked && x.TransportSection.Route.Loads.Count == 0);
                if (ps != null)
                {
                    ps.RouteAvailable = RouteStatuses.Request;
                }
            }

            if (moveToConvLevel == null)
            {
                Log.Write(string.Format("{0}: Error in Elevator.MoveElevator(), can't find currentElevatorConvLevel", ParentMultiShuttle.Name), Color.Red);
                if (conv != null)
                {
                    Log.Write(string.Format("{0}: Conveyor {1}", ParentMultiShuttle.Name, conv.Name), Color.Red);
                }
                Log.Write(Environment.StackTrace, Color.Red);
                return;
            }

            //Account for the offset height of the elevator
            float ElevatorHeight = ParentMultiShuttle.Position.Y;
            float ConveyorHeight = moveToConvLevel.Height;
            float MovePosition = ConveyorHeight - ElevatorHeight;

            float check = Math.Abs(Vehicle.Distance - MovePosition);

            if (Math.Abs(Vehicle.Distance - MovePosition) < 0.001)
            {
                ElevatorOnArrived(null, null);
                return;
            }

            if (Vehicle.DestAP.Distance > MovePosition)
            {
                Lift.Route.Motor.Backward();
            }
            else if (Vehicle.DestAP.Distance < MovePosition)
            {
                Lift.Route.Motor.Forward();
            }

            Vehicle.DestAP.Distance = MovePosition;
            Lift.Route.Motor.Start();
            Vehicle.Release();
        }

        private void ElevatorOnArrived(object sender, EventArgs e)
        {
            if (CurrentTask == null)
            {
                //Log.Write("Elevator arrived with a null current task");
                return;
            }

            ElevatorConveyor.Route.NextRoute = null;
            ElevatorConveyor.Route.LastRoute = null;

            if (OnElevatorArrived != null)
            {
                OnElevatorArrived(this, new EventArgs());
            }

            if (CurrentTask.Flow == TaskType.Infeed || CurrentTask.Flow == TaskType.HouseKeep)
            {
                if (ElevatorConveyor.Route.Loads.Count == 0) //Arrived at the pickstation so release whatever is waiting for the elevator to arrive
                {
                    Load loadASpecial = null, loadBSpecial = null;

                    var A = ParentMultiShuttle.ConveyorLocations.Find(x => x.LocName == CurrentTask.SourceLoadA);
                    if (A != null)
                    {
                        loadASpecial = A.ActiveLoad;
                    }

                    var B = ParentMultiShuttle.ConveyorLocations.Find(x => x.LocName == CurrentTask.SourceLoadB);
                    if (B != null)
                    {
                        loadBSpecial = B.ActiveLoad;
                    }

                    if (loadASpecial != null)
                    {
                        loadASpecial.Release();
                    }

                    if (loadBSpecial != null)
                    {
                        loadBSpecial.Release();
                    }
                }
                else if (CurrentTask.UnloadCycle == Cycle.Single)
                {
                    ElevatorConveyor.LocationB.Release();
                    //  Load.Get(CurrentTask.LoadB_ID).Release();

                }
                else if (CurrentTask.LoadCycle == Cycle.Double)
                {
                    //Arrived at a double dropoff infeed rack conveyor so release both elevator loc
                    ElevatorConveyor.LocationB.Release();
                    ElevatorConveyor.LocationA.Release();
                    ElevatorConveyor.LocationB.Enabled = false;
                }
            }
            else if (CurrentTask.Flow == TaskType.Outfeed)
            {
                if (ElevatorConveyor.UnLoading) //outfeed and unloading so have arrived at a DS
                {
                    if (CurrentTask.DestinationLoadBConv != null && CurrentTask.DestinationLoadBConv.ThisRouteStatus.Available != RouteStatuses.Available)
                    {
                        CurrentTask.DestinationLoadBConv.ThisRouteStatus.OnRouteStatusChanged += ThisRouteStatus_OnRouteStatusChanged;
                        return;
                    }

                    ElevatorConveyor.LocationB.Release();
                    ElevatorConveyor.LocationA.Release();

                    if (CurrentTask.UnloadCycle == Cycle.Double || CurrentTask.NumberOfLoadsInTask == 1)
                    {
                        ElevatorConveyor.LocationB.Enabled = false; // if only 1 load in task don't care about this but doesn't do any harm
                        ElevatorConveyor.UnLoading = false;
                    }
                }
                else if (!ElevatorConveyor.UnLoading) // still loading the elevator
                {
                    RackConveyor rc = moveToConvLevel as RackConveyor;
                    if (rc == null)
                    {
                        Log.Write("Offloading at DS but elevator thinks it is at a rack conv.", Color.Red);
                    }
                    else
                    {
                        rc.LocationB.Release();

                        if (CurrentTask.LoadCycle == Cycle.Double) // If its a double also release A as well
                        {
                            rc.LocationA.Release();
                        }
                    }
                }
            }
        }

        void ThisRouteStatus_OnRouteStatusChanged(object sender, RouteStatusChangedEventArgs e)
        {
            if (CurrentTask != null)
            {
                CurrentTask.DestinationLoadBConv.ThisRouteStatus.OnRouteStatusChanged -= ThisRouteStatus_OnRouteStatusChanged;
            }

            if (e._available == RouteStatuses.Available)
            {
                ElevatorConveyor.LocationB.Release();
                ElevatorConveyor.LocationA.Release();

                if (CurrentTask != null && (CurrentTask.UnloadCycle == Cycle.Double || CurrentTask.NumberOfLoadsInTask == 1))
                {
                    ElevatorConveyor.LocationB.Enabled = false; // if only 1 load in task don't care about this!!!
                    ElevatorConveyor.UnLoading = false;
                }
            }
        }

        public override void Reset()
        {
            Lift.Route.Motor.Stop();

            Vehicle.DestAP.Distance = 0;
            Vehicle.Switch(Vehicle.DestAP);

            ElevatorTasks.Clear();
            currentTask = null;

            foreach (Load l in ElevatorConveyor.Route.Loads)
            {
                l.Dispose();
            }

            base.Reset();
        }

        public override void Dispose()
        {
            ElevatorTasks.CollectionChanged -= ElevatorTasks_CollectionChanged;
            Vehicle.OnPositionChanged -= Car_PositionChanged;
            Vehicle.Dispose();
            Lift.Dispose();
            ElevatorTasks.Clear();
            Remove(ElevatorConveyor);
            ElevatorConveyor.Dispose();

            base.Dispose();
        }

        #region Properties

        public override string Category
        {
            get { return "Elevator"; }
        }

        public override Image Image
        {
            get { return Common.Icons.Get("Elevator"); }
        }

        /// <summary>
        /// Rotates the whole conveyor into the required direction
        /// </summary>
        /// <param name="taskType"></param>
        public void ChangeElevatorConvDirection(TaskType taskType)
        {
            if (!ParentMultiShuttle.MultiShuttleinfo.DriveThrough)
            {
                //ElevatorType doesn't really make any sense as it can service both infeed and outfeed
                if (taskType == TaskType.Infeed && ElevatorType == MultiShuttleDirections.Outfeed)
                {
                    ElevatorConveyor.LocalYaw = ElevatorConveyor.LocalYaw + (float)Math.PI;
                    ElevatorType = MultiShuttleDirections.Infeed;
                }
                else if ((taskType == TaskType.Outfeed || taskType == TaskType.HouseKeep) && ElevatorType == MultiShuttleDirections.Infeed)
                {
                    ElevatorConveyor.LocalYaw = ElevatorConveyor.LocalYaw - (float)Math.PI;
                    ElevatorType = MultiShuttleDirections.Outfeed;
                }
            }
        }

        private ElevatorTask currentTask;
        public ElevatorTask CurrentTask
        {
            get { return currentTask; }
            set
            {
                if (value != null)
                {
                    lastTask = currentTask;
                    currentTask = value;
                    ElevatorTasks.Remove(value);
                    ChangeElevatorConvDirection(currentTask.Flow);
                    MoveElevator();
                }
                else if (value == null)
                {
                    lastTask = currentTask;
                    currentTask = null;
                    //Now we need to trigger the controller to select a new task
                    ParentMultiShuttle.ElevatorTasksStatusChanged(new ElevatorTasksStatusChangedEventArgs(this));
                }
            }
        }

        /// <summary>
        /// <para>Do not create a task directly only add a task to ElevatorTasks</para>para>
        /// <para>When a task has finished then set the task to null directly </para>para>
        /// <para>(CurrentTask = null;) this will get the next task in the list ElevatorTasks.</para>
        /// </summary>
        //public ElevatorTask CurrentTask
        //{
        //    get { return currentTask; }
        //    set
        //    {
        //        if (value != null) //at not null value only comes from ElevatorTasks_CollectionChanged or itself (CurrentTask)
        //        {
        //            //temp DeBug
        //            if (Side == RackSide.Left)
        //            {
        //                ParentMultiShuttle.LeftElevatorTaskDisplay = value.ToString();
        //            }
        //            else if (Side == RackSide.Right)
        //            {
        //                ParentMultiShuttle.RightElevatorTaskDisplay = value.ToString();
        //            }

        //            lastTask = currentTask;
        //            currentTask = value;
        //            ElevatorTasks.Remove(value);
        //            ChangeElevatorConvDirection(currentTask.Flow);
        //            MoveElevator();
        //        }
        //        else if (value == null)
        //        {
        //            ElevatorConveyor.UnLoading = false;

        //            if (ParentMultiShuttle.AutoNewElevatorTask) //This switch allows the control object to choose what load is next..Call The controller should call GetNewElevatorTask
        //            {
        //                if (ElevatorTasks.Any()) //Just take the first task if one exists
        //                {
        //                    currentTask = null; //The CurrentTask wraps this field
        //                    ElevatorTask eT = ElevatorTasks[0];
        //                    CurrentTask = eT;
        //                }
        //            }

        //            //Temp Debug
        //            if (Side == RackSide.Left)
        //            {
        //                ParentMultiShuttle.LeftElevatorTaskDisplay = "";
        //            }
        //            else if (Side == RackSide.Right)
        //            {
        //                ParentMultiShuttle.RightElevatorTaskDisplay = "";
        //            }
        //        }
        //    }
        //}

        /// <summary>
        /// <para>Use this method when AutoNewElevatorTask is false to get the next task for the elevator.</para>
        /// <para>It will allow you to change prioritys to whatever you need it to be.</para>
        /// </summary>
        /// <param name="priorityMethod"></param>
        //public void GetNewElevatorTask(Func<ObservableCollection<ElevatorTask>, ElevatorTask, ElevatorTask> priorityMethod)
        //{
        //    lastTask = currentTask;
        //    currentTask = null; //The CurrentTask wraps this field...Really important to do this....I think!
        //    if (priorityMethod != null)
        //    {
        //        ElevatorTask task = priorityMethod.Invoke(ElevatorTasks, lastTask);
        //        if (task != null)
        //        {
        //            CurrentTask = task;
        //        }
        //    }
        //}

        #endregion

    }

    [Serializable]
    [XmlInclude(typeof(ElevatorInfo))]
    public class ElevatorInfo : AssemblyInfo
    {
        #region Fields

        public MultiShuttleInfo multishuttleinfo;
        public string ElevatorName;
        public RackSide Side;
        public string groupName;
        internal MultiShuttle Multishuttle;

        private static ElevatorInfo properties = new ElevatorInfo();
        public MultiShuttleDirections ElevatorType;

        #endregion

    }
}