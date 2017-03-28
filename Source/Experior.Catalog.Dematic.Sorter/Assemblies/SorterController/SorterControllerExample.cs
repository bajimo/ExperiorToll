using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using Experior.Core.Assemblies;
using Experior.Core.Communication.TCPIP;
using Experior.Core.Loads;
using Experior.Core.Mathematics;
using Experior.Core.Parts;
using Microsoft.DirectX;
using Environment = Experior.Core.Environment;

namespace Experior.Catalog.Dematic.Sorter.Assemblies.SorterController
{
    /// <summary>
    /// Example sorter controller. Loads are sent to random destinations.
    /// </summary>
    public class SorterControllerExample : Assembly
    {
        /// <summary>
        /// A reference to the master sorter element. All communication with the sorter should go through the SorterMaster.Control object.
        /// </summary>
        [Browsable(false)]
        public SorterElement SorterMasterElement {get; private set;}
        private Connection connection;
        private readonly Dictionary<Load, List<SorterCarrier>> loadToCarriers = new Dictionary<Load, List<SorterCarrier>>();
        float inductionTime = 1;

            /// <summary>
        /// Communication
        /// </summary>
        [Browsable(false)]
        public Connection Connection
        {
            get { return connection; }
            internal set { connection = value; }
        }

        #region Construction and initializing
        public SorterControllerExample(SorterControllerExampleInfo info) : base(info)
        {
            var cube = new Cube(Color.Green, 0.5f, 0.5f, 0.5f);
            Add(cube, new Vector3(0, cube.Height / 2, 0));

            CreateConnection();

            //Check if experior is loading a model. 
            if (Environment.Scene.Loading)
                Environment.Scene.OnLoaded += Environment_LoadingCompleted;
            else
                SetSorterMaster();

        }

        private void Environment_LoadingCompleted()
        {
            Environment.Scene.OnLoaded -= Environment_LoadingCompleted;
            SetSorterMaster();
        }
        #endregion

        #region Logic

        /// <summary>
        /// This method is called whenever a load arrives at its destination sorterfixpoint (The load is on the carrier on top of the sorterfixpoint).
        /// </summary>
        /// <param name="master"></param>
        /// <param name="carrier">The carrier that arrives with the load</param>
        /// <param name="destination">The destination sorter fix point.</param>
        /// <param name="load">The load that has arrived.</param>
        /// <param name="discharged">If true then the load has been added to the chute</param>
        void Control_Load_Arrived_At_Destination(SorterMasterControl master, SorterCarrier carrier, SorterElementFixPoint destination, Load load, bool discharged)
        {
            if (discharged)
            {
                //Load was successfully discarged to the chute
                foreach (var c in loadToCarriers[load])
                {
                    //Free other carriers
                    SorterMasterElement.Control.DeleteReservation(c);
                    c.Color = Color.Empty;
                }
                loadToCarriers.Remove(load);
            }
            else
            {
                //Failed to discharge. 50/50 Take a round and try again or go to dump chute
                var dumpchute = master.FixPoints.FirstOrDefault(c => c.DumpChute);
                if (Environment.Random.Next(0, 2) == 0 && dumpchute != null)
                {
                    carrier.Color = Color.Red;   
                    master.SetLoadDestination(load, dumpchute);
                }
                else
                {
                    carrier.Color = Color.Orange;                
                    master.SetLoadDestination(load, destination);    
                }
                
            }
        }

