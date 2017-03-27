using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using Experior.Catalog.Dematic.Sorter.Assemblies.Chute;
using Experior.Core;
using Experior.Core.Assemblies;
using Experior.Core.Loads;
using Experior.Core.Mathematics;
using Experior.Core.Parts;
using Experior.Core.Properties;
using Experior.Core.Properties.Collections;
using Experior.Core.Properties.TypeConverter;
using Experior.Core.Routes;
using Microsoft.DirectX;
using Environment = Experior.Core.Environment;
using Mesh = Experior.Core.Parts.Mesh;
using Experior.Dematic.Base;

namespace Experior.Catalog.Dematic.Sorter.Assemblies
{
    public abstract class SorterElement : Assembly, IControllable
    {
        /// <summary>
        /// All control of the sorter is done through this control object. 
        /// Only the master element control should be called.
        /// </summary>
        [Browsable(false)]
        public SorterMasterControl Control { get; private set; }

        /// <summary>
        /// List of all sorter elements
        /// </summary>
        public static List<SorterElement> SorterElements = new List<SorterElement>();

        public enum SorterTypes
        {
            TiltTray,
            CrossBelt,
            Cube,
            Edge
        }

        public delegate void SorterElementEvent(SorterElement sender);

        public event SorterElementEvent OnInitialized;
        public event SorterElementEvent OnSettingMasterElement;

        private bool running = true;
        private double stoppedtime, stoptime;
        private const float Minimumtimerresolution = 0.005f;
        private int masterindex;
        private bool initialized;
        private double carriersLastUpdateTime;
        private readonly FixPoint endFixPoint;
        private readonly FixPoint startFixPoint;
        private SorterElement masterElement;
        private readonly Dictionary<Load, float> loadCarrierStopped = new Dictionary<Load, float>();
        private readonly SorterElementInfo sorterElementInfo;
        private Cube masterCube;
        private readonly List<SorterElement> elements = new List<SorterElement>();
        private readonly List<SorterElementFixPoint> masterFixPoints = new List<SorterElementFixPoint>();
        private readonly List<string> masterFixPointNames = new List<string>();
        private List<SorterCarrier> carriers = new List<SorterCarrier>();
        private Matrix carrierScaling = Matrix.Identity;
        private SorterElement next;
        private readonly Dictionary<int, SorterCarrier> windowCarrier = new Dictionary<int, SorterCarrier>();
        private readonly Dictionary<Load, SorterCarrier> loadCarrier = new Dictionary<Load, SorterCarrier>(); //Map the load to the carrier

        internal Dictionary<int, TrackTransformation> Track = new Dictionary<int, TrackTransformation>();

        protected SorterElement(SorterElementInfo info): base(info)
        {
            sorterElementInfo = info;           
            carriersLastUpdateTime = -1;

            startFixPoint = new FixPoint(Color.Red, FixPoint.Types.Start, this);
            endFixPoint = new FixPoint(Color.Blue, FixPoint.Types.End, this);

            Add(startFixPoint);
            Add(endFixPoint);
            startFixPoint.LocalRoll = Trigonometry.Angle2Rad(info.StartFixLocalRoll);
            endFixPoint.LocalRoll = Trigonometry.Angle2Rad(info.EndFixLocalRoll);

            startFixPoint.OnSnapped += StartFixPoint_Snapped;
            startFixPoint.OnUnSnapped += StartFixPoint_UnSnapped;

            endFixPoint.OnSnapped += EndFixPoint_Snapped;
            endFixPoint.OnUnSnapped += EndFixPoint_UnSnapped;

            SorterElements.Add(this);

            foreach (SorterElementFixPoint f in info.FixPoints)
            {
                InitializeFixPoint(f);
            }

            Control = new SorterMasterControl(this);
            SetMasterGraphics();
            Core.Environment.Scene.OnLoaded += Scene_OnLoaded;
        }

        private void Scene_OnLoaded()
        {
            if (ControllerProperties == null)
            {
                ControllerProperties = StandardCase.SetMHEControl(sorterElementInfo, this);
            }
        }

        #region Communication Interface

        internal List<string> FixPointNames
        {
            get
            {
                if (SorterMaster)
                {
                    return masterFixPointNames;
                }

                return masterElement.FixPointNames;
            }
        }

        internal List<SorterElementFixPoint> FixPointsList
        {
            get
            {
                if (SorterMaster)
                {
                    return masterFixPoints;

                }

                return masterElement.FixPointsList;
            }
        }

        internal void SorterOff()
        {
            stoptime = Environment.Time.Simulated;

            if (!SorterMaster)
            {
                masterElement.SorterOff();
                return;
            }

            foreach (SorterElementFixPoint f in SorterFixPointList)
            {
                f.Stop();
            }

            foreach (SorterElement element in elements)
            {
                foreach (SorterElementFixPoint f in element.SorterFixPointList)
                {
                    f.Stop();
                }
            }

            foreach (Load load in loadCarrier.Keys)
            {
                if (load.WaitingTime > 0)
                {
                    if (!loadCarrierStopped.ContainsKey(load))
                        loadCarrierStopped.Add(load, load.WaitingTime);

                    load.OnFinishedWaitingEvent -= LoadArrivedAtDestination;
                    load.WaitingTime = 0;
                }
            }

            if (!running)
                return;

            running = false;
        }

        internal void SorterOn()
        {
            stoppedtime += Environment.Time.Simulated - stoptime;

            if (!SorterMaster)
            {
                masterElement.SorterOn();
                return;
            }

            foreach (SorterElementFixPoint f in SorterFixPointList)
            {
                f.Start();
            }

            foreach (SorterElement element in elements)
            {
                foreach (SorterElementFixPoint f in element.SorterFixPointList)
                {
                    f.Start();
                }
            }

            //Set the waiting time again after restart
            foreach (KeyValuePair<Load, float> watingloads in loadCarrierStopped)
            {
                if (watingloads.Value > Minimumtimerresolution)
                {
                    watingloads.Key.WaitingTime = watingloads.Value;

                    watingloads.Key.OnFinishedWaitingEvent -= LoadArrivedAtDestination;
                    watingloads.Key.OnFinishedWaitingEvent += LoadArrivedAtDestination;
                }
                else
                {
                    LoadArrivedAtDestination(watingloads.Key);
                }
            }

            loadCarrierStopped.Clear();

            if (running)
                return;

            running = true;
        }


        /// <summary>
        /// Calculate the time in seconds to arrival of the carrier at the FixPoint.
        /// Returns -1 if the FixPoint is not found.
        /// </summary>
        /// <param name="carrier"></param>
        /// <param name="fixPointName"></param>
        /// <returns></returns>
        internal float TimeToArrival(SorterCarrier carrier, string fixPointName)
        {
            if (!Initialized)
            {
                Environment.Log.Write("Sorter has not been initialized!", Environment.Log.Filter.System);
                return -1;
            }

            if (!SorterMaster)
                return masterElement.TimeToArrival(carrier, fixPointName);

            SorterElementFixPoint f = masterFixPoints.Find(com => com.Name == fixPointName);
            if (f == null)
            {
                Environment.Log.Write("Fixpoint with name: '" + fixPointName + "' not found!");
                return -1;
            }

            return TimeToArrival(carrier, f);

        }

        /// <summary>
        /// Calculate the time in seconds to arrival of the carrier at the FixPoint.
        /// </summary>
        /// <param name="carrier"></param>
        /// <param name="fixpoint"></param>
        /// <returns></returns>
        internal float TimeToArrival(SorterCarrier carrier, SorterElementFixPoint fixpoint)
        {
            if (!Initialized)
            {
                Environment.Log.Write("Sorter has not been initialized!", Environment.Log.Filter.Error);
                return -1;
            }

            if (!SorterMaster)
                return masterElement.TimeToArrival(carrier, fixpoint);

            UpdateCarriers();

            float time = DistanceToFixpoint(fixpoint, carrier) / masterElement.Speed;

            return time;
        }

        /// <summary>
        /// The method will look for a free carrier starting from FixPoint global distance minus MinimumDistanceFromFixPoint.
        /// </summary>
        /// <param name="minimumDistanceFromFixPoint">Distance in meter.</param>
        /// <param name="fixPoint"></param>
        /// <returns></returns>
        internal SorterCarrier FirstFreeCarrier(float minimumDistanceFromFixPoint, string fixPoint)
        {
            if (!Initialized)
            {
                Environment.Log.Write("Sorter has not been initialized!", Environment.Log.Filter.Error);
                return null;
            }

            if (!SorterMaster)
                return masterElement.FirstFreeCarrier(minimumDistanceFromFixPoint, fixPoint);

            UpdateCarriers();

            SorterElementFixPoint f = masterFixPoints.Find(com => com.Name == fixPoint);
            if (f == null)
            {
                Environment.Log.Write("Fixpoint with name: '" + fixPoint + "' not found!");
                return null;
            }

            return FirstFreeCarrier(minimumDistanceFromFixPoint, f);
        }

