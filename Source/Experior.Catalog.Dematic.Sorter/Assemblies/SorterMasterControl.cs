using System.Collections.Generic;
using Experior.Core.Loads;

namespace Experior.Catalog.Dematic.Sorter.Assemblies
{
    public class SorterMasterControl
    {
        public delegate void SorterControllerEvent(SorterMasterControl sender);
        public event SorterControllerEvent Started;
        public event SorterControllerEvent Stopped;
        public delegate void CarrierEvent(SorterMasterControl master, SorterCarrier carrier, SorterElementFixPoint induction);
        public delegate void CarrierLoadDestinationEvent(SorterMasterControl master, SorterCarrier carrier, SorterElementFixPoint destination, Load load, bool discharged);
        public delegate void CarrierLoadInductionEvent(SorterMasterControl master, SorterElementFixPoint induction, Load load);
        public event CarrierEvent CarrierArrived;
        public event CarrierLoadDestinationEvent LoadArrivedAtDestination;
        public event CarrierLoadInductionEvent LoadArrivedAtInduction;

        readonly SorterElement parent;

        public SorterMasterControl(SorterElement parent)
        {
            this.parent = parent;
        }

        public void SorterOn()
        {
            parent.SorterOn();
            OnStartedEvent();
        }

        public void SorterOff()
        {
            parent.SorterOff();
            OnStoppedEvent();
        }

        /// <summary>
        /// Calculate the time in seconds to arrival of the carrier at the FixPoint.
        /// Returns -1 if the FixPoint is not found.
        /// </summary>
        /// <param name="carrier"></param>
        /// <param name="fixPoint"></param>
        /// <returns></returns>
        public float TimeToArrival(SorterCarrier carrier, string fixPoint)
        {
            return parent.TimeToArrival(carrier, fixPoint);
        }

        /// <summary>
        /// Calculate the time in seconds to arrival of the carrier at the FixPoint.
        /// </summary>
        /// <param name="carrier"></param>
        /// <param name="fixpoint"></param>
        /// <returns></returns>
        public float TimeToArrival(SorterCarrier carrier, SorterElementFixPoint fixpoint)
        {
            return parent.TimeToArrival(carrier, fixpoint);
        }

        /// <summary>
        /// The method will look for a free carrier starting from FixPoint global distance minus MinimumDistanceFromFixPoint.
        /// </summary>
        /// <param name="minimumDistanceFromFixPoint">Distance in meter.</param>
        /// <param name="fixPointName"></param>
        /// <returns></returns>
        public SorterCarrier FirstFreeCarrier(float minimumDistanceFromFixPoint, string fixPointName)
        {
            return parent.FirstFreeCarrier(minimumDistanceFromFixPoint, fixPointName);
        }

        /// <summary>
        /// The method will look for a free carrier starting from FixPoint global distance minus MinimumDistanceFromFixPoint.
        /// </summary>
        /// <param name="minimumDistanceFromFixPoint">Distance in meter.</param>
        /// <param name="f"></param>
        /// <returns>the first free carrier. Could be null if no free carrier is found.</returns>
        public SorterCarrier FirstFreeCarrier(float minimumDistanceFromFixPoint, SorterElementFixPoint f)
        {
            return parent.FirstFreeCarrier(minimumDistanceFromFixPoint, f);
        }

        /// <summary>
        /// The method will look for free carriers starting from FixPoint global distance minus minimumDistanceFromFixPoint.
        /// </summary>
        /// <param name="minimumDistanceFromFixPoint">Distance in meter.</param>
        /// <param name="f"></param>
        /// <param name="consecutiveCarriers">Number of consecutive free carriers</param>
        /// <returns>List of free carriers. Could be empty if no free carrier is found.</returns>
        public List<SorterCarrier> FirstFreeCarriers(float minimumDistanceFromFixPoint, SorterElementFixPoint f, uint consecutiveCarriers)
        {
            return parent.FirstFreeCarriers(minimumDistanceFromFixPoint, f, consecutiveCarriers);
        }

