using Experior.Core.Loads;
using Experior.Core.TransportSections;
using Microsoft.DirectX;
using System.ComponentModel;
using System.Drawing;

namespace Experior.Catalog.Dematic.Storage.Assemblies
{
    //public class ShuttleJob
    //{
    //    #region Fields

    //    public float Depth;
    //    public float DestinationLength;
    //    public string Id;
    //    public JobTypes JobType;
    //    public Load Load;
    //    public bool Rack;
    //    public object UserData;

    //    #endregion

    //    public enum JobTypes { Goto, Pick, Drop,}

    //    #region Methods

    //    public override string ToString()
    //    {
    //        string result = "";
    //        switch (JobType)
    //        {
    //            case JobTypes.Goto:
    //                result = "Type: " + JobType + ", Destination length: " + DestinationLength;
    //                break;
    //            case JobTypes.Pick:
    //                result = "Type: " + JobType + ", Destination depth: " + Depth;
    //                break;
    //            case JobTypes.Drop:
    //                result = "Type: " + JobType + ", Destination depth: " + Depth;
    //                break;
    //        }

    //        return result;
    //    }

    //    #endregion
    //}

    public class ShuttleCar : Box
    {

        public ShuttleCar(LoadInfo info): base(info){}

        public enum ExceptionTypes
        {
            [Description("None")]
            None,
            [Description("Bin Store Full (status 04)")]
            BinStoreFull,
            [Description("Bin Store Blocked (status 12)")]
            BinStoreBlocked,
            [Description("Bin Retrieve Empty (status 05)")]
            BinRetrieveEmpty,
            [Description("Bin Retrieve Blocked (status 11)")]
            BinRetrieveBlocked
        }

        ExceptionTypes exceptionType = ExceptionTypes.None;

        [Category("Status")]
        [DisplayName("Exception")]
        [Description("Create this exception in next job. Note BinStoreBlocked and BinRetrieveBlocked is only sent if depth > 1.")]

        [ReadOnly(false)]
        [Core.Properties.AlwaysEditable]
        public ExceptionTypes ExceptionType
        {
            get { return exceptionType; }
            set { exceptionType = value; }
        }
        public bool InException = false;

        [Category("Status")]
        [DisplayName("Motor status")]
        [Description("Motor status - for debugging")]
        public string MotorStatus
        {
            get
            {
                if (Disposed)
                {
                    return "DISPOSED";
                }

                if (Route == null)
                {
                    return "NO MOTOR";
                }

                if (Stopped && !Route.Motor.Running)
                {
                    return "STOPPED";
                }

                if (!Stopped && Route.Motor.Running)
                {
                    return "RUNNING";
                }

                return "UNKNOWN";
            }
        }

        [Browsable(false)]
        public override float Angle
        {
            get
            {
                return base.Angle;
            }
            set
            {
                base.Angle = value;
            }
        }

    }


    public class TrackCar : StraightTransportSection, Core.IEntity
    {
        public TrackCar(MultiShuttleInfo shuttleinfo, int level, MultiShuttle parent) : base(Color.Gray, shuttleinfo.raillength, 0.05f)
        {
            multishuttle         = parent;
            ListSolutionExplorer = true;
            this.level           = level;
            this.shuttleinfo = shuttleinfo;//TODO shuttleinfo is of type multishuttleinfo -- this is confusing

            LoadInfo shuttlecarinfo = new LoadInfo();
            shuttlecarinfo.length   = carlength;
            shuttlecarinfo.height   = carheight;
            shuttlecarinfo.width    = carwidth;
            shuttlecarinfo.color    = Color.Yellow;
            shuttlecar              = new ShuttleCar(shuttlecarinfo);

            Load.Items.Add(shuttlecar);
            route.Add(shuttlecar);

            shuttlecar.Deletable     = false;
            shuttlecar.UserDeletable = false;
            shuttlecar.Embedded      = true;
            route.Motor.Speed        = shuttleinfo.shuttlecarSpeed;
            shuttlecar.Stop();
            route.Motor.Stop();
           
            Destination                  = route.InsertActionPoint(0);
           // Destination.Visible          = true;
            Destination.OnEnter          += destination_Enter;

            shuttlecar.OnPositionChanged += Car_PositionChanged;

            //timer             = new Core.Timer(shuttleinfo.ShuttlePositioningTime);
            //nextJobDelayTimer = new Core.Timer(0.1f);
        }

        
        public override void Dispose()
        {
            shuttlecar.Dispose();
            base.Dispose();
        }
        //public class ShuttleCarController
        //{
        //    #region Fields