        /// <summary>
        /// The method will look for a free carrier starting from FixPoint global distance minus MinimumDistanceFromFixPoint.
        /// </summary>
        /// <param name="minimumDistanceFromFixPoint">Distance in meters.</param>
        /// <param name="f"></param>
        /// <returns>The first free carrier. Could be null if no free carrier is found.</returns>
        internal SorterCarrier FirstFreeCarrier(float minimumDistanceFromFixPoint, SorterElementFixPoint f)
        {
            return FirstFreeCarrier((int)((f.GlobalDistance - minimumDistanceFromFixPoint) * 1000));
        }

        /// <summary>
        ///  The method will look for free carriers starting from FixPoint global distance minus MinimumDistanceFromFixPoint.
        /// </summary>
        /// <param name="minimumDistanceFromFixPoint">Distance in meters</param>
        /// <param name="f"></param>
        /// <param name="consecutiveCarriers">Number of free consecutive carriers</param>
        /// <returns></returns>
        internal List<SorterCarrier> FirstFreeCarriers(float minimumDistanceFromFixPoint, SorterElementFixPoint f, uint consecutiveCarriers)
        {
            return FirstFreeCarriers((int)((f.GlobalDistance - minimumDistanceFromFixPoint) * 1000), consecutiveCarriers);
        }

        /// <summary>
        /// Reserve a carrier. If the carrier is reserved then it is only free to for the reserver.
        /// </summary>
        /// <param name="carrier">The carrier to reserve</param>
        /// <param name="reservationKey">Can be any thing</param>
        /// <returns>Returns true if the reservartion is succesfull. Otherwise false.</returns>
        internal bool ReserveCarrier(SorterCarrier carrier, object reservationKey)
        {
            if (carrier.ReservationKey != null || carrier.CurrentLoad != null)
                return false;

            carrier.ReservationKey = reservationKey;
            return true;
        }

        /// <summary>
        /// Delete the carrier reserve key.
        /// </summary>
        /// <param name="carrier"></param>
        internal void DeleteReservation(SorterCarrier carrier)
        {
            carrier.ReservationKey = null;
        }

        /// <summary>
        /// Get a list of free carriers searching from "distance" and back.
        /// </summary>
        /// <param name="distance">Global distance in mm</param>
        /// <param name="consecutiveCarriers">Number consecutive free carriers</param>
        /// <returns>If no free carriers are found the list is empty</returns>
        internal List<SorterCarrier> FirstFreeCarriers(int distance, uint consecutiveCarriers)
        {
            List<SorterCarrier> result = new List<SorterCarrier>();

            if (!Initialized)
            {
                Environment.Log.Write("Sorter has not been initialized!", Environment.Log.Filter.Error);
                return null;
            }

            if (!SorterMaster)
                return masterElement.FirstFreeCarriers(distance, consecutiveCarriers);

            if (distance < 0)
                distance = Track.Count + distance;

            UpdateCarriers();

            if (distance > Track.Count || distance < 0)
                return result;

            var carrier = GetSorterCarrier(distance);

            //Start looking for free carriers
            for (int i = 0; i < carriers.Count; i++)
            {
                carrier = carrier.Previous;
                var test = carrier;

                for (int j = 0; j < consecutiveCarriers; j++)
                {
                    //We need "consecutiveCarriers" consecutive free carriers
                    if (test.Enabled && test.CurrentLoad == null && test.ReservationKey == null && !result.Contains(test))
                    {
                        result.Add(test);
                    }
                    test = test.Previous;
                }

                if (result.Count == consecutiveCarriers)
                {
                    //Succes
                    return result;
                }

                //We did not find enough free carriers so try the previous
                result.Clear();
            }

            return result;
        }

        /// <summary>
        /// Get the first free carrier before this distance. 
        /// </summary>
        /// <param name="distance">Global distance in mm</param>
        /// <returns>First free carrier. If no free carrier is found it returns null.</returns>
        internal SorterCarrier FirstFreeCarrier(int distance)
        {
            if (!Initialized)
            {
                Environment.Log.Write("Sorter has not been initialized!", Environment.Log.Filter.Error);
                return null;
            }

            if (!SorterMaster)
                return masterElement.FirstFreeCarrier(distance);

            if (distance < 0)
                distance = Track.Count + distance;

            UpdateCarriers();

            if (distance > Track.Count || distance < 0)
                return null;

            var carrier = GetSorterCarrier(distance);

            //Start looking for free carrier
            SorterCarrier result = null;
            for (int i = 0; i < carriers.Count; i++)
            {
                carrier = carrier.Previous;
                if (carrier.Enabled && carrier.CurrentLoad == null && carrier.ReservationKey == null)
                {
                    result = carrier;
                    break;
                }
            }

            return result;
        }

        private SorterCarrier GetSorterCarrier(int distance)
        {
            int window = (int)(distance / (CarrierLengthPlusSpacing * 1000));
            if (window == carriers.Count)
                window = 0;

            if (windowCarrier[window].Enabled && windowCarrier[window].CurrentLoad == null &&
                windowCarrier[window].ReservationKey == null)
                return windowCarrier[window];

            SorterCarrier carrier = windowCarrier[window];

            return carrier;
        }

        /// <summary>
        /// Calling this method will cause a CarrierArrived event when the carrier arrives at the FixPoint. 
        /// </summary>
        /// <param name="carrier">The carrier that should arrive at the FixPoint</param>
        /// <param name="fixPoint"></param>
        /// <returns>Returns false if there is another notification waiting for this FixPoint.
        /// Returns false if sorter is not initialized.
        /// Returns false if there is no fixpoint with this name.</returns>
        internal bool NotifyArrival(SorterCarrier carrier, string fixPoint)
        {
            if (!Initialized)
            {
                Environment.Log.Write("Sorter has not been initialized!", Environment.Log.Filter.Error);
                return false;
            }

            if (carrier == null)
            {
                Environment.Log.Write("Carrier can not be null!");
                return false;
            }

            if (!SorterMaster)
                return masterElement.NotifyArrival(carrier, fixPoint);

            SorterElementFixPoint f = masterFixPoints.Find(com => com.Name == fixPoint);
            if (f == null)
            {
                Environment.Log.Write("Fixpoint with name: '" + fixPoint + "' not found!");
                return false;
            }

            return NotifyArrival(carrier, f);
        }

        internal bool NotifyArrival(SorterCarrier carrier, SorterElementFixPoint f)
        {
            return NotifyArrival(carrier, f, 0);
        }

        /// <summary>
        /// Calling this method will cause a CarrierArrived event when the carrier arrives at the FixPoint. 
        /// </summary>
        /// <param name="carrier">The carrier that should arrive at the FixPoint</param>
        /// <param name="f"></param>
        /// <param name="leadtime"></param>
        /// <returns>Returns false if there is another notification waiting for this FixPoint or the lead time is less than arrival time.</returns>
        internal bool NotifyArrival(SorterCarrier carrier, SorterElementFixPoint f, float leadtime)
        {
            if (!Initialized)
            {
                Environment.Log.Write("Sorter has not been initialized!", Environment.Log.Filter.Error);
                return false;
            }

            if (!SorterMaster)
                return masterElement.NotifyArrival(carrier, f);

            if (f.Timer.Running)
            {
                Environment.Log.Write("Another arrival event is waiting");
                return false;
            }

            float time = TimeToArrival(carrier, f);

            if (time < leadtime)
                return false;

            time -= leadtime;

            f.CarrierArriving = carrier;

            if (time > Minimumtimerresolution)
            {
                f.Timer.Timeout = time;

                if (MasterElement.Running)
                    f.Timer.Start();
                else
                    f.RestartTimer = true;
            }
            else
                CarrierArrivedAtSorterFixPoint(f.Timer);

            return true;
        }