        /// <summary>
        /// Get the first free carrier before this distance. 
        /// </summary>
        /// <param name="globalDistance">Global distance in mm</param>
        /// <returns>First free carrier. If no free carrier is found it returns null.</returns>
        public SorterCarrier FirstFreeCarrier(int globalDistance)
        {
            return parent.FirstFreeCarrier(globalDistance);
        }

        /// <summary>
        /// Get the first free carriers before this distance. 
        /// </summary>
        /// <param name="globalDistance">Global distance in mm</param>
        /// <param name="consecutiveCarriers">Number of free consecutive carriers</param>
        /// <returns>First free carrier. If no free carrier is found it returns null.</returns>
        public List<SorterCarrier> FirstFreeCarriers(int globalDistance, uint consecutiveCarriers)
        {
            return parent.FirstFreeCarriers(globalDistance, consecutiveCarriers);
        }

        /// <summary>
        /// Calling this method will cause a CarrierArrived event when the carrier arrives at the FixPoint. 
        /// </summary>
        /// <param name="carrier">The carrier that should arrive at the FixPoint</param>
        /// <param name="fixPointName"></param>
        /// <returns>Returns false if there is another notification waiting for this FixPoint.
        /// Returns false if sorter is not initialized.
        /// Returns false if there is no fixpoint with this name.</returns>
        public bool NotifyArrival(SorterCarrier carrier, string fixPointName)
        {
            return parent.NotifyArrival(carrier, fixPointName);
        }

        /// <summary>
        /// Calling this method will cause a CarrierArrived event when the carrier arrives at the FixPoint. 
        /// If a sorter controller is listening to this event it will handle the event. Otherwise nothing will happen.
        /// </summary>
        /// <param name="carrier">The carrier that should arrive at the FixPoint</param>
        /// <param name="f"></param>
        /// <returns>Returns false if there is another notification waiting for this FixPoint.</returns>
        public bool NotifyArrival(SorterCarrier carrier, SorterElementFixPoint f)
        {
            return parent.NotifyArrival(carrier, f);
        }

        /// <summary>
        /// Calling this method will cause a CarrierArrived event when the carrier arrives at the FixPoint. 
        /// If a sorter controller is listening to this event it will handle the event. Otherwise nothing will happen.
        /// </summary>
        /// <param name="carrier">The carrier that should arrive at the FixPoint</param>
        /// <param name="f"></param>
        /// /// <param name="leadtime"></param>
        /// <returns>Returns false if there is another notification waiting for this FixPoint.</returns>
        public bool NotifyArrival(SorterCarrier carrier, SorterElementFixPoint f, float leadtime)
        {
            return parent.NotifyArrival(carrier, f, leadtime);
        }

        /// <summary>
        /// Adds the load to the carrier. 
        /// </summary>
        /// <param name="load"></param>
        /// <param name="carrier"></param>
        /// <param name="reservationKey">Can be any thing</param>
        /// <returns>Returns true if the load is successfully added.
        /// Returns false if the load has waitingtime (load.Waiting == true)
        /// Returns false if the load is already added.</returns>
        public bool AddLoadToCarrier(Load load, SorterCarrier carrier, object reservationKey)
        {
            return parent.AddLoadToCarrier(load, carrier, reservationKey);
        }

        /// <summary>
        /// Reserve a carrier. When the carrier is reserved it is only free to the reserver.
        /// </summary>
        /// <param name="carrier">The carrier to reserve</param>
        /// <param name="reservationKey">the key. Can be any thing</param>
        /// <returns>Returns true if the reservartion is succesfull. Otherwise false.</returns>
        public bool ReserveCarrier(SorterCarrier carrier, object reservationKey)
        {
            if (carrier.ReservationKey != null)
                return false;

            carrier.ReservationKey = reservationKey;
            return true;
        }

        /// <summary>
        /// Delete the carrier reserve key.
        /// </summary>
        /// <param name="carrier"></param>
        public void DeleteReservation(SorterCarrier carrier)
        {
            carrier.ReservationKey = null;
        }

        /// <summary>
        /// Set the destination for this load. When the load arrives a LoadArrived event will occur.
        /// </summary>
        /// <param name="load"></param>
        /// <param name="destination"></param>
        /// <returns>Returns true if the destination is successfully set.</returns>
        public bool SetLoadDestination(Load load, SorterElementFixPoint destination)
        {
            return parent.SetLoadDestination(load, destination);
        }

