using Experior.Core.Loads;
using Experior.Core.TransportSections;
using Microsoft.DirectX;
using System.ComponentModel;
using System.Drawing;

namespace Experior.Catalog.Dematic.Storage.MultiShuttle.Assemblies
{

    public class TrackRail : StraightTransportSection, Core.IEntity
    {
        public TrackRail(MultiShuttleInfo trackInfo, int level, MultiShuttle parent, Elevator elevator = null) : base(Color.Gray, trackInfo.raillength, 0.05f)
        {
            multishuttle         = parent;
            ListSolutionExplorer = true;
            this.level           = level;
            this.shuttleinfo     = trackInfo;//TODO shuttleinfo is of type multishuttleinfo -- this is confusing
            ThisElevator         = elevator;

            LoadInfo shuttlecarinfo = new LoadInfo() 
            {
                length = carlength, 
                height = carheight, 
                width  = carwidth, 
                color  = Color.Yellow 
            };

            shuttlecar                    = new TrackVehicle(shuttlecarinfo);
            shuttlecar.Deletable          = false;
            shuttlecar.UserDeletable      = false;
            shuttlecar.Embedded           = true;

            //if (elevator == null)
            //{
            //    shuttlecar.OnPositionChanged += Car_PositionChanged;
            //}

            Load.Items.Add(shuttlecar);

            if (ThisElevator != null)
            {
                route.Add(shuttlecar);
                Destination = route.InsertActionPoint(0);
            }
            else
            {
                route.Add(shuttlecar, multishuttle.workarround);
                Destination = route.InsertActionPoint(multishuttle.workarround);
            }

            Destination.Visible  = true;
            Destination.OnEnter += destination_Enter;
            route.Motor.Speed    = trackInfo.shuttlecarSpeed;           
            shuttlecar.Stop();
            route.Motor.Stop();

            Visible = false;
        }

        
        public override void Dispose()
        {
            Car.OnPositionChanged -= Car_PositionChanged;
            Destination.OnEnter -= destination_Enter;
            shuttlecar.Dispose();
            base.Dispose();
        }
    
        internal TrackVehicle shuttlecar;
        public Core.Routes.ActionPoint Destination;
        private Load currentLoad;
        private float carheight = 0.05f;
        private float carwidth = 0.5f;
        private float carlength = 0.5f;
        private bool finishedmoving;
        private MultiShuttleInfo shuttleinfo;
        private int level; //Y-coordinate
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
        public TrackVehicle ShuttleCar
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

        [Browsable(false)]
        public Box Car
        {
            get { return shuttlecar; }
        }

        private Elevator thisElevator;

        /// <summary>
        /// if the shuttle is used as a elevator then this will not be null.
        /// </summary>
        [Browsable(false)]        
        public Elevator ThisElevator 
        {
            set { thisElevator = value; }
            get { return thisElevator; }
            
        }

        private Shuttle thisShuttle;

        /// <summary>
        /// If the shuttle is used as a shuttle rather than a evelator then this will not be null.
        /// </summary>
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