        /// <summary>
        /// Adds the load to the carrier. 
        /// </summary>
        /// <param name="load"></param>
        /// <param name="carrier"></param>
        /// <param name="reserveKey"></param>
        /// <returns>Returns true if the load is added.</returns>
        internal bool AddLoadToCarrier(Load load, SorterCarrier carrier, object reserveKey)
        {
            if (load == null || carrier == null)
            {
                Environment.Log.Write("Load or carrier can not be null when added to the sorter");
                return false;
            }

            if (!Initialized)
            {
                Environment.Log.Write("Sorter has not been initialized!");
                return false;
            }

            if (!SorterMaster)
                return masterElement.AddLoadToCarrier(load, carrier, reserveKey);

            if (carrier.ReservationKey != reserveKey)
            {
                Environment.Log.Write("You need the reserve key to add a load to the carrier!");
                return false;
            }

            if (carrier.CurrentLoad != null)
            {
                Environment.Log.Write("The carrier is not empty! The load is not added to the carrier.");
                return false;
            }

            if (load.Waiting)
            {
                Environment.Log.Write("Load has waiting time and can not be added to the sorter");
                return false;
            }

            if (loadCarrier.ContainsKey(load))
            {
                Environment.Log.Write("Load is already added");
                return false;
            }

            //Add load
            load.OnDisposing += RemoveLoadFromSorter;
            loadCarrier[load] = carrier;
            carrier.CurrentLoad = load;
            if (Environment.Engine.Events)
            {
                load.Deletable = false;
                load.UserDeletable = true;
            }
            else
            {
                load.Part.Kinematic = true;
            }
            carrier.ReservationKey = null;

            if (load.Route != null)
            {
                load.Route.Remove(load);
            }

            carrier.CurrentLoadYaw = Trigonometry.Yaw(load.Orientation) -
                                  Trigonometry.Yaw(Track[carrier.CurrentDistance].Orientation);

            SetLoadCarrierOffset(load, carrier);

            UpdateCarriers();

            return true;
        }

        private void SetLoadCarrierOffset(Load load, SorterCarrier carrier)
        {
            Vector3 loaddirection = new Vector3(0, 0, 1);
            loaddirection.TransformCoordinate(Matrix.RotationY(carrier.CurrentLoadYaw));

            Vector3 carrierDirection = new Vector3(0, 0, 1);

            float width = Vector3.Dot(loaddirection, new Vector3(-carrierDirection.Z, carrierDirection.Y, carrierDirection.X)) *
                          load.Width;
            float length = Vector3.Dot(loaddirection, carrierDirection) * load.Length;
            if (width < 0)
                width *= -1;
            if (length < 0)
                length *= -1;

            float distance = length / 2 + width / 2;

            if (LoadCarrierPosition == ActionPoint.Edges.Leading)
                carrier.CurrentLoadOffset = distance - CarrierLength / 2 + 0.05f;
            else if (LoadCarrierPosition == ActionPoint.Edges.Trailing)
                carrier.CurrentLoadOffset = -distance + CarrierLength / 2 - 0.05f;
            else
                carrier.CurrentLoadOffset = 0;
        }

        /// <summary>
        /// Set the destination for this load. When the load arrives a LoadArrived event will occur.
        /// </summary>
        /// <param name="load"></param>
        /// <param name="fixpointdestination"></param>
        /// <param name="leadtime"></param>
        /// <returns>Returns true if the destination is successfully set.</returns>
        internal bool SetLoadDestination(Load load, string fixpointdestination, float leadtime)
        {
            if (!Initialized)
            {
                Environment.Log.Write("Sorter has not been initialized!");
                return false;
            }

            if (!SorterMaster)
                return masterElement.SetLoadDestination(load, fixpointdestination, leadtime);

            SorterElementFixPoint f = masterFixPoints.Find(com => com.Name == fixpointdestination);
            if (f == null)
            {
                Environment.Log.Write("Fixpoint with name: '" + fixpointdestination + "' not found!");
                return false;
            }
            return SetLoadDestination(load, f, leadtime);
        }

        internal SorterElementFixPoint GetSorterFixPoint(string name)
        {
            if (!SorterMaster)
                return GetSorterFixPoint(name);

            return masterFixPoints.Find(com => com.Name == name);
        }

        internal bool Cancel(Load load)
        {
            if (!Initialized)
            {
                Environment.Log.Write("Sorter has not been initialized!");
                return false;
            }

            if (!SorterMaster)
                return masterElement.Cancel(load);

            if (!loadCarrier.ContainsKey(load))
                return false;

            SorterCarrier carrier = loadCarrier[load];

            if (carrier.CurrentTiltAngle != 0) //Already tilting
                return false;

            load.OnFinishedWaitingEvent -= LoadArrivedAtDestination;
            load.WaitingTime = 0;

            if (loadCarrierStopped.ContainsKey(load))
                loadCarrierStopped.Remove(load);

            carrier.CurrentDestination = null;
            carrier.StopTiltingDistance = 0;
            carrier.CurrentTiltAngle = 0;

            return true;
        }

        /// <summary>
        /// Set the destination for this load. When the load arrives a LoadArrived event will occur.
        /// </summary>
        /// <param name="load"></param>
        /// <param name="destination"></param>
        /// <returns>Returns true if the destination is successfully set.</returns>
        internal bool SetLoadDestination(Load load, SorterElementFixPoint destination)
        {
            return SetLoadDestination(load, destination, 0);
        }

        /// <summary>
        /// Set the destination for this load. When the load arrives a LoadArrived event will occur.
        /// </summary>
        /// <param name="load"></param>
        /// <param name="destination"></param>
        /// /// <param name="leadtime"></param>
        /// <returns>Returns true if the destination is successfully set.</returns>
        internal bool SetLoadDestination(Load load, SorterElementFixPoint destination, float leadtime)
        {
            if (!Initialized)
            {
                Environment.Log.Write("Sorter has not been initialized!");
                return false;
            }

            if (!SorterMaster)
                return masterElement.SetLoadDestination(load, destination);


            if (!loadCarrier.ContainsKey(load))
            {
                //Environment.Log.Write("Can not set the destination. The load is not added to the sorter");
                //For ATC each time the sorter mission message is received then SetLoadDestination is called even if the load has not entered the sorter yet and is travelling on the induct
                //Perhaps the request message for the destination is sent too early and it should only be sent when the load is at the fixpoint and therefore on the sorter
                //This could be changed later if required, for now i have disabled this message to remove clutter in the log. BG 15/10/15
                return false;
            }

            //Set destination
            SorterCarrier carrier = loadCarrier[load];
            carrier.CurrentDestination = destination;

            if (destination == null)
            {
                Environment.Log.Write("Carrier and Load have no destination, cannot route load");
                return false;
            }

            if (destination.Type == SorterElementFixPoint.SorterFixPointTypes.Chute)
                SetStopDistance(carrier, destination.GlobalDistance);

            float time = TimeToArrival(carrier, destination) - leadtime;

            if (Running)
            {
                if (time > Minimumtimerresolution)
                {
                    load.WaitingTime = time;

                    load.OnFinishedWaitingEvent -= LoadArrivedAtDestination;
                    load.OnFinishedWaitingEvent += LoadArrivedAtDestination;
                }
                else
                {
                    LoadArrivedAtDestination(load);
                }
            }
            else
            {
                loadCarrierStopped.Add(load, time);
            }
            return true;
        }

        private void SetStopDistance(SorterCarrier carrier, float globaldistance)
        {
            if (TiltTrays)
            {
                carrier.StopTiltingDistance = globaldistance + TiltTrayDistance;
            }
            else
            {
                carrier.StopTiltingDistance = 0;
            }

            carrier.CurrentTiltAngle = 0;
        }

        #endregion

        #region Logic

        private float DistanceToGlobalDistance(float globalDistance, SorterCarrier carrier)
        {
            float distance = (globalDistance - (float)carrier.CurrentDistance / 1000);

            if (distance < Minimumtimerresolution)
                distance = masterElement.TotalSorterLength - (float)carrier.CurrentDistance / 1000 + globalDistance;

            return distance;
        }

        private float DistanceToFixpoint(SorterElementFixPoint f, SorterCarrier carrier)
        {
            return DistanceToGlobalDistance(f.GlobalDistance, carrier);
        }

        private void UpdateLoadPositionOnCarrier(SorterCarrier carrier)
        {
            if (!SorterMaster)
                return;

            if (!Initialized)
                return;

            if (carrier.CurrentLoad != null)
            {
                //Load position
                carrier.CurrentLoad.Position = Track[carrier.CurrentDistance].Position +
                                            new Vector3(0,
                                                carrier.CurrentLoad.Height / 2 + LoadOffset + carrier.Master.CarrierHeight / 2, 0) -
                                            Track[carrier.CurrentDistance].Direction * carrier.CurrentLoadOffset;

                //Load orientation
                if (TiltTrays && carrier.CurrentTiltAngle != 0)
                {
                    //Tilting tray
                    if (carrier.CurrentLoadYaw != 0)
                        carrier.CurrentLoad.Orientation = Matrix.RotationY(carrier.CurrentLoadYaw) *
                                                       Matrix.RotationX(carrier.CurrentTiltAngle) *
                                                       Track[carrier.CurrentDistance].Orientation;
                    else
                        carrier.CurrentLoad.Orientation = Matrix.RotationX(carrier.CurrentTiltAngle) *
                                                       Track[carrier.CurrentDistance].Orientation;
                }
                else
                {
                    //Normal tray
                    if (carrier.CurrentLoadYaw != 0)
                        carrier.CurrentLoad.Orientation = Matrix.RotationY(carrier.CurrentLoadYaw) *
                                                       Track[carrier.CurrentDistance].Orientation;
                    else
                        carrier.CurrentLoad.Orientation = Track[carrier.CurrentDistance].Orientation;
                }
            }
        }