        //    private Shuttle Parent;

        //    private bool shuttleLocked;

        //    #endregion


        //    public ShuttleCarController(Shuttle parent)
        //    {
        //        Parent = parent;
        //    }

        //    #region Delegates

        //    public delegate void FinishedJobEvent(Shuttle shuttlecar, ShuttleJob job);

        //    public delegate void LoadHandledEvent(Shuttle shuttlecar, Load load, bool rack, string id, ShuttleJob job);

        //    #endregion

        //    #region Events

        //    public event FinishedJobEvent FinishedJob;

        //    public event LoadHandledEvent LoadDropped;

        //    public event LoadHandledEvent LoadPicked;

        //    #endregion

        //    #region Properties


        //    /// <summary>
        //    /// The current job.
        //    /// </summary>
        //    public ShuttleJob CurrentJob
        //    {
        //        get { return Parent.currentJob; }
        //    }

        //    /// <summary>
        //    /// The crane job queue.
        //    /// </summary>
        //    public List<ShuttleJob> JobQueue
        //    {
        //        get { return Parent.jobQueue; }
        //    }

        //    /// <summary>
        //    /// Returns true if the crane is running.
        //    /// </summary>
        //    public bool Running
        //    {
        //        get { return Parent.running; }
        //    }

        //    public bool ShuttleLocked
        //    {
        //        get { return shuttleLocked; }
        //    }

        //    #endregion

        //    #region Methods

        //    /// <summary>
        //    /// Reset control logic. Shuttle car is placed at distance 0.
        //    /// </summary>
        //    public void Reset()
        //    {
        //   ////     Parent.Reset();
        //    }

        //    ///// <summary>
        //    ///// Drop the current load.
        //    ///// </summary>
        //    ///// <param name="depth">Select the depth the crane should drop the load. The unit is meter.</param>
        //    ///// <param name="dropInRack">Select true if the load is dropped in a rack. The parameter is not used by the shuttle but it is passed on to the LoadDropped event.</param>
        //    ///// <param name="id">Give the job an identification.</param>
        //    ///// <param name="userdata"></param>
        //    //public void DropLoad(float depth, bool dropInRack, string id, object userdata)
        //    //{
        //    //    Parent.jobQueue.Add(new ShuttleJob { Depth = depth, JobType = ShuttleJob.JobTypes.Drop, Rack = dropInRack, Id = id, UserData = userdata });
        //    //}

        //    ///// <summary>
        //    ///// Goto a Destination.
        //    ///// </summary>
        //    ///// <param name="length">Destination lenght. Unit is meter.</param>
        //    ///// <param name="id">Give the job an identification.</param>
        //    //public void Goto(float length, string id)
        //    //{
        //    //    Parent.jobQueue.Add(new ShuttleJob { DestinationLength = length, JobType = ShuttleJob.JobTypes.Goto, Id = id });
        //    //}

        //    //public void Goto(float length, string id, object userdata)
        //    //{
        //    //    Parent.jobQueue.Add(new ShuttleJob { DestinationLength = length, JobType = ShuttleJob.JobTypes.Goto, Id = id, UserData = userdata });
        //    //}

        //    /// <summary>
        //    /// Pick a load.
        //    /// </summary>
        //    /// <param name="depth">Select the depth the shuttle car should pick the load from. The unit is meter.</param>
        //    /// <param name="load">The load to pick.</param>
        //    /// <param name="pickInRack">Select true if the load is picked from rack. The parameter is not used by the HBW but it is passed on to the LoadPicked event.</param>
        //    /// <param name="id">Give the job an identification.</param>
        //    /// <param name="userdata"></param>
        //    public void PickLoad(float depth, Load load, bool pickInRack, string id, object userdata)
        //    {
        //        Parent.jobQueue.Add(new ShuttleJob { Depth = depth, Load = load, JobType = ShuttleJob.JobTypes.Pick, Rack = pickInRack, Id = id, UserData = userdata });
        //    }