        /// <summary>
        /// Set the destination for this load. When the load arrives a LoadArrived event will occur.
        /// </summary>
        /// <param name="load"></param>
        /// <param name="destination"></param>
        /// <param name="leadtime"></param>
        /// <returns>Returns true if the destination is successfully set.</returns>
        public bool SetLoadDestination(Load load, SorterElementFixPoint destination, float leadtime)
        {
            return parent.SetLoadDestination(load, destination, leadtime);
        }

        public bool Running
        {
            get { return parent.Running; }
        }

        /// <summary>
        /// Set the destination for this load. When the load arrives a LoadArrived event will occur.
        /// </summary>
        /// <param name="load"></param>
        /// <param name="fixpointdestination"></param>
        /// <returns>Returns true if the destination is successfully set.</returns>
        public bool SetLoadDestination(Load load, string fixpointdestination)
        {
            return parent.SetLoadDestination(load, fixpointdestination, 0);
        }

        public SorterElementFixPoint GetSorterFixPoint(string name)
        {
            return parent.GetSorterFixPoint(name);
        }

        /// <summary>
        /// Cancel the current destination for this load.
        /// </summary>
        /// <param name="load"></param>
        /// <returns></returns>
        public bool Cancel(Load load)
        {
            return parent.Cancel(load);
        }
        /// <summary>
        /// Get a list of all sorter fixpoints
        /// </summary>
        /// <returns></returns>
        public List<SorterElementFixPoint> FixPoints
        {
            get { return parent.FixPointsList; }
        }

        /// <summary>
        /// Get a list of all sorter fixpoints with a chute point.
        /// Note: This method just returns FixPointsList.FindAll(f => f.ChutePoint != null);
        /// </summary>
        /// <returns></returns>
        public List<SorterElementFixPoint> FixPointsWithChutePoint
        {
            get { return parent.FixPointsList.FindAll(f => f.ChutePoint != null); }
        }

        /// <summary>
        /// Get a list of all sorter fixpoints with an induction point.
        /// Note: This method just returns FixPointsList.FindAll(f => f.InductionPoint != null);
        /// </summary>
        /// <returns></returns>
        public List<SorterElementFixPoint> FixPointsWithInductionPoint
        {
            get { return parent.FixPointsList.FindAll(f => f.InductionPoint != null); }
        }

        /// <summary>
        /// Get a list with the names of all sorter fixpoints
        /// </summary>
        /// <returns></returns>
        public List<string> PointNames
        {
            get { return parent.FixPointNames; }
        }

        /// <summary>
        /// Total sorter length in meter.
        /// </summary>
        public float SorterLength
        {
            get { return parent.TotalSorterLength; }
        }

        /// <summary>
        /// If the sorter is initialized it is ready for communication.
        /// </summary>
        public bool Initialized
        {
            get { return parent.Initialized; }
        }

        internal void OnCarrierArrivedEvent(SorterCarrier carrier, SorterElementFixPoint f)
        {
            if (CarrierArrived != null)
                CarrierArrived(parent.Control, carrier, f);

            if (f.InductionPoint != null && f.InductionPoint.ActiveLoad != null && f.InductionPoint.ActiveLoad.Stopped)
                f.Arriving(carrier, f.InductionPoint.ActiveLoad);

        }

        internal void OnLoadDestinationArrivedEvent(SorterCarrier carrier, SorterElementFixPoint destination, Load load, bool discharged)
        {
            if (LoadArrivedAtDestination != null)
                LoadArrivedAtDestination(parent.Control, carrier, destination, load, discharged);

            destination.Arriving(carrier, load);
        }

        internal void OnLoadInductionArrivedEvent(SorterElementFixPoint induction, Load load)
        {
            if (LoadArrivedAtInduction != null)
                LoadArrivedAtInduction(parent.Control, induction, load);
        }

        internal void OnStartedEvent()
        {
            if (Started != null)
                Started(this);
        }

        internal void OnStoppedEvent()
        {
            if (Stopped != null)
                Stopped(this);
        }

        public float Speed
        {
            get { return parent.Speed; }
        }
    }
}