        private void LoadArrivedAtDestination(Load load)
        {
            load.OnFinishedWaitingEvent -= LoadArrivedAtDestination;

            if (!initialized)
                return;

            if (!SorterMaster)
                return;

            SorterCarrier carrier = loadCarrier[load];
            SorterElementFixPoint destination = carrier.CurrentDestination;

            bool ready = (destination.Fixpoint.Attached != null && destination.Fixpoint.Attached.Parent is IChute)
                ? (destination.Fixpoint.Attached.Parent as IChute).Ready && carrier.CurrentDestination.Enabled
                : carrier.CurrentDestination.Enabled && destination.Fixpoint.Attached.Parent is IChute;

            if (ready)
                Tilt(load, destination);

            Control.OnLoadDestinationArrivedEvent(carrier, destination, load, ready);
        }

        private void Tilt(Load load, SorterElementFixPoint destination)
        {
            if (SorterMaster)
                UpdateCarriers();
            else
                masterElement.UpdateCarriers();

            RemoveLoadFromSorter(load);

            //Switch to destination action point
            if (destination.ChutePoint != null)
            {
                load.Switch(destination.ChutePoint, true);
            }

        }

        private void RemoveLoadFromSorter(Load load)
        {
            load.Deletable = true;
            load.OnFinishedWaitingEvent -= LoadArrivedAtDestination;
            load.OnDisposing -= RemoveLoadFromSorter;

            if (loadCarrier.ContainsKey(load))
            {
                SorterCarrier carrier = loadCarrier[load];

                loadCarrier.Remove(load);

                carrier.CurrentDestination = null;
                carrier.CurrentLoad = null;
                carrier.CurrentLoadYaw = 0;
            }
        }

        private void CarrierArrivedAtSorterFixPoint(Timer sender)
        {
            if (!initialized)
                return;

            if (SorterMaster)
                UpdateCarriers();
            else
                masterElement.UpdateCarriers();
            SorterElementFixPoint f = (SorterElementFixPoint)(sender.UserData);
            SorterCarrier carrier = f.CarrierArriving;
            f.CarrierArriving = null;
            if (SorterMaster)
                Control.OnCarrierArrivedEvent(carrier, f);
            else
                masterElement.Control.OnCarrierArrivedEvent(carrier, f);
        }

        private double GetCurrentTime()
        {
            if (Environment.Engine.Events)
                return Environment.Time.Simulated;

            return 0;
        }

        internal void UpdateLoads()
        {
            foreach (SorterCarrier carrier in loadCarrier.Values)
            {
                UpdateLoadPositionOnCarrier(carrier);
            }
        }

        internal void UpdateCarriers()
        {
            if (!SorterMaster)
                return;

            if (carriersLastUpdateTime == GetCurrentTime())
                return;

            carriersLastUpdateTime = GetCurrentTime();

            if (!running && carriersLastUpdateTime > 0)
                return;

            if (Environment.Engine.Events)
                masterindex = (int)(((GetCurrentTime() - stoppedtime) * Speed * 1000) % Track.Count);
            else
                masterindex = (int)(((GetCurrentTime()) * Speed * 1000) % Track.Count);


            int index;

            foreach (SorterCarrier carrier in carriers)
            {
                index = (int)(masterindex + carrier.OffsetDistance) % Track.Count;
                carrier.CurrentDistance = index;
                carrier.RenderingDistance = (int)(index + carrier.RenderingOffsetDistance) % Track.Count;

                float w = (index / (CarrierLengthPlusSpacing * 1000f));
                int window = (int)Math.Round(w);
                if (window == carriers.Count)
                    window = 0;

                windowCarrier[window] = carrier;

            }
        }

        #endregion

        #region Snapping

        protected virtual void EndFixPoint_UnSnapped(FixPoint fixpoint)
        {
            next = null;
        }

        protected virtual void EndFixPoint_Snapped(FixPoint fixpoint, FixPoint.SnapEventArgs e)
        {
            if (fixpoint.Type == FixPoint.Types.End)
            {
                e.Cancel = true;
                return;
            }

            if (fixpoint.Parent is SorterElement)
            {
                next = (SorterElement)fixpoint.Parent;
                return;
            }

            e.Cancel = true;
        }

        protected virtual void StartFixPoint_UnSnapped(FixPoint fixpoint)
        {

        }

        protected virtual void StartFixPoint_Snapped(FixPoint fixpoint, FixPoint.SnapEventArgs e)
        {
            if (fixpoint.Type == FixPoint.Types.Start)
            {
                e.Cancel = true;
                return;
            }

            if (fixpoint.Parent is SorterElement)
            {
                return;
            }

            e.Cancel = true;
        }

        #endregion

        #region Configure and Orientation and abstract methods

        private void SetMasterGraphics()
        {
            if (SorterMaster)
            {
                if (masterCube == null)
                {
                    masterCube = new Cube(Color.Green, 0.2f, 0.2f, 0.2f);
                    Add(masterCube);
                    masterCube.LocalPosition = new Vector3(0, -0.2f, 0);
                }
            }
            else
            {
                if (masterCube != null)
                {
                    Remove(masterCube);
                    masterCube.Dispose();
                    masterCube = null;
                }
            }
        }

        protected abstract void ConfigureFixPoint(SorterElementFixPoint f);
        internal abstract Matrix OrientationElement { get; }

        private void InitializeFixPoint(SorterElementFixPoint f)
        {
            f.Parent = this;
            Add(f.Fixpoint);
            f.Distance = f.Distance;
        }

        internal void AddCarrierMeshes()
        {
            foreach (var carrier in carriers)
            {
                Add(carrier.CarrierMesh);
            }

            UpdateCarriersVisibility();
        }

        internal void RemoveCarrierMeshes()
        {
            foreach (var carrier in carriers)
            {
                RemovePart(carrier.CarrierMesh);
            }
        }

        private void UpdateCarriersVisibility()
        {
            foreach (var carrier in carriers)
            {
                carrier.CarrierMesh.Visible = sorterElementInfo.VisibleCarriers;
            }
        }

        internal bool UpdateSorterFixPointDistance(SorterElementFixPoint f, float distance)
        {
            if (distance < 0 || distance > Length)
                return false;

            ConfigureFixPoint(f);

            return true;
        }

        internal void UpdateFixPoints(int value)
        {
            foreach (SorterElementFixPoint f in sorterElementInfo.FixPoints)
            {
                if (Selected)
                    Environment.Properties.Remove(f);
                f.Dispose();
            }

            sorterElementInfo.FixPoints.Clear();

            for (int i = 0; i < value; i++)
            {
                SorterElementFixPoint f = new SorterElementFixPoint();
                InitializeFixPoint(f);
                f.Distance = Length / value * i + Length / ((float)value * 2);
                sorterElementInfo.FixPoints.Add(f);
            }

            Environment.Properties.Refresh();
        }

        internal bool RemoveFixPoint(FixPoint f)
        {
            return RemovePart(f);
        }

        [Category("Configuration")]
        [DisplayName(@"Fix Points")]
        [TypeConverter(typeof(ExpandableObjectConverter))]
        [PropertyOrder(10)]
        [DefaultFilter("Configuration")]
        public ExpandablePropertyList<SorterElementFixPoint> SorterFixPointList
        {
            get { return sorterElementInfo.FixPoints; }
        }

        #endregion

        #region Overriding Render, Dispose, Category, Image, Reset, Inserted, DoubleClick from Assembly

        /// <summary>
        /// Update the transformation matrix and 
        /// </summary>
        private void UpdateCarriersTransformation()
        {
            if (!VisibleCarriers)
                return;

            foreach (SorterCarrier carrier in carriers)
            {
                if (TiltTrays)
                {
                    if (carrier.CurrentTiltAngle != 0 && carrier.CurrentDestination == null)
                    {
                        float distancetodest = DistanceToGlobalDistance(carrier.StopTiltingDistance, carrier);

                        if (distancetodest < TiltTrayDistance)
                        {
                            carrier.CurrentTiltAngle = Math.Sign(carrier.CurrentTiltAngle) *
                                                    (1 - (TiltTrayDistance - distancetodest) / TiltTrayDistance) *
                                                    sorterElementInfo.TiltTrayAngle;
                        }
                        else
                            carrier.CurrentTiltAngle = 0;
                    }

                    else if (carrier.CurrentDestination != null)
                    {
                        float distancetodest = DistanceToFixpoint(carrier.CurrentDestination, carrier);

                        if (distancetodest < TiltTrayDistance)
                        {
                            bool ready = (carrier.CurrentDestination.Fixpoint.Attached != null &&
                                          carrier.CurrentDestination.Fixpoint.Attached.Parent is IChute)
                                ? (carrier.CurrentDestination.Fixpoint.Attached.Parent as IChute).Ready && carrier.CurrentDestination.Enabled
                                : carrier.CurrentDestination.Enabled;

                            if (ready)
                            {
                                //Start tilt animation
                                carrier.CurrentTiltAngle = (TiltTrayDistance - distancetodest) / TiltTrayDistance * sorterElementInfo.TiltTrayAngle;
                                if (carrier.CurrentDestination.DischargeSide == SorterElementFixPoint.DischargeSides.Right)
                                    carrier.CurrentTiltAngle *= -1;
                            }
                        }
                    }

                    if (carrier.CurrentTiltAngle != 0)
                        carrier.CarrierMesh.Transformation = carrierScaling * Matrix.RotationX(carrier.CurrentTiltAngle) *
                                                       Track[carrier.RenderingDistance].Orientation *
                                                       Matrix.Translation(Track[carrier.RenderingDistance].Position);
                    else
                        carrier.CarrierMesh.Transformation = Track[carrier.RenderingDistance].Transformation;
                }
                else
                    carrier.CarrierMesh.Transformation = Track[carrier.RenderingDistance].Transformation;

            }
        }