        /// <summary>
        /// This method is called whenever a load arrives at the actionpoint induction.InductionPoint.
        /// </summary>
        /// <param name="master"></param>
        /// <param name="induction">The sorterfixpoint with the reference to the actionpoint where the load is (induction.InductionPoint).</param>
        /// <param name="load">The load that has arrived</param>
        void Control_Load_Arrived_At_Induction(SorterMasterControl master, SorterElementFixPoint induction, Load load)
        {
            load.Stop();

            //Minimum distance before induction point to look for free carriers
            var distance = SorterMasterElement.Control.Speed*inductionTime;
            var carriersToReserve = (uint)Environment.Random.Next(1, 3);//Change this to be dependent on size
            var carriers = SorterMasterElement.Control.FirstFreeCarriers(distance, induction, carriersToReserve);

            if (!carriers.Any())
            {
                Environment.Log.Write(Name + ": Induction " + induction.Name + " no free carrier found!", Color.Red);
                Environment.Scene.Pause();
                return;
            }

            //Remember carriers assigned to this load. 
            loadToCarriers[load] = carriers;

            //Reserve all carriers
            foreach (var sorterCarrier in  carriers)
            {
                sorterCarrier.Color = Color.Yellow;  //Just for visualization
                SorterMasterElement.Control.ReserveCarrier(sorterCarrier, induction.InductionPoint);     
            }

            var carrier = carriers.First();

            //Notify about arrival "inductionTime" seconds before arrival
            SorterMasterElement.Control.NotifyArrival(carrier, induction, inductionTime);
        }

        void Control_Carrier_Arrived_At_Induction(SorterMasterControl master, SorterCarrier carrier, SorterElementFixPoint induction)
        {
            //Carrier is now "inductionTime" seconds before induct point. Start releasing the load
            float distancetosorter = Math.Abs(induction.LocalOffset.Z) / (float)Math.Sin(Trigonometry.Angle2Rad(induction.LocalYaw));
            distancetosorter += (induction.InductionPoint.ActiveLoad.Route.Length - induction.InductionPoint.ActiveLoad.Distance);
            StartInducting(carrier, induction, induction.InductionPoint.ActiveLoad, inductionTime, distancetosorter);
        }

        private void StartInducting(SorterCarrier carrier, SorterElementFixPoint induction, Load load, float inductiontime, float distancetosorter)
        {
            Vector3 direction = Trigonometry.DirectionYaw(Trigonometry.Yaw(load.Route.Orientation));
            load.Translate(() => Arrived(carrier, induction), direction * distancetosorter, inductiontime);
        }

        /// <summary>
        /// This method is called whenever a carrier arrives at the induction.
        /// Override this method to handle this event.
        /// If the method is not overridden it will try to add the load to the sorter and give it a random destination.
        /// </summary>
        /// <param name="carrier">The carrier that has arrived.</param>
        /// <param name="induction">The carrier is just above this sorterfixpoint.</param>
        void Arrived(SorterCarrier carrier, SorterElementFixPoint induction)
        {
            if (induction != null && induction.InductionPoint != null && induction.InductionPoint.Active)
            {
                Load load = induction.InductionPoint.ActiveLoad;
                if (SorterMasterElement.Control.AddLoadToCarrier(load, carrier, induction.InductionPoint))
                {
                    //Get a list of possible destinations
                    List<SorterElementFixPoint> destinations = SorterMasterElement.Control.FixPointsWithChutePoint;
                    int max = destinations.Count;
                    if (max == 0)
                    {
                        Environment.Log.Write("No valid destinations found!", Color.Red);
                        Environment.Scene.Pause();
                        return;
                    }
                    int random = Environment.Random.Next(0, max);
                    SorterElementFixPoint destination = destinations[random];
                    load.UserData = "Travelling on " + carrier + ", destination chute: " + destination.Name;
                    //Set the destination
                    SorterMasterElement.Control.SetLoadDestination(load, destination);
                }
            }
        }

        #endregion

        #region Overriding Dispose, Category, Image, Reset, Inserted, Activate, Deactivate from Assembly

        public override void Activate()
        {
            base.Activate();
            if (SorterMasterElement != null)
            {
                //Start the sorter
                SorterMasterElement.SorterOn();
            }
        }

        public override void DeActivate()
        {
            base.DeActivate();
            if (SorterMasterElement != null)
            {
                //Stop the sorter
                SorterMasterElement.SorterOff();
            }
        }

        public override void Reset()
        {
            base.Reset();
            loadToCarriers.Clear();
        }

