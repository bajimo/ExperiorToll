using Experior.Catalog.Dematic.Sorter.Assemblies;
using Experior.Core.Loads;
using Experior.Core.Mathematics;
using Experior.Dematic.Base;
using Microsoft.DirectX;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Xml.Serialization;
using Environment = Experior.Core.Environment;
using Experior.Core.Assemblies;
using System.ComponentModel;
using Experior.Catalog.Dematic.ATC.Assemblies.Storage;
using Dematic.ATC;
using System.Text;
using Experior.Catalog.Dematic.Sorter.Assemblies.Induction;

namespace Experior.Catalog.Dematic.ATC.Assemblies.Sorters
{
    public class MHEControl_Sorter : MHEControl
    {
        SorterATCInfo sorterATCInfo;
        public SorterElement SorterMasterElement { get; private set; }
        MHEController_Sorter mheController_Sorter;
        private readonly Dictionary<Load, List<SorterCarrier>> loadToCarriers = new Dictionary<Load, List<SorterCarrier>>();
        float inductionTime = 1;

        public MHEControl_Sorter(SorterATCInfo info, SorterElement sorterElement)
        {
            Info = info;  // set this to save properties 
            sorterATCInfo = info;
            SorterMasterElement = sorterElement;
            mheController_Sorter = ((MHEController_Sorter)SorterMasterElement.Controller);
            //Check if experior is loading a model. 
            if (Environment.Scene.Loading)
            {
                Environment.Scene.OnLoaded += Environment_LoadingCompleted;
            }
            else
            {
                SetSorterMaster();
            }

        }

        private void Environment_LoadingCompleted()
        {
            Environment.Scene.OnLoaded -= Environment_LoadingCompleted;
            SetSorterMaster();
        }

        private void SetSorterMaster()
        {

            if (SorterMasterElement != null)
            {
                //unsubscribe from old sortermaster.Control events
                SorterMasterElement.Control.LoadArrivedAtInduction -= Control_Load_Arrived_At_Induction;
                SorterMasterElement.Control.LoadArrivedAtDestination -= Control_Load_Arrived_At_Destination;
                SorterMasterElement.Control.CarrierArrived -= Control_Carrier_Arrived_At_Induction;
            }

            SorterMasterElement = (SorterElement)Assembly.Get(SorterMasterName);

            if (SorterMasterElement != null)
            {
                //Subscribe to sortermaster.Control events
                SorterMasterElement.Control.LoadArrivedAtInduction += Control_Load_Arrived_At_Induction;
                SorterMasterElement.Control.LoadArrivedAtDestination += Control_Load_Arrived_At_Destination;
                SorterMasterElement.Control.CarrierArrived += Control_Carrier_Arrived_At_Induction;
            }
        }

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