        /// <summary>
        /// Update carriers and loads in event mode before rendering
        /// </summary>
        private void UpdateCarriersAndLoadsBeforeRendering()
        {
            if (Track.Count > 0)
            {
                UpdateCarriers();

                UpdateLoads();

                if (Environment.Scene.PresentationLevel == Environment.Scene.PresentationLevels.Primitives ||
                    Environment.Scene.PresentationLevel == Environment.Scene.PresentationLevels.Loads)
                    return;

                UpdateCarriersTransformation();
            }
        }

        public override void Render()
        {
            if (Environment.Scene.PresentationLevel == Environment.Scene.PresentationLevels.Primitives ||
                Environment.Scene.PresentationLevel == Environment.Scene.PresentationLevels.Loads)
                return;

            if (SorterMaster)
                UpdateCarriersAndLoadsBeforeRendering();

            //base renders carriers
            base.Render();

            if (Environment.Debug.Level != Environment.Debug.Levels.Disabled)
            {
                foreach (SorterElementFixPoint f in SorterFixPointList)
                {
                    Environment.Scene.Text.Projected(f.Name, f.Position, false, false, false,
                        Environment.Scene.FontSize.Medium);
                }
            }

            if (SorterMaster)
            {
                if ((Environment.Engine.Events && Environment.Scene.ShowLabels == Environment.Scene.ViewLabels.Route))
                {
                    foreach (SorterCarrier carrier in carriers)
                    {
                        if (Environment.Engine.Events &&
                            Environment.Scene.ShowLabels == Environment.Scene.ViewLabels.Route)
                        {
                            if (!string.IsNullOrWhiteSpace(carrier.Name))
                                Environment.Scene.Text.Projected(carrier.Name,
                                    Track[carrier.RenderingDistance].Position, false, true, false);
                            else
                                Environment.Scene.Text.Projected(carrier.Index.ToString(),
                                    Track[carrier.RenderingDistance].Position, false, true, false);
                        }
                    }
                }
            }

        }

        public override void Dispose()
        {
            elements.Clear();
            masterFixPoints.Clear();
            Track.Clear();
            Track = null;
            windowCarrier.Clear();
            loadCarrier.Clear();

            foreach (SorterCarrier carrier in carriers)
            {
                carrier.CurrentDestination = null;
                carrier.CurrentLoad = null;
                carrier.Master = null;
                carrier.Next = null;
                carrier.Previous = null;
                carrier.ReservationKey = null;
                carrier.UserData = null;
            }

            carriers.Clear();

            foreach (var c in SorterFixPointList)
            {
                c.Timer.OnElapsed -= CarrierArrivedAtSorterFixPoint;
                c.Dispose();
            }

            startFixPoint.OnSnapped -= StartFixPoint_Snapped;
            startFixPoint.OnUnSnapped -= StartFixPoint_UnSnapped;

            endFixPoint.OnSnapped -= EndFixPoint_Snapped;
            endFixPoint.OnUnSnapped -= EndFixPoint_UnSnapped;

            SorterElements.Remove(this);

            base.Dispose();
        }

        public override string Category
        {
            get { return "Sorter Element"; }
        }

        public override Image Image
        {
            get { return Common.Icons.Get(Category); }
        }

        public override void Reset()
        {
            stoppedtime = 0;
            stoptime = 0;

            foreach (var carrier in carriers)
            {
                carrier.CurrentDestination = null;
                carrier.CurrentLoad = null;
                carrier.ReservationKey = null;
                carrier.UserData = null;
                carrier.CurrentTiltAngle = 0;
                carrier.CurrentLoadOffset = 0;
                carrier.CurrentLoadYaw = 0;
                carrier.CarrierMesh.Color = Color.Empty;
            }
            foreach (var f in SorterFixPointList)
            {
                f.Reset();
            }

            foreach (var load in loadCarrier.Keys)
            {
                load.Deletable = true;
                load.OnFinishedWaitingEvent -= LoadArrivedAtDestination;
            }

            loadCarrier.Clear();

            carriersLastUpdateTime = -1;
            UpdateCarriers();
            UpdateLoads();
            base.Reset();
        }

        public override void Inserted()
        {
            Position = Info.position;
            Yaw = Info.yaw;
        }

        #endregion

        #region Public Get and Set

        [Browsable(false)]
        public virtual SorterElement MasterElement
        {
            get { return masterElement; }
            internal set
            {
                masterElement = value;

                if (OnSettingMasterElement != null)
                    OnSettingMasterElement(this);
            }
        }

        [PropertyOrder(0)]
        [Category("Settings")]
        [Description("If true then this element is recognized as part of a sorter")]
        [DisplayName(@"Initialized")]
        [PropertyAttributesProvider("DynamicPropertyMasterElementInitialize")]
        [DefaultFilter("Configuration")]
        public virtual bool Initialized
        {
            get { return initialized; }
            set
            {
                if (value != initialized)
                {
                    if (Environment.InvokeRequired)
                    {
                        Environment.Invoke(() => Initialized = value);
                        return;
                    }

                    if (!initialized && SorterMaster)
                    {
                        if (!VerifyLoop(this))
                        {
                            Environment.Log.Write(Name + " could not be initialized.", Color.Red,
                                Environment.Log.Filter.Error);
                            return;
                        }
                    }

                    initialized = value;
                    carriersLastUpdateTime = -1;

                    if (initialized && SorterMaster)
                    {
                        BuildSorter(this);
                    }


                    if (!value && SorterMaster)
                    {
                        BreakSorter(this);
                    }

                    if (initialized)
                    {
                        foreach (SorterElementFixPoint c in SorterFixPointList)
                        {
                            c.Timer.OnElapsed += CarrierArrivedAtSorterFixPoint;
                        }
                    }
                    else
                    {
                        foreach (SorterElementFixPoint c in SorterFixPointList)
                        {
                            c.Timer.OnElapsed -= CarrierArrivedAtSorterFixPoint;
                        }
                    }

                    Environment.Properties.Refresh();

                    if (value)
                    {
                        Reset();

                        foreach (SorterElement element in elements)
                        {
                            element.Reset();
                        }

                        if (OnInitialized != null)
                        {
                            OnInitialized(this);
                        }
                    }
                    if (SorterMaster && initialized && Running)
                    {
                        //Sorter is started as default (running == true)
                        Control.OnStartedEvent();
                    }
                }
            }
        }

        [PropertyOrder(1)]
        [Category("Settings")]
        [DisplayName(@"Load Position")]
        [PropertyAttributesProvider("DynamicPropertyMasterElement")]
        [DefaultFilter("Configuration")]
        public virtual ActionPoint.Edges LoadCarrierPosition
        {
            get { return sorterElementInfo.LoadCarrierPosition; }
            set { sorterElementInfo.LoadCarrierPosition = value; }
        }

        [PropertyOrder(1)]
        [Category("Settings")]
        [Description("If true then this element is the master element of a sorter. Note: Only one master element is allowed per sorter")]
        [DisplayName(@"Sorter master")]
        [PropertyAttributesProvider("DynamicPropertyInitialized")]
        [DefaultFilter("Configuration")]
        public virtual bool SorterMaster
        {
            get { return sorterElementInfo.SorterMasterElement; }
            set
            {
                sorterElementInfo.SorterMasterElement = value;
                SetMasterGraphics();
                Environment.Properties.Refresh();
            }
        }

        [PropertyOrder(2)]
        [Category("Settings")]
        [Description("Speed in m/s")]
        [DisplayName(@"Speed")]
        [PropertyAttributesProvider("DynamicPropertyMasterElement")]
        [DefaultFilter("Configuration")]
        [TypeConverter(typeof(SpeedConverter))]
        public virtual float Speed
        {
            get { return sorterElementInfo.Sorterspeed; }
            set
            {
                if (value > 0)
                    sorterElementInfo.Sorterspeed = value;
            }
        }