        public override void Dispose()
        {
            Environment.Scene.OnLoaded -= Environment_LoadingCompleted;

            //Unsubscribe from sorter events
            if (SorterMasterElement != null)
            {
                SorterMasterElement.Control.LoadArrivedAtInduction -= Control_Load_Arrived_At_Induction;
                SorterMasterElement.Control.LoadArrivedAtDestination -= Control_Load_Arrived_At_Destination;
                SorterMasterElement.Control.CarrierArrived -= Control_Carrier_Arrived_At_Induction;
            }
            //Unsubscribe from old connection
            if (connection != null)
            {
                connection.OnTelegramReceived -= Connection_TelegramReceived;
            }
            base.Dispose();
        }

        public override string Category
        {
            get
            {
                return "Sorter Controller";
            }
        }

        public override Image Image
        {
            get
            {
                return Common.Icons.Get("SorterController");
            }
        }

        public override void Inserted()
        {
            Info.position.Y = 0;
            Position = Info.position;
            Yaw = Info.yaw;
        }

        #endregion

        #region Set sorter master

        private void SetSorterMaster()
        {
            if (SorterMasterElement != null)
            {
                //unsubscribe from old sortermaster.Control events
                SorterMasterElement.Control.LoadArrivedAtInduction -= Control_Load_Arrived_At_Induction;
                SorterMasterElement.Control.LoadArrivedAtDestination -= Control_Load_Arrived_At_Destination;
                SorterMasterElement.Control.CarrierArrived -= Control_Carrier_Arrived_At_Induction;
            }

            SorterMasterElement = (SorterElement) Get(SorterMasterName);

            if (SorterMasterElement != null)
            {
                //Subscribe to sortermaster.Control events
                SorterMasterElement.Control.LoadArrivedAtInduction += Control_Load_Arrived_At_Induction;
                SorterMasterElement.Control.LoadArrivedAtDestination += Control_Load_Arrived_At_Destination;
                SorterMasterElement.Control.CarrierArrived += Control_Carrier_Arrived_At_Induction;
            }
        }

        #endregion

        #region Set up communication

        private void CreateConnection()
        {
            //Unsubscribe from old connection
            if (connection != null)
            {
                connection.OnTelegramReceived -= Connection_TelegramReceived;
            }

            //Find connection
            if (Core.Communication.Connection.Items.ContainsKey(((SorterControllerExampleInfo)Info).ConnectionName))
                connection = (Connection)Core.Communication.Connection.Items[((SorterControllerExampleInfo)Info).ConnectionName];

            //Subscribe to new connection
            if (connection != null)
            {
                connection.OnTelegramReceived += Connection_TelegramReceived;
            }
        }

        /// <summary>
        /// This method is called whenever the connection recieves a telegram. Override it to implement your telegram handling.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="telegram"></param>
        public virtual void Connection_TelegramReceived(Connection sender, string telegram)
        {
            Environment.Log.Write(Name + " recieved telegram: " + telegram);
        }

        #endregion

        #region User interface

        [Category("Controller Configuration")]
        [DisplayName(@"Sorter")]
        [Description("Select the sorter that this sorter control should control")]
        [TypeConverter(typeof(SorterMasterConverter))]
        public virtual string SorterMasterName
        {
            get { return ((SorterControllerExampleInfo)Info).SorterMaster; }
            set
            { 
                ((SorterControllerExampleInfo)Info).SorterMaster = value;
                SetSorterMaster();
            }
        }

        [Category("Controller Configuration")]
        [Description("The internal connection no. that this sorter control uses for communication")]
        [DisplayName(@"Connection")]
        [TypeConverter(typeof(Connection.NameConverter))]
        public string ConnectionName
        {
            get { return ((SorterControllerExampleInfo)Info).ConnectionName; }
            set
            {
                ((SorterControllerExampleInfo)Info).ConnectionName = value;
                CreateConnection();
            }
        }

        #endregion
    }

    /// <summary>
    /// Used to show a list of aviable master sorter elements
    /// </summary>
    public class SorterMasterConverter : StringConverter
    {
        public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
        {
            //true means show a combobox
            return true;
        }

        public override bool GetStandardValuesExclusive(ITypeDescriptorContext context)
        {
            //true will limit to list. false will show the list, but allow free-form entry
            return true;
        }

        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
        {
            return new StandardValuesCollection((from element in SorterElement.SorterElements where element.SorterMaster select element.Name).ToList<string>());
        }

    }
}