        //    /// <summary>
        //    /// If the crane has a CurrentJob this will be continued. Otherwise the crane will start on the first job in the JobQueue. 
        //    /// </summary>
        //    public void Start(bool unLockshuttle = false)
        //    {
        //        if (unLockshuttle)
        //        {
        //            shuttleLocked = false;
        //        }

        //        if (ShuttleLocked)
        //        {
        //            return;
        //        }

        //        if (!Parent.shuttlecar.InException)
        //        {
        //        ////    Parent.StartCrane();
        //        }
        //    }

        //    /// <summary>
        //    /// Stop the crane. 
        //    /// </summary>
        //    public void Stop(bool lockshuttle = false)
        //    {
        //        shuttleLocked = lockshuttle;
        //    ////    Parent.StopCrane();
        //    }

        //    //internal void MakeDropEvent()
        //    //{
        //    //    if (LoadDropped != null)
        //    //    {
        //    //        try
        //    //        {
        //    //            LoadDropped(Parent, Parent.CurrentLoad, Parent.currentJob.Rack, Parent.currentJob.Id, Parent.currentJob);
        //    //        }
        //    //        catch (Exception se)
        //    //        {
        //    //            Core.Environment.Log.Write(Parent.Name + ": Exception in LoadDropped event");
        //    //            Core.Environment.Log.Write(se);
        //    //            Core.Environment.Scene.Pause();
        //    //        }
        //    //    }
        //    //    else if (Parent.currentJob.Rack)
        //    //    {
        //    //        Parent.currentLoad.Dispose();
        //    //        Parent.currentLoad = null;
        //    //    }
        //    //}

        //    //internal void MakeFinishedJobEvent()
        //    //{
        //    //    try
        //    //    {
        //    //        if (FinishedJob != null)
        //    //            FinishedJob(Parent, Parent.currentJob);
        //    //    }
        //    //    catch (Exception se)
        //    //    {
        //    //        Core.Environment.Log.Write(Parent.Name + ": Exception in FinishedJob event");
        //    //        Core.Environment.Log.Write(se);
        //    //        Core.Environment.Scene.Pause();
        //    //    }
        //    //}

        //    //internal void MakePickEvent()
        //    //{
        //    //    if (LoadPicked != null)
        //    //    {
        //    //        try
        //    //        {
        //    //            LoadPicked(Parent, Parent.CurrentLoad, Parent.currentJob.Rack, Parent.currentJob.Id, Parent.currentJob);
        //    //        }
        //    //        catch (Exception se)
        //    //        {
        //    //            Core.Environment.Log.Write(Parent.Name + ": Exception in LoadPicked event");
        //    //            Core.Environment.Log.Write(se);
        //    //            Core.Environment.Scene.Pause();
        //    //        }
        //    //    }
        //    //}

        //    #endregion
        //}

       // private ShuttleJob currentJob;
        internal ShuttleCar shuttlecar;
        public Core.Routes.ActionPoint Destination;
        private Load currentLoad;
        private float carheight = 0.05f;
        private float carwidth = 0.5f;
        private float carlength = 0.5f;
     //   private List<ShuttleJob> jobQueue = new List<ShuttleJob>();
       // private ShuttleCarController control;
        private bool finishedmoving;
  //      private Core.Timer timer, nextJobDelayTimer;
        private MultiShuttleInfo shuttleinfo;
        private int level; //Y-coordinate
        //private string levelstring;
        private bool running;
        private MultiShuttle multishuttle;

        [Browsable(false)]
        public Load CurrentLoad
        {
            get { return currentLoad; }
        }

        [Browsable(false)]
        public MultiShuttle Multishuttle
        {
            get { return multishuttle; }
        }

        [Browsable(false)]
        public ShuttleCar ShuttleCar
        {
            get { return shuttlecar; }
        }    