        [PropertyOrder(3)]
        [Category("Size")]
        [Description("Length in meter")]
        [DisplayName(@"Length")]
        [PropertyAttributesProvider("DynamicPropertyInitialized")]
        [TypeConverter(typeof(FloatConverter))]
        public virtual float Length
        {
            get { return sorterElementInfo.SorterElementLength; }
            set
            {
                if (value > 0)
                    sorterElementInfo.SorterElementLength = value;
            }
        }

        [PropertyOrder(4)]
        [Category("Size")]
        [Description("Width in meter")]
        [DisplayName(@"Width")]
        [PropertyAttributesProvider("DynamicPropertyInitialized")]
        [TypeConverter(typeof(FloatConverter))]
        public virtual float SorterWidth
        {
            get { return sorterElementInfo.SorterWidth; }
            set
            {
                if (value > 0)
                {
                    sorterElementInfo.SorterWidth = value;
                    Refresh();
                }
            }
        }

        [PropertyOrder(5)]
        [Category("Settings")]
        [Description("Total sorter length in meter")]
        [DisplayName(@"Total sorter length")]
        [PropertyAttributesProvider("DynamicPropertyMasterElement")]
        [DefaultFilter("Configuration")]
        public virtual float TotalSorterLength { get; internal set; }

        [PropertyOrder(6)]
        [Category("Carriers")]
        [Description("Number of carriers on sorter")]
        [DisplayName(@"Number of Carriers")]
        [PropertyAttributesProvider("DynamicPropertyMasterElement")]
        public virtual int NumberOfCarriers
        {
            get { return sorterElementInfo.NumberOfCarriers; }
            set
            {
                if (value > 0)
                {
                    sorterElementInfo.NumberOfCarriers = value;
                    sorterElementInfo.UseNumberOfCarriers = true;
                }

                if (value <= 0)
                    sorterElementInfo.UseNumberOfCarriers = false;
            }
        }

        [PropertyOrder(7)]
        [Category("Carriers")]
        [Description("Carrier length in milimeter")]
        [DisplayName(@"Carrier Length")]
        [PropertyAttributesProvider("DynamicPropertyMasterElement")]
        [TypeConverter(typeof(FloatConverter))]
        public virtual float CarrierLength
        {
            get { return sorterElementInfo.CarrierLength; }
            set
            {
                if (value > 0)
                {
                    sorterElementInfo.CarrierLength = value;
                }
            }
        }

        [PropertyOrder(8)]
        [Category("Carriers")]
        [Description("Carrier Spacing")]
        [DisplayName(@"Carrier Spacing")]
        [PropertyAttributesProvider("DynamicPropertyMasterElement")]
        [TypeConverter(typeof(FloatConverter))]
        public virtual float CarrierSpacing
        {
            get { return sorterElementInfo.CarrierSpacing; }
            set
            {
                if (initialized)
                    return;
                sorterElementInfo.CarrierSpacing = value;
                Environment.Properties.Refresh();
            }
        }

        [PropertyOrder(9)]
        [Category("Carriers")]
        [Description("Carrier length with spacing in milimeter")]
        [DisplayName(@"Carrier Length With Spacing")]
        [PropertyAttributesProvider("DynamicPropertyMasterElement")]
        [TypeConverter(typeof(FloatConverter))]
        public virtual float CarrierLengthPlusSpacing
        {
            get { return sorterElementInfo.CarrierLength + sorterElementInfo.CarrierSpacing; }
        }

        [PropertyOrder(10)]
        [Category("Carriers")]
        [Description("Carrier width")]
        [DisplayName(@"Carrier Width")]
        [PropertyAttributesProvider("DynamicPropertyMasterElement")]
        [TypeConverter(typeof(FloatConverter))]
        public virtual float CarrierWidth
        {
            get { return sorterElementInfo.Carrierwidth; }
            set
            {
                if (initialized)
                    return;
                if (value > 0)
                {
                    sorterElementInfo.Carrierwidth = value;
                }
            }
        }

        [PropertyOrder(11)]
        [Category("Carriers")]
        [Description("Carrier height")]
        [DisplayName(@"Carrier Height")]
        [PropertyAttributesProvider("DynamicPropertyMasterElement")]
        [TypeConverter(typeof(FloatConverter))]
        public virtual float CarrierHeight
        {
            get { return sorterElementInfo.CarrierHeight; }
            set
            {
                if (initialized)
                    return;
                if (value > 0)
                {
                    sorterElementInfo.CarrierHeight = value;
                }
            }
        }

        [PropertyOrder(12)]
        [Category("Carriers")]
        [Description("Carrier height offset")]
        [DisplayName(@"Carrier Offset (Height)")]
        [PropertyAttributesProvider("DynamicPropertyMasterElement")]
        [TypeConverter(typeof(FloatConverter))]
        public virtual float CarrierHeightOffset
        {
            get { return sorterElementInfo.CarrierHeightOffset; }
            set
            {
                if (initialized)
                    return;

                sorterElementInfo.CarrierHeightOffset = value;

            }
        }

        [PropertyOrder(13)]
        [Category("Carriers")]
        [Description("Load offset on the carrier")]
        [DisplayName(@"Load Offset (Height)")]
        [PropertyAttributesProvider("DynamicPropertyMasterElement")]
        [TypeConverter(typeof(FloatConverter))]
        public virtual float LoadOffset
        {
            get { return sorterElementInfo.LoadOffsetOnCarrier; }
            set
            {
                if (initialized)
                    return;

                sorterElementInfo.LoadOffsetOnCarrier = value;
            }
        }

        [PropertyOrder(14)]
        [Category("Carriers")]
        [Description("Carrier type")]
        [DisplayName(@"Carrier type")]
        [PropertyAttributesProvider("DynamicPropertyMasterElement")]
        public virtual SorterTypes SorterType
        {
            get { return sorterElementInfo.SorterType; }
            set
            {
                if (initialized)
                    return;

                if (sorterElementInfo.SorterType == value)
                    return;

                sorterElementInfo.SorterType = value;

                foreach (var carrier in carriers)
                {
                    if (carrier.CarrierMesh != null)
                        carrier.CarrierMesh.Dispose();

                    if (SorterType == SorterTypes.TiltTray)
                        carrier.CarrierMesh = new Mesh(Common.Meshes.Get("SorterTray"));
                    else if (SorterType == SorterTypes.CrossBelt)
                        carrier.CarrierMesh = new Mesh(Common.Meshes.Get("Crossbelt"));
                    else
                        carrier.CarrierMesh = new Mesh(Common.Meshes.Get("cube"));
                }

                if (value != SorterTypes.TiltTray)
                    TiltTrays = false;

                Environment.Properties.Refresh();
            }
        }

        [PropertyOrder(17)]
        [Category("Carriers")]
        [Description("If true the trays will be tilted at the destination (Animation)")]
        [DisplayName(@"Tilt trays")]
        [PropertyAttributesProvider("DynamicPropertyTilt")]
        public virtual bool TiltTrays
        {
            get { return sorterElementInfo.TiltTrays; }
            set
            {
                if (initialized)
                    return;

                sorterElementInfo.TiltTrays = value;
            }
        }

        [PropertyOrder(18)]
        [Category("Carriers")]
        [Description("Set the degrees the trays should be tilted")]
        [DisplayName(@"Tilting Angle")]
        [PropertyAttributesProvider("DynamicPropertyTilt")]
        [TypeConverter(typeof(Rad2AngleConverter))]
        public virtual float TiltTrayAngle
        {
            get { return sorterElementInfo.TiltTrayAngle; }
            set
            {
                if (initialized)
                    return;

                sorterElementInfo.TiltTrayAngle = value;
            }
        }

        [PropertyOrder(19)]
        [Category("Carriers")]
        [Description("Set the distance the trays should use to tilt or is active")]
        [DisplayName(@"Tilting / Active Distance")]
        [PropertyAttributesProvider("DynamicPropertyTilt")]
        [TypeConverter(typeof(FloatConverter))]
        public virtual float TiltTrayDistance
        {
            get { return sorterElementInfo.TiltTrayDistance; }
            set
            {
                if (initialized)
                    return;

                sorterElementInfo.TiltTrayDistance = value;
            }
        }

        [PropertyOrder(20)]
        [Category("Carriers")]
        [Description("Set visibility of carriers")]
        [DisplayName(@"Visible carriers")]
        [PropertyAttributesProvider("DynamicPropertyMasterElement")]
        public bool VisibleCarriers
        {
            get { return sorterElementInfo.VisibleCarriers; }
            set
            {
                sorterElementInfo.VisibleCarriers = value;
                Environment.InvokeIfRequired(UpdateCarriersVisibility);
            }
        }

