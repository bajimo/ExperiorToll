using Experior.Catalog.Dematic.Case;
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
        private ElevatorTask currentTask;
        public MultiShuttle ParentMultiShuttle;
        public ElevatorConveyor ElevatorConveyor;
        private TrackRail lift;
        public RackSide Side;
        public int AisleNumber;
        private StraightConveyor currentElevatorConvLevel;

        public event EventHandler<EventArgs> OnElevatorArrived;

        public MultiShuttleDirections ElevatorType 
        {
            get {return multishuttleElevatorInfo.ElevatorType; }
            set
            {
                multishuttleElevatorInfo.ElevatorType = value;
            }
        }


        MultiShuttleElevatorInfo multishuttleElevatorInfo;

        [Browsable(false)]
        public ElevatorConveyor LiftConveyor
        {
            get { return ElevatorConveyor; }
        }

        public float ElevatorSpeed
        {
            get { return lift.Route.Motor.Speed; }
            set { lift.Route.Motor.Speed = value; }
        }

        public float ElevatorConveyorSpeed //TODO move this to the elevator coveyor
        {
            get { return ElevatorConveyor.Route.Motor.Speed; }
            set { ElevatorConveyor.Route.Motor.Speed = value; }
        }

        public string ElevatorName
        {
            get { return multishuttleElevatorInfo.ElevatorName; }
        }

        public Elevator(MultiShuttleElevatorInfo info) : base(info)
        {
            multishuttleElevatorInfo = info;            
            Embedded                 = true;
            ParentMultiShuttle       = info.Multishuttle;
            Side                     = info.Side;
            AisleNumber              = ParentMultiShuttle.AisleNumber;

            ElevatorConveyor = new ElevatorConveyor(new ElevatorConveyorInfo
                {
                    length    = info.multishuttleinfo.ElevatorConveyorLength,
                    width     = info.multishuttleinfo.ElevatorConveyorWidth,
                    thickness = 0.05f,
                    color     = Core.Environment.Scene.DefaultColor,
                    Elevator  = this
                }
                );          

            AddAssembly(ElevatorConveyor);
            ElevatorConveyor.Route.Motor.Speed = info.multishuttleinfo.ConveyorSpeed;
            ElevatorConveyor.LocalYaw          = -(float)Math.PI;

            lift = new TrackRail(info.multishuttleinfo, 1, info.Multishuttle,this);// UserData = this };
            lift.Car.OnPositionChanged      += Car_PositionChanged; 
            AddPart(lift);           
            lift.LocalRoll                   = -(float)Math.PI / 2;
            lift.Route.Motor.Speed           = multishuttleElevatorInfo.multishuttleinfo.elevatorSpeed;
            lift.Car.Visible                 = false;
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
                    ///////

                    Log.Write(etNew.ToString());
                    if (CurrentTask == null && etNew != null ) //elevator not doing a task add this to the current task
                    {
                        CurrentTask = etNew;
                    }

                }
            }
        }

        void Car_PositionChanged(Load load, Vector3 position)//TODO move this to the elevator coveyor
        {
            ElevatorConveyor.LocalPosition = new Vector3(0, load.Distance - lift.Length / 2, 0);
        }  
         
        public float ElevatorHeight
        {
            get { return lift.Length; }
            set
            {
                if (value <= 0) {return; }
                lift.Length = value;
            }
        }

        internal void MoveElevator(float destHeight)
        {
            lift.Destination.Distance = destHeight;
            lift.Route.Motor.Start();
            lift.ShuttleCar.Release();          
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
                currentElevatorConvLevel = conv;
            }
            else if (ElevatorConveyor.TransportSection.Route.Loads.Count == 0) //get the first load to pickup, don't care if outfeed or infeed
            {
                currentElevatorConvLevel =  CurrentTask.SourceLoadBConv ?? CurrentTask.SourceLoadAConv;
            }
            else if (CurrentTask.Flow == TaskType.Outfeed) // an outfeed so there must be a load at LocationB, the load at B is the first drop off    
            {
                if (CurrentTask.NumberOfLoadsInTask == 2)
                {
                    if (ElevatorConveyor.Route.Loads.Count == 1 && !ElevatorConveyor.UnLoading) //Have already got 1 load on board get the second pickup conveyor
                    {                       
                        currentElevatorConvLevel = CurrentTask.GetSourceConvOfLoad((Case_Load)ElevatorConveyor.Route.Loads.ElementAt(0), true); // regardless of what load is on board get the dest of the load not picked up
                    }
                    else //2 loads going to 1 or 2 dropstations..currently always choose location probably will not work with drivethrought or where the DS is not infront of the MS
                    {
                        currentElevatorConvLevel = CurrentTask.GetDestConvOfLoad((Case_Load)ElevatorConveyor.LocationB.ActiveLoad);
                    }
                }
                else if (CurrentTask.NumberOfLoadsInTask == 1)
                {
                    currentElevatorConvLevel = CurrentTask.GetDestConvOfLoad((Case_Load)ElevatorConveyor.LocationB.ActiveLoad);
                }
            }            
            else if (CurrentTask.Flow == TaskType.Infeed)// && psConv.Route.Loads.Count == 0) 
            {
                var ps = ParentMultiShuttle.PickStationConveyors.Find(x => x.RouteAvailable == RouteStatuses.Blocked && x.TransportSection.Route.Loads.Count == 0);
                if (ps != null)
                {
                    ps.RouteAvailable = RouteStatuses.Request;
                }
                //psConv.RouteAvailable = RouteStatuses.Request; // A new infeed job has just been loaded on the elevator so make the pickstation avaiable
            }

            if (currentElevatorConvLevel == null)
            {
                Log.Write("Error in Elevator.MoveElevator(), can't find currentElevatorConvLevel", Color.Red);
                return;
            }

            //Account for the offset height of the elevator
            float ElevatorHeight = ParentMultiShuttle.Position.Y;
            float ConveyorHeight = currentElevatorConvLevel.Height;
            float MovePosition = ConveyorHeight - ElevatorHeight;

            if (lift.Destination.Distance == MovePosition)
            {
                lift.Destination.Distance = MovePosition;
                ElevatorOnArrived();
                return;
            }

            if (lift.Destination.Distance > MovePosition) //currentElevatorConvLevel.Height)
            {
                lift.Route.Motor.Backward();
            }
            else if (lift.Destination.Distance < MovePosition) //currentElevatorConvLevel.Height)
            {
                lift.Route.Motor.Forward();
            }

            lift.Destination.Distance = MovePosition; //currentElevatorConvLevel.Height;
            lift.Route.Motor.Start();
            lift.ShuttleCar.Release();                     
        }

        //internal void MoveElevator(StraightConveyor conv)
        //{
        //    //A different points the locations might be disabled to make controlling loads so make sure that they are enabled before moving
        //    ElevatorConveyor.LocationA.Enabled = true;
        //    ElevatorConveyor.LocationB.Enabled = true;

        //    currentElevatorConvLevel = conv; // bit dodge but.... unloadConveyor is used in ElevatorOnArrived()
        //    var psConv = ParentMultiShuttle.PickStationConveyors[0];

        //    // A new infeed job has just been loaded on the elevator so make the pickstation avaiable
        //    if (CurrentTask.Flow == TaskType.Infeed && psConv.Route.Loads.Count == 0)
        //    {
        //        psConv.RouteAvailable = RouteStatuses.Available;
        //    }

        //    if (lift.Destination.Distance > conv.Height)
        //    {
        //        lift.Route.Motor.Backward();
        //    }
        //    else if (lift.Destination.Distance < conv.Height)
        //    {
        //        lift.Route.Motor.Forward();
        //    }

        //    lift.Destination.Distance = conv.Height;
        //    lift.Route.Motor.Start();
        //    lift.ShuttleCar.Release();
        //}

        public void ReleaseLocationAFFS(Load load, string userData)
        {
            if (load != null)
            {
                load.UserData = userData;
                load.Release();
            }
        }

        //Load loadASpecial = null;
       // Load loadBSpecial = null;

        public void ElevatorOnArrived()
        {
            if (CurrentTask == null)
            {
                Log.Write("Elevator arrived with a null current task");
                return;
            }

            ElevatorConveyor.Route.NextRoute = null;
            ElevatorConveyor.Route.LastRoute = null;

            if (OnElevatorArrived != null)
            {
                OnElevatorArrived(this, new EventArgs());
            }

            if (CurrentTask.Flow == TaskType.Infeed)
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

                }
                else if (CurrentTask.LoadCycle == Cycle.Double)
                {
                    //if (CurrentTask.UnloadCycle == Cycle.Double && currentElevatorConvLevel.TransportSection.Route.Loads.Count > 0)
                    //{
                    //    Log.Write("MS Error: Cannot drop two loads to infeed rack conveyor");
                    //    Core.Environment.Scene.Pause();
                    //    return;
                    //}

                    //Arrived at a double dropoff infeed rack conveyor so release both elevator loc
                    ElevatorConveyor.LocationB.Release();
                    ElevatorConveyor.LocationA.Release(); 
                    //ReleaseLocationAFFS(ElevatorConveyor.LocationA.ActiveLoad, "Elevator 328");
                    ElevatorConveyor.LocationB.Enabled = false;
                }
            }
            else if (CurrentTask.Flow == TaskType.Outfeed)
            {
                if (ElevatorConveyor.UnLoading) //outfeed and unloading so have arrived at a DS
                {
                    StraightConveyor conv = CurrentTask.GetDestConvOfLoad((Case_Load)ElevatorConveyor.LocationB.ActiveLoad);

                    if (conv.ThisRouteStatus.Available != RouteStatuses.Available)
                    {
                        conv.ThisRouteStatus.OnRouteStatusChanged += ThisRouteStatus_OnRouteStatusChanged;
                        //Core.Environment.Log.Write("MS Error: Cannot drop load to Drop Station conveyor");
                        //Core.Environment.Scene.Pause();
                        return;
                    }

                    ElevatorConveyor.LocationB.Release();
                    ElevatorConveyor.LocationA.Release(); 
                    //ReleaseLocationAFFS(ElevatorConveyor.LocationA.ActiveLoad, "Elevator 346");

                    if (CurrentTask.UnloadCycle == Cycle.Double || CurrentTask.NumberOfLoadsInTask == 1)
                    {
                        ElevatorConveyor.LocationB.Enabled = false; // if only 1 load in task don't care about this but doesn't do any harm
                        ElevatorConveyor.UnLoading = false;
                    }
         
                }
                else if (!ElevatorConveyor.UnLoading) // still loading the elevator 
                {
                    if (CurrentTask.LoadCycle == Cycle.Single && CurrentTask.NumberOfLoadsInTask == 2)
                    {

                    }

                    ((RackConveyor)currentElevatorConvLevel).LocationB.Release();

                    //var locB = ParentMultiShuttle.ConveyorLocations.Find(x => x.LocName == CurrentTask.SourceLoadB);
                    //locB.Release();

                    //((RackConveyor)currentElevatorConvLevel).LocationB.Release(); //Release B regardless

                    if (CurrentTask.LoadCycle == Cycle.Double) // If its a double also release A as well
                    {
                        ((RackConveyor)currentElevatorConvLevel).LocationA.Release();

                        //var locA = ParentMultiShuttle.ConveyorLocations.Find(x => x.LocName == CurrentTask.SourceLoadA);
                        //locA.Release(); //BOOM
                        //ReleaseLocationAFFS(locA.ActiveLoad, "Elevator 366");
                        //((RackConveyor)currentElevatorConvLevel).LocationA.Release();
                    }
                }

                //if (ElevatorConveyor.Route.Loads.Count == 0)
                //{
                //    if (CurrentTask.LoadCycle == Cycle.Single)
                //    {

                //        var v = ParentMultiShuttle.ConveyorLocations.Find(x => x.LocName == CurrentTask.SourceLoadB);
                //        ParentMultiShuttle.ConveyorLocations.Find(x => x.LocName == CurrentTask.SourceLoadB).Release();
                //    }
                //    else if (CurrentTask.LoadCycle == Cycle.Double)
                //    {
                //        ParentMultiShuttle.ConveyorLocations.Find(x => x.LocName == CurrentTask.SourceLoadA).Release();
                //        ParentMultiShuttle.ConveyorLocations.Find(x => x.LocName == CurrentTask.SourceLoadB).Release();
                //    }
                //}
                //else
                //{

                //    if (CurrentTask.LoadCycle == Cycle.Single && CurrentTask.NumberOfLoadsInTask() == 2 && ElevatorConveyor.TransportSection.Route.Loads.Count == 1)
                //    {
                //        //loading 2 loads from different levels. Just arrived at the 2nd loads rack conveyor
                //        ParentMultiShuttle.ConveyorLocations.Find(x => x.LocName == CurrentTask.SourceLoadA).Release();
                //    }
                //    //else if (CurrentTask.UnloadCycle == Cycle.Single && ElevatorConveyor.TransportSection.Route.Loads.Count == 1)
                //    //{
                //    //    ElevatorConveyor.LocationB.Release();
                //    //}
                //    else if ((CurrentTask.UnloadCycle == Cycle.Single && ElevatorConveyor.TransportSection.Route.Loads.Count == 1) || (CurrentTask.UnloadCycle == Cycle.Double && ElevatorConveyor.TransportSection.Route.Loads.Count == 2))
                //    {
                //        //Under these conditions the elevator must have arrived at the drop station
                //        if (CurrentTask.DestinationLoadBConv.NextRouteStatus.Available != RouteStatuses.Available)
                //        {
                //            Core.Environment.Log.Write("MS Error: Cannot drop load to Drop Station conveyor");
                //            Core.Environment.Scene.Pause();
                //            return;
                //        }

                //        ElevatorConveyor.LocationB.Release();
                //        ElevatorConveyor.LocationA.Release();
                //        ElevatorConveyor.LocationB.Enabled = false;
                //    }
                //    else if (CurrentTask.UnloadCycle == Cycle.Single && CurrentTask.NumberOfLoadsInTask() == 2 && ElevatorConveyor.TransportSection.Route.Loads.Count == 2)
                //    {
                //        ElevatorConveyor.LocationA.Release();
                //        ElevatorConveyor.LocationB.Release();
                //    }
                //    else if (CurrentTask.UnloadCycle == Cycle.Single)
                //    {
                //        ElevatorConveyor.LocationA.Release();
                //    }
                //}
            }

            //if (ElevatorConveyor.Route.Loads.Count > 0 && ElevatorType == MultiShuttleDirections.Infeed)//This is wrong must not use elevator type as it does not exist
            //{
            //    if (CurrentTask.UnloadCycle == Cycle.Single && unloadConveyor.TransportSection.Route.Loads.Count > 1)
            //    {
            //        Core.Environment.Log.Write("MS Error: Cannot drop load to infeed rack conveyor");
            //        Core.Environment.Scene.Pause();
            //        return;
            //    }
            //    ElevatorConveyor.LocationB.Release();
            //}
            
        }

        void loadBSpecial_OnReleased(Load load)
        {
            //Log.Write(string.Format("LOAD B: {0} Released: {1}", load.Identification, load.UserData.ToString()));
        }

        void loadASpecial_OnReleased(Load load)
        {
           // Log.Write(string.Format("LOAD A: {0} Released: {1}", load.Identification, load.UserData == null ? "" : load.UserData.ToString()));
          //  Log.Write(Environment.StackTrace);
        }

        void ThisRouteStatus_OnRouteStatusChanged(object sender, RouteStatusChangedEventArgs e)
        {
            ((RouteStatus)sender).OnRouteStatusChanged -= ThisRouteStatus_OnRouteStatusChanged;

            if (e._available == RouteStatuses.Available)
            {
                ElevatorConveyor.LocationB.Release();
                ElevatorConveyor.LocationA.Release(); 

                if (CurrentTask.UnloadCycle == Cycle.Double || CurrentTask.NumberOfLoadsInTask == 1)
                {
                    ElevatorConveyor.LocationB.Enabled = false; // if only 1 load in task don't care about this!!!
                    ElevatorConveyor.UnLoading = false;
                }
            }
        }

        public override void Reset()
        {
            lift.Route.Motor.Stop();

            ElevatorTasks.Clear();
            currentTask = null;

            foreach (Load l in ElevatorConveyor.Route.Loads)
            {
                l.Dispose();
            }

            lift.Destination.Distance = 0;
            lift.ShuttleCar.Switch(lift.Destination);
            base.Reset();
        }

        public override void Dispose()
        {            
            ElevatorTasks.CollectionChanged -= ElevatorTasks_CollectionChanged;
            lift.Car.OnPositionChanged -= Car_PositionChanged; 
            lift.Dispose();
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

        private void ChangeElevatorConvDirection(TaskType taskType) 
        {
            if (taskType == TaskType.Infeed && ElevatorType == MultiShuttleDirections.Outfeed)
            {
                ElevatorConveyor.LocalYaw = ElevatorConveyor.LocalYaw + (float)Math.PI;
                ElevatorType = MultiShuttleDirections.Infeed;
            }
            else if (taskType == TaskType.Outfeed && ElevatorType == MultiShuttleDirections.Infeed)
            {
                ElevatorConveyor.LocalYaw = ElevatorConveyor.LocalYaw - (float)Math.PI;
                ElevatorType = MultiShuttleDirections.Outfeed;
            }
        }

        /// <summary>
        /// Do not create a task directly only add a task to ElevatorTasks
        /// When a task has finished then set the task to null directly 
        /// (CurrentTask = null;) this will get the next task in the list ElevatorTasks.
        /// </summary>
        public ElevatorTask CurrentTask
        {
            get { return currentTask; }
            set
            {
                if (value != null) //at not null value only comes from ElevatorTasks_CollectionChanged or itself (CurrentTask)
                {
                    //temp DeBug
                    if (Side == RackSide.Left)
                    {
                        ParentMultiShuttle.LeftElevatorTaskDisplay = value.ToString();
                    }
                    else if (Side == RackSide.Right)
                    {
                        ParentMultiShuttle.RightElevatorTaskDisplay = value.ToString();
                    }

                    currentTask = value;
                    ElevatorTasks.RemoveAt(0); //value and ElevatorTasks[0] should be the same
                    ChangeElevatorConvDirection(currentTask.Flow);
                    MoveElevator();
                }
                else if (value == null)
                {
                    ElevatorConveyor.UnLoading = false;

                    if (ParentMultiShuttle.AutoNewElevatorTask)
                    {
                        GetNewElevatorTask();
                    }

                    //Temp Debug
                    if (Side == RackSide.Left)
                    {
                        ParentMultiShuttle.LeftElevatorTaskDisplay = "";
                    }
                    else if (Side == RackSide.Right)
                    {
                        ParentMultiShuttle.RightElevatorTaskDisplay = "";
                    }
                }
            }
        }

        /// <summary>
        /// Refactored from CurrentTask so that a control object can have control of when a new task assigned (if there is one to be assigned).
        /// </summary>
        public void GetNewElevatorTask()
        {
            currentTask = null; //The CurrentTask wraps this field

            if (ElevatorTasks.Any()) //Get a new task
            {
                ElevatorTask eT = ElevatorTasks[0];
                CurrentTask = eT;
            }
        }

        #endregion

    }

    [Serializable]
    [XmlInclude(typeof(MultiShuttleElevatorInfo))]
    public class MultiShuttleElevatorInfo : AssemblyInfo
    {
        #region Fields

        public MultiShuttleInfo multishuttleinfo;
        public string ElevatorName;
        public RackSide Side;
        internal MultiShuttle Multishuttle;

        private static MultiShuttleElevatorInfo properties = new MultiShuttleElevatorInfo();
        public MultiShuttleDirections ElevatorType;

        #endregion

    }
}