                string sendTelegram = mheController_Sorter.CreateTelegramFromLoad(TelegramTypes.SorterTransportFinishedTelegram, (ATCCaseLoad)load);
                sendTelegram = sendTelegram.SetFieldValue(TelegramFields.mts, mheController_Sorter.Name);
                mheController_Sorter.SendTelegram(sendTelegram, ConnectionChannel.Main, true);
            }
            else if (destination.ChutePoint != null)
            {
                //BG Note - This was nonsense... the load should always do a lap
                //Failed to discharge. 50/50 Take a round and try again or go to dump chute
                //var dumpchute = master.FixPoints.FirstOrDefault(c => c.DumpChute);
                //if (Environment.Random.Next(0, 2) == 0 && dumpchute != null)
                //{
                //    carrier.Color = Color.Red;
                //    master.SetLoadDestination(load, dumpchute);
                //}
                //else
                //{
                //    carrier.Color = Color.Orange;
                //    master.SetLoadDestination(load, destination);
                //}

                //Send an exception that the load cannot discharge
                string sendTelegram = mheController_Sorter.CreateTelegramFromLoad(TelegramTypes.SorterTransportFinishedTelegram, (ATCCaseLoad)load);
                sendTelegram = sendTelegram.SetFieldValue(TelegramFields.mts, mheController_Sorter.Name);
                sendTelegram = sendTelegram.SetFieldValue(TelegramFields.stateCode, "DN");
                mheController_Sorter.SendTelegram(sendTelegram, ConnectionChannel.Main, true);

                carrier.Color = Color.Orange;
                master.SetLoadDestination(load, destination);

            }
            else //The load has been sent to the next induction point and needs to send a new transport request
            {
                SorterMasterElement.Control.SetLoadDestination(load, NextInductPoint(destination));

                string sendTelegram = mheController_Sorter.CreateTelegramFromLoad(TelegramTypes.TransportRequestTelegram, (ATCCaseLoad)load);
                sendTelegram = sendTelegram.SetFieldValue(TelegramFields.location, destination.Name);
                sendTelegram = sendTelegram.InsertField("CarrierID", string.Format("STCR{0}{1}", SorterID, carrier.Index.ToString().PadLeft(4, '0')));
                sendTelegram = sendTelegram.SetFieldValue(TelegramFields.mts, mheController_Sorter.NameDespatch);

                mheController_Sorter.SendTelegram(sendTelegram, ConnectionChannel.Despatch, true);
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
            var distance = SorterMasterElement.Control.Speed * inductionTime;
            var carriersToReserve = (uint)Environment.Random.Next(1, 3);//Change this to be dependent on size
            var carriers = SorterMasterElement.Control.FirstFreeCarriers(distance, induction, carriersToReserve);

            if (!carriers.Any())
            {
                //Environment.Log.Write(Name + ": Induction " + induction.Name + " no free carrier found!", Color.Red);
                Environment.Log.Write("Name here TODO" + ": Induction " + induction.Name + " no free carrier found!", Color.Red);

                Environment.Scene.Pause();
                return;
            }

            //Remember carriers assigned to this load. 
            loadToCarriers[load] = carriers;

            //Reserve all carriers
            foreach (var sorterCarrier in carriers)
            {
                sorterCarrier.Color = Color.Yellow;  //Just for visualization
                SorterMasterElement.Control.ReserveCarrier(sorterCarrier, induction.InductionPoint);
            }

            var carrier                   = carriers.First();
            string sendTelegram           = mheController_Sorter.CreateTelegramFromLoad(TelegramTypes.TransportRequestTelegram, (ATCCaseLoad)load);
            sendTelegram                  = sendTelegram.SetFieldValue(TelegramFields.location, load.CurrentActionPoint.ParentAssembly);
            SorterInduction inductStation = (SorterInduction)Assembly.Get(load.CurrentActionPoint.ParentAssembly);
            inductStation.CurrentLoad     = load;
            sendTelegram                  = sendTelegram.InsertField("CarrierID", string.Format("STCR{0}{1}", SorterID, carrier.Index.ToString().PadLeft(4,'0')));
            sendTelegram                  = sendTelegram.SetFieldValue(TelegramFields.mts, mheController_Sorter.NameDespatch);

            mheController_Sorter.SendTelegram(sendTelegram, ConnectionChannel.Despatch, true);

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
                    //List<SorterElementFixPoint> destinations = SorterMasterElement.Control.FixPointsWithChutePoint;
                    //int max = destinations.Count;
                    //if (max == 0)
                    //{
                    //    Environment.Log.Write("No valid destinations found!", Color.Red);
                    //    Environment.Scene.Pause();
                    //    return;
                    //}
                    //int random = Environment.Random.Next(0, max);
                    //SorterElementFixPoint destination = destinations[random];
                    //load.UserData = "Travelling on " + carrier + ", destination chute: " + destination.Name;

                    //Set the destination
                    List<SorterElementFixPoint> destinations = SorterMasterElement.Control.FixPointsWithChutePoint;
                    ATCCaseLoad atcLoad = load as ATCCaseLoad;
                    SorterElementFixPoint destination = destinations.Find(x => x.Name == atcLoad.Destination);

                    if (destination == null) //Set the destination to be the next induct point if the destination has not been sent by MFC
                    {
                        destination = NextInductPoint(induction);
                    }

                    SorterMasterElement.Control.SetLoadDestination(load, destination);
                }
            }
        }

        /// <summary>
        /// Finds the next induction point from the current induction point (even if there is only 1)
        /// </summary>
        /// <param name="induction">The current incucion point</param>
        /// <returns></returns>
        SorterElementFixPoint NextInductPoint(SorterElementFixPoint induction)
        {
            for (int i = 0; i < SorterMasterElement.Control.FixPointsWithInductionPoint.Count; i++)
            {
                if (SorterMasterElement.Control.FixPointsWithInductionPoint[i] == induction)
                {
                    if (i == SorterMasterElement.Control.FixPointsWithInductionPoint.Count - 1)
                    {
                        return SorterMasterElement.Control.FixPointsWithInductionPoint[0];
                    }
                    else
                    {
                        return SorterMasterElement.Control.FixPointsWithInductionPoint[i + 1];
                    }
                }
            }
            return null;
        }




        #endregion

        #region User interface

        [Category("Controller Configuration")]
        [DisplayName(@"Sorter")]
        [Description("Select the sorter that this sorter control should control")]
        [TypeConverter(typeof(SorterMasterConverter))]
        public virtual string SorterMasterName
        {
            get { return sorterATCInfo.SorterMaster; }
            set
            {
                sorterATCInfo.SorterMaster = value;
                SetSorterMaster();
            }
        }

        [Category("Controller Configuration")]
        [DisplayName(@"Sorter ID")]
        [Description("The ID of the sorter")]
        [TypeConverter(typeof(SorterMasterConverter))]
        public virtual string SorterID
        {
            get{ return sorterATCInfo.sorterID; }
            set{ sorterATCInfo.sorterID = value; }
        }

        



        //[Category("Controller Configuration")]
        //[Description("The internal connection no. that this sorter control uses for communication")]
        //[DisplayName(@"Connection")]
        //[TypeConverter(typeof(Connection.NameConverter))]
        //public string ConnectionName
        //{
        //    get { return sorterATCInfo.ConnectionName; }
        //    set
        //    {
        //        sorterATCInfo.ConnectionName = value;
        //        CreateConnection();
        //    }
        //}

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

        #endregion

        public override void Dispose()
        {
            throw new NotImplementedException();
        }
    }

    [Serializable]
    [XmlInclude(typeof(SorterATCInfo))]
    public class SorterATCInfo : ProtocolInfo
    {
        public string SorterMaster = string.Empty;
        public string ConnectionName = string.Empty;
        public string sorterID = "01";

    }

}