        [PropertyOrder(21)]
        [Category("Fixpoints")]
        [Description("Startfixpoint local roll degree. Change this to snap in a different direction.")]
        [DisplayName(@"Start (Local Roll)")]
        [PropertyAttributesProvider("DynamicPropertyInitialized")]
        [DefaultFilter("Configuration")]
        [TypeConverter(typeof(Rad2AngleConverter))]
        public virtual float StartFixLocalRoll
        {
            get { return startFixPoint.LocalRoll; }
            set
            {
                startFixPoint.LocalRoll = value;
                sorterElementInfo.StartFixLocalRoll = Trigonometry.Rad2Angle(value);
            }
        }

        [PropertyOrder(22)]
        [Category("Fixpoints")]
        [Description("Endfixpoint local roll degree. Change this to snap in a different direction.")]
        [DisplayName(@"End (Local Roll)")]
        [PropertyAttributesProvider("DynamicPropertyInitialized")]
        [DefaultFilter("Configuration")]
        [TypeConverter(typeof(Rad2AngleConverter))]
        public virtual float EndFixLocalRoll
        {
            get { return endFixPoint.LocalRoll; }
            set
            {
                endFixPoint.LocalRoll = value;
                sorterElementInfo.EndFixLocalRoll = Trigonometry.Rad2Angle(value);
            }
        }

        [PropertyOrder(23)]
        [Category("Fixpoints")]
        [Description("Number of fixpoints on this element")]
        [DisplayName(@"Number of fixpoints")]
        [PropertyAttributesProvider("DynamicPropertyInitialized")]
        [DefaultFilter("Configuration")]
        public virtual int NumberOfFixPoints
        {
            get { return sorterElementInfo.FixPoints.Count; }

            set
            {
                if (value == sorterElementInfo.FixPoints.Count)
                    return;

                if (value < 0)
                {
                    Environment.Log.Write("This value must be greater than 0");
                    return;
                }

                Environment.Invoke(() => UpdateFixPoints(value));
            }
        }


        [PropertyOrder(24)]
        [Category("Fixpoints")]
        [Description("Set the snap angle for all fixpoints on this element")]
        [DisplayName(@"Angle")]
        [PropertyAttributesProvider("DynamicPropertyInitialized")]
        [DefaultFilter("Configuration")]
        [TypeConverter(typeof(AngleConverter))]
        public virtual float LocalYawAllFixpoints
        {
            get
            {
                float value = 0;
                if (SorterFixPointList.Count > 0)
                {
                    value = SorterFixPointList[0].LocalYaw;
                    foreach (SorterElementFixPoint f in SorterFixPointList)
                    {
                        if (f.LocalYaw != value)
                        {
                            value = 0;
                            break;
                        }
                    }
                }

                return value;
            }
            set
            {
                foreach (SorterElementFixPoint f in SorterFixPointList)
                {
                    f.LocalYaw = value;
                    ConfigureFixPoint(f);
                }
            }
        }

        [PropertyOrder(25)]
        [Category("Fixpoints")]
        [Description("Set the local offset for all fixpoints on this element")]
        [TypeConverter(typeof(Vector3Converter))]
        [DisplayName(@"Local offset")]
        [PropertyAttributesProvider("DynamicPropertyInitialized")]
        [DefaultFilter("Configuration")]
        public virtual Vector3 LocalOffset
        {
            get
            {
                Vector3 value = Vector3.Empty;
                if (SorterFixPointList.Count > 0)
                {
                    value = SorterFixPointList[0].LocalOffset;
                    foreach (SorterElementFixPoint f in SorterFixPointList)
                    {
                        if (f.LocalOffset != value)
                        {
                            value = Vector3.Empty;
                            break;
                        }
                    }
                }

                return value;
            }
            set
            {
                foreach (SorterElementFixPoint f in SorterFixPointList)
                {
                    f.LocalOffset = value;
                    ConfigureFixPoint(f);
                }
            }
        }

        [Category("Sorter status")]
        [DisplayName(@"Running")]
        public bool Running
        {
            get
            {
                if (!initialized)
                    return false;

                if (SorterMaster)
                    return running;

                if (masterElement != null)
                    return masterElement.Running;

                return false;
            }

        }

        [Browsable(false)]
        public override bool Enabled
        {
            get { return base.Enabled; }
            set { base.Enabled = value; }
        }

        [Browsable(false)]
        public FixPoint EndFixPoint
        {
            get { return endFixPoint; }
        }

        [Browsable(false)]
        public FixPoint StartFixPoint
        {
            get { return startFixPoint; }
        }

        public void DynamicPropertyMasterElement(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = SorterMaster;
            attributes.IsReadOnly = Initialized;
        }

        public void DynamicPropertyMasterElementInitialize(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = SorterMaster;
        }

        public void DynamicPropertyInitialized(PropertyAttributes attributes)
        {
            attributes.IsReadOnly = Initialized;
        }

        public void DynamicPropertyTilt(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = SorterType == SorterTypes.TiltTray && SorterMaster;
            attributes.IsReadOnly = Initialized;
        }

        #endregion

        #region Building and breaking the sorter
        private static void BreakSorter(SorterElement master)
        {
            Environment.UI.Locked = true;

            master.RemoveCarrierMeshes();

            foreach (SorterElement unit in master.elements)
            {
                unit.Initialized = false;
                unit.MasterElement = null;

                foreach (SorterCarrier carrier in unit.carriers)
                {
                    carrier.CurrentDestination = null;
                    carrier.Master = null;
                    carrier.CurrentLoad = null;
                    carrier.Next = null;
                    carrier.Previous = null;
                    carrier.CurrentLoad = null;
                }

                unit.carriers.Clear();
                unit.Track.Clear();
                unit.windowCarrier.Clear();
                unit.loadCarrier.Clear();
                if (!((SorterElementInfo)(unit.Info)).UseNumberOfCarriers)
                    unit.NumberOfCarriers = 0;
                unit.masterFixPointNames.Clear();
                unit.masterFixPoints.Clear();
            }

            master.elements.Clear();

            Environment.UI.Locked = false;
        }

        private static bool VerifyLoop(SorterElement master)
        {
            bool loopdetected = false;
            SorterElement element = master;

            for (int i = 0; i <= SorterElements.Count; i++)
            {
                if (element.next == null)
                    return false;

                element = element.next;

                if (element != master && element.SorterMaster)
                {
                    Environment.Log.Write("Experior found 2 master elements in the same sorter loop.", Color.Red, Environment.Log.Filter.Error);
                    return false;
                }
                if (element == master)
                {
                    loopdetected = true;
                    break;
                }
            }

            return loopdetected;
        }

        private static bool VerifyLoops(List<SorterElement> masterElements)
        {
            bool verify = true;

            foreach (SorterElement master in masterElements)
            {
                if (!VerifyLoop(master))
                {
                    verify = false;
                    break;
                }
            }

            return verify;
        }

        public static void InitializeMasterSorters()
        {
            if (SorterElements.Count == 0)
                return;

            Environment.UI.Locked = true;

            var masterElements = SorterElements.FindAll(e => e.SorterMaster).ToList();

            if (masterElements.Count == 0 && SorterElements.Count > 0)
            {
                Environment.UI.Locked = false;
                Environment.Log.Write("Experior did not find any sorter master elements. No sorters has been initialized.", Color.Red, Environment.Log.Filter.Error);
                return;
            }

            if (!VerifyLoops(masterElements))
            {
                Environment.UI.Locked = false;
                Environment.Log.Write("Experior found " + masterElements.Count + " master elements, but could not detect " + masterElements.Count + " sorters. No sorters has been initialized.", Color.Red, Environment.Log.Filter.Error);
                return;
            }

            foreach (SorterElement master in masterElements)
            {
                master.Initialized = true;
            }

            Environment.UI.Locked = false;
        }