        [Browsable(false)]
        public bool Deletable { get; set; }
        [Browsable(false)]
        public ulong EntityId { get; set; }
        [Browsable(false)]
        public Image Image { get { return null; } }
        [Browsable(false)]
        public bool ListSolutionExplorer { get; set; }
        [Browsable(false)]
        public bool Warning { get { return false; } }

        void destination_Enter(Core.Routes.ActionPoint sender, Load load)
        {
            shuttlecar.Stop();
            route.Motor.Stop();
            finishedmoving = true;

            if (ThisElevator != null) //if the shuttle is used as an elevator pass control back to elevator
            {
                ThisElevator.ElevatorOnArrived();
            }
            else
            {
                ThisShuttle.ShuttleOnArrived();
            }
        }

       // #region Control logic
        /// <summary>
        /// Update current load position when shuttle car moves.
        /// </summary>
        /// <param name="load"></param>
        /// <param name="pos"></param>
        void Car_PositionChanged(Load load, Vector3 pos)
        {
            if (currentLoad == null)
            {
                return;
            }

            currentLoad.Position = shuttlecar.Position + new Vector3(0, currentLoad.Height / 2 + carheight, 0);
        }


      

        #region Public Get

        //private string name;

        //[CategoryAttribute("Configuration")]
        //[DescriptionAttribute("Name")]
        //[DisplayName("Name")]
        //public new string Name
        //{
        //    get { return name; }
        //    set
        //    {
        //        name = value;
        //        shuttlecar.Identification = multishuttle.Name + " - " + value;
        //    }
        //}

        [Browsable(false)]
        public override float Pitch
        {
            get
            {
                return base.Pitch;
            }
            set
            {
                base.Pitch = value;
            }
        }
        [Browsable(false)]
        public override float Roll
        {
            get
            {
                return base.Roll;
            }
            set
            {
                base.Roll = value;
            }
        }

        [Browsable(false)]
        public override float Yaw
        {
            get
            {
                return base.Yaw;
            }
            set
            {
                base.Yaw = value;
            }
        }

        [Browsable(false)]
        public override bool Locked
        {
            get
            {
                return base.Locked;
            }
            set
            {
                base.Locked = value;
            }
        }

        [Browsable(false)]
        public override float Length
        {
            get
            {
                return base.Length;
            }
            set
            {
                base.Length = value;
            }
        }

        [Browsable(false)]
        public override Color Color
        {
            get
            {
                return base.Color;
            }
            set
            {
                base.Color = value;
            }
        }

        [Browsable(false)]
        public override Matrix Orientation
        {
            get
            {
                return base.Orientation;
            }
            set
            {
                base.Orientation = value;
            }
        }
        [Browsable(false)]
        public override Vector3 Position
        {
            get
            {
                return base.Position;
            }
            set
            {
                base.Position = value;
            }
        }
        //[Browsable(false)]
        //public ShuttleCarController Control
        //{
        //    get { return control; }
        //}
        [Browsable(false)]
        public Box Car
        {
            get { return shuttlecar; }
        }

        private Elevator thisElevator;

        [Browsable(false)]
        public Elevator ThisElevator 
        {
            set { thisElevator = value; }
            get { return thisElevator; }
            
        }

        private Shuttle thisShuttle;

        [Browsable(false)]
        public Shuttle ThisShuttle
        {
            set { thisShuttle = value; }
            get { return thisShuttle; }

        }

        [CategoryAttribute("Level")]
        [DescriptionAttribute("Level")]
        [DisplayName("Level")]
        public int Level
        {
            get { return level; }
        }

        #endregion

        //#region Dispose
        //public override void Dispose()
        //{
        //    Reset();
        //    timer.Dispose();
        //    timer.OnElapsed -= MakePick;
        //    timer.OnElapsed -= FinishPick;
        //    timer.OnElapsed -= MakeDrop;
        //    timer.OnElapsed -= FinishDrop;
        //    nextJobDelayTimer.OnElapsed -= delaytimer_Elapsed;
        //    nextJobDelayTimer.Dispose();
        //    shuttlecar.Deletable = true;
        //    shuttlecar.Dispose();
        //    Destination.Dispose();
        //    base.Dispose();
        //}

        //#endregion

    }

}