        private static void BuildSorter(SorterElement master)
        {
            if (Environment.InvokeRequired)
            {
                Environment.Invoke(() => BuildSorter(master));
                return;
            }

            Environment.UI.Locked = true;

            float sorterlength = 0;
            master.elements.Clear();
            master.masterFixPoints.Clear();
            master.masterFixPointNames.Clear();
            SorterElement element = master;
            bool finishbuild = false;

            //Find all elements for this sorter master element
            while (!finishbuild)
            {
                if (!master.elements.Contains(element))
                {
                    foreach (SorterElementFixPoint c in element.SorterFixPointList)
                        c.GlobalDistance = c.Distance + sorterlength;

                    master.masterFixPoints.AddRange(element.SorterFixPointList);
                    master.elements.Add(element);
                    sorterlength += element.Length;
                }

                element = element.next;

                if (element == master)
                    finishbuild = true;
            }

            master.TotalSorterLength = sorterlength;

            foreach (SorterElementFixPoint f in master.masterFixPoints)
            {
                master.masterFixPointNames.Add(f.Name);
            }

            //Initialize carriers
            if (((SorterElementInfo)(master.Info)).UseNumberOfCarriers)
                master.CarrierLength = sorterlength / master.NumberOfCarriers - master.CarrierSpacing;
            else
                ((SorterElementInfo)(master.Info)).NumberOfCarriers =
                    (int)Math.Truncate(sorterlength / master.CarrierLengthPlusSpacing);

            master.carriers.Clear();
            master.windowCarrier.Clear();
            master.carriers = new List<SorterCarrier>(master.NumberOfCarriers);
            for (int i = 0; i < master.NumberOfCarriers; i++)
            {
                var carrier = new SorterCarrier
                {
                    OffsetDistance = (master.NumberOfCarriers - 1 - i) * master.CarrierLengthPlusSpacing * 1000,
                    Master = master
                };

                if (master.SorterType == SorterTypes.TiltTray)
                    carrier.CarrierMesh = new Mesh(Common.Meshes.Get("SorterTray"));
                else if (master.SorterType == SorterTypes.CrossBelt)
                    carrier.CarrierMesh = new Mesh(Common.Meshes.Get("Crossbelt"));
                else
                    carrier.CarrierMesh = new Mesh(Common.Meshes.Get("cube"));

                if (master.SorterType == SorterTypes.Edge)
                    master.carrierScaling = Matrix.Scaling(0.01f / carrier.CarrierMesh.Length,
                        master.CarrierHeight / carrier.CarrierMesh.Height, master.SorterWidth / carrier.CarrierMesh.Width);
                else
                    master.carrierScaling = Matrix.Scaling(master.CarrierLength / carrier.CarrierMesh.Length,
                        master.CarrierHeight / carrier.CarrierMesh.Height, master.CarrierWidth / carrier.CarrierMesh.Width);

                if (master.SorterType == SorterTypes.Edge)
                    carrier.RenderingOffsetDistance = master.CarrierLength / 2f * 1000f;

                if (i > 0)
                {
                    carrier.Next = master.carriers[i - 1];
                    carrier.Next.Previous = carrier;
                }
                carrier.Index = i;
                master.carriers.Add(carrier);
                master.windowCarrier.Add(i, carrier);
            }
            master.carriers[0].Next = master.carriers[master.NumberOfCarriers - 1];
            master.carriers[master.NumberOfCarriers - 1].Previous = master.carriers[0];

            master.AddCarrierMeshes();

            //Build the track
            //Track resolution is 1 mm
            var numberofindices = (int)(sorterlength * 1000);
            master.Track.Clear();
            master.Track = new Dictionary<int, TrackTransformation>(numberofindices);
            int index = 0;

            foreach (SorterElement unit in master.elements)
            {
                unit.MasterElement = master;
                unit.Initialized = true;

                var numberOfIndicesForUnitElement = (int)(unit.Length * 1000);

                if (unit is SorterElementStraight)
                {
                    Vector3 dir = unit.EndFixPoint.Position - unit.StartFixPoint.Position;
                    dir.Normalize();
                    Matrix orientation = Matrix.RotationY((float)Math.PI) * unit.OrientationElement;
                    var direction = new Vector3(1, 0, 0);
                    direction.TransformCoordinate(orientation);

                    for (int i = 0; i < numberOfIndicesForUnitElement; i++)
                    {
                        Vector3 position = unit.StartFixPoint.Position + dir * (i / 1000f) +
                                           new Vector3(0, master.CarrierHeightOffset - master.CarrierHeight / 2, 0);
                        Matrix transformation = master.carrierScaling * orientation * Matrix.Translation(position);
                        master.Track[index] = new TrackTransformation
                        {
                            Position = position,
                            Orientation = orientation,
                            Transformation = transformation,
                            Direction = direction
                        };
                        index++;
                    }
                }
                else
                {
                    Vector3 dir = unit.StartFixPoint.Position - unit.Position;
                    float degreeresolution = (((SorterElementCurve)unit).Degrees / 180 * (float)Math.PI) /
                                             numberOfIndicesForUnitElement;
                    int rev = 1;
                    int turnaround = 0;
                    if (((SorterElementInfo)unit.Info).Revolution == Environment.Revolution.Counterclockwise)
                    {
                        rev = -1;
                        turnaround = 1;
                    }

                    float heightDegree = 0;
                    float heightdifference = 0;
                    if (unit.StartFixPoint.Position.Y != unit.EndFixPoint.Position.Y)
                    {
                        var length = ((SorterElementCurve)unit).Degrees / 180 * ((SorterElementCurve)unit).Radius *
                                     (float)Math.PI;
                        heightdifference = unit.EndFixPoint.Position.Y - unit.StartFixPoint.Position.Y;
                        heightDegree = (float)Math.Atan(heightdifference / length);
                    }

                    for (int i = 0; i < numberOfIndicesForUnitElement; i++)
                    {
                        Vector3 position = unit.Position + dir +
                                           new Vector3(0, master.CarrierHeightOffset - master.CarrierHeight / 2, 0) +
                                           new Vector3(0, heightdifference * i / numberOfIndicesForUnitElement, 0);
                        Matrix orientation = Matrix.RotationY(degreeresolution * i * rev + turnaround * (float)Math.PI) *
                                             unit.OrientationElement;

                        if (((SorterElementCurve)unit).HeightDifference != 0)
                            //Height Difference property used then unit.Orientation does not include incline or decline
                            orientation = Matrix.RotationZ(heightDegree) * orientation;

                        Matrix transformation = master.carrierScaling * orientation * Matrix.Translation(position);
                        Vector3 direction = new Vector3(1, 0, 0);
                        direction.TransformCoordinate(orientation);

                        master.Track[index] = new TrackTransformation
                        {
                            Position = position,
                            Orientation = orientation,
                            Transformation = transformation,
                            Direction = direction
                        };
                        dir.TransformCoordinate(Matrix.RotationY(degreeresolution * rev));
                        index++;
                    }
                }

            }

            master.UpdateCarriers();
            Environment.UI.Locked = false;
            Environment.Log.Write("Sorter with master element '" + master.Name + "' has been initialized.", Environment.Log.Filter.System);

        }

        #endregion

        #region IControllable
        private MHEControl controllerProperties;
        private IController controller;

        [Browsable(false)]
        public IController Controller
        {
            get{ return controller;}
            set
            {
                controller = value;
                if (value != null)
                {   //If the PLC is deleted then any conveyor referencing the PLC will need to remove references to the deleted PLC.
                    controller.OnControllerDeletedEvent += controller_OnControllerDeletedEvent;
                    controller.OnControllerRenamedEvent += controller_OnControllerRenamedEvent;
                }
                else if (controller != null && value == null)
                {
                    controller.OnControllerDeletedEvent -= controller_OnControllerDeletedEvent;
                    controller.OnControllerRenamedEvent -= controller_OnControllerRenamedEvent;
                }

                Core.Environment.Properties.Refresh();
            }
        }

        [Category("Routing")]
        [DisplayName("Control")]
        [Description("Embedded routing control with protocol and routing specific configuration")]
        [PropertyOrder(21)]
        [PropertyAttributesProvider("DynamicPropertyAssemblyPLCconfig")]
        public MHEControl ControllerProperties
        {
            get { return controllerProperties; }
            set
            {
                controllerProperties = value;
                if (value == null)
                {
                    Controller = null;
                }
                Experior.Core.Environment.Properties.Refresh();
            }
        }

        [Category("Routing")]
        [DisplayName("Controller")]
        [Description("Controller name that handles this conveyor")]
        [PropertyAttributesProvider("DynamicPropertyMasterElement")]
        [TypeConverter(typeof(CaseControllerConverter))]
        public string ControllerName
        {
            get
            {
                return sorterElementInfo.ControllerName;
            }
            set
            {
                if (!value.Equals(sorterElementInfo.ControllerName))
                {
                    ControllerProperties = null;
                    sorterElementInfo.ProtocolInfo = null;
                    Controller = null;
                }

                sorterElementInfo.ControllerName = value;
                if (value != null)
                {
                    //Note StandardCase.SetMHEControl will set the ControllerProperties and Controller properties!
                    ControllerProperties = StandardCase.SetMHEControl(sorterElementInfo, this);
                    if (ControllerProperties == null)
                    {
                        sorterElementInfo.ControllerName = "No Controller";
                    }
                }
            }
        }

        public void controller_OnControllerDeletedEvent(object sender, EventArgs e)
        {
            controller.OnControllerDeletedEvent -= controller_OnControllerDeletedEvent;
            ControllerName = "No Controller";
            Controller = null;
            sorterElementInfo.ProtocolInfo = null;
        }

        void controller_OnControllerRenamedEvent(object sender, EventArgs e)
        {
            sorterElementInfo.ControllerName = ((Assembly)sender).Name;
            Core.Environment.Properties.Refresh();
        }

        public void DynamicPropertyAssemblyPLCconfig(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = Controller != null;
        }

        #endregion
    }
}

