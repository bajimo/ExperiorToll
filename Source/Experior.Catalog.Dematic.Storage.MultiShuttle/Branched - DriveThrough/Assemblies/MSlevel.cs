using Experior.Core.Assemblies;
using Experior.Core.Loads;
using Experior.Core.Mathematics;
using Experior.Core.Routes;
using Experior.Core.TransportSections;
using Experior.Dematic;
using Experior.Dematic.Base;
using Microsoft.DirectX;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;

namespace Experior.Catalog.Dematic.Storage.MultiShuttle.Assemblies
{
    /// <summary>
    /// The MultiShuttle Level holds the track Rail, shuttle, and the current task
    /// </summary>
    public class MSlevel : Assembly
    {
        public ObservableCollection<ShuttleTask> ShuttleTasks = new ObservableCollection<ShuttleTask>();
        private ShuttleTask currentTask;
        public TrackRail Track;
        public TrackVehicle Vehicle;
        internal MultiShuttle ParentMS;
        float shuttleAP_Zpos, rackLoc;

        private MSlevelInfo msLevelInfo;
        private const float Tolerance = 0.01f; //1 cm

        public MSlevel(MSlevelInfo info) : base(info)
        {
            msLevelInfo = info;
            ParentMS = info.parentMultishuttle;

            Track = new TrackRail(new TrackRailInfo() { level = info.level, parentMultiShuttle = ParentMS, shuttlecarSpeed = ParentMS.ShuttleCarSpeed, controlAssembly = this });
            Vehicle = new TrackVehicle(new TrackVehicleInfo() { trackRail = Track, moveToDistance = InfeedRackShuttleLocation, controlAssembly = this});
            Add((Core.Parts.RigidPart)Track, new Vector3(0, -0.025f, 0));

            Vehicle.OnLoadArrived          += Vehicle_OnLoadArrived;
            Vehicle.Length                  = ParentMS.ShuttleCarLength;
            Vehicle.Width                   = ParentMS.ShuttleCarWidth;
            shuttleAP_Zpos                  = (info.multiShuttleinfo.RackConveyorWidth / 2) + (info.multiShuttleinfo.carwidth / 2);
            ShuttleTasks.CollectionChanged += ShuttleTasks_CollectionChanged;
            Vehicle.OnVehicleArrived       += ShuttleOnArrived;
            ShuttleTasks.Clear();
        }

        /// <summary>
        /// A Load has just arrived onto the shuttle; the ShuttleAP is the AP on the conveyor attached to the shuttle.
        /// </summary>
        private void Vehicle_OnLoadArrived(object sender, ApEnterEventArgs e)
        {
            e._load.Stop();
            if (!CurrentTask.Source.IsRackBinLocation()) //If there was a load at the infeed rack conveyor behind the load that has just arrived onto shuttle release this load so that it arrived at B
            {
                //ParentMS.ConveyorLocations.Find(x => x.LocName == CurrentTask.Source.Substring(0, CurrentTask.Source.Length - 1) + "A").Release(); //Release a load at A if there is one there
                var ap = ParentMS.ConveyorLocations.Find(x => x.LocName == CurrentTask.Source.Substring(0, CurrentTask.Source.Length - 1) + "A");
                //MRP Xcelgo 01/02/2016. Invoke the ap.Release method. This will put the method call at the end of the current (current time) event list. If a load arrives at the same time at A as the shuttle picks then the two events are simultaneously and we dont know which event will be executed first.
                Core.Environment.Invoke(ap.Release); //Release a load at A if there is one there
            }

            ParentMS.ArrivedAtShuttle(new TaskEventArgs(CurrentTask, e._load));
            MoveShuttle(CurrentTask.DestPosition);
        }

        public override void Reset()
        {
            ShuttleTasks.Clear();
            CurrentTask = null;
            Vehicle.Reset();
            base.Reset();
        }

        public ShuttleTask CurrentTask
        {
            get { return currentTask; }
            set
            {
                if (value != null)
                {
                    currentTask = value;
                    Vehicle.ShuttleTaskDisplay = value.ToString();
                    MoveShuttle(value.SourcePosition);
                }
                else if (value == null)
                {
                    currentTask = null;
                    ShuttleTask newTask = SetNewShuttleTask();
                    if (newTask == null)
                    {
                        Vehicle.ShuttleTaskDisplay = "";
                    }
                }
            }
        }

        /// <summary>
        /// Deals with a new task added to ShuttleTasks, checks for repeated tasks and adds source and dest positions
        /// </summary>
        void ShuttleTasks_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                ShuttleTask stNew = null;
                foreach (ShuttleTask newTask in e.NewItems)
                {
                    newTask.DestPosition   = SetShuttlePosition(newTask.Destination.RackXLocation(), newTask.Destination.ConvType());
                    newTask.SourcePosition = SetShuttlePosition(newTask.Source.RackXLocation(), newTask.Source.ConvType());
                    if(newTask.SourcePosition > ParentMS.Raillength || newTask.DestPosition > ParentMS.Raillength || newTask.SourcePosition < 0 || newTask.DestPosition < 0)
                    {
                        Log.Write(string.Format("Error {0} Level {1}: Cannot reach source or destination location - x Location is greater than rail length or invalid", ParentMS.Name, Level), Color.Red);
                        ShuttleTasks.Remove(newTask);
                        return;
                    }
                    else
                    {
                        stNew = newTask;
                    }
                }

                //Check for repeated tasks This shouldn't happen however the controller/WMS may issue repeated tasks //////
                List<ShuttleTask> listST = ShuttleTasks.ToList();

                if (CurrentTask != null)
                {
                    listST.Add(CurrentTask);
                }

                listST.Remove(stNew);

                foreach (ShuttleTask st in listST)
                {
                    if (st.Equals(stNew)) // Already have this task
                    {
                        ShuttleTasks.Remove(stNew);
                        stNew = null;
                    }
                }

                SetNewShuttleTask();
            }
        }

        public ShuttleTask SetNewShuttleTask()
        {
            ShuttleTask newTask = null;
            if (CurrentTask == null && ShuttleTasks.Count != 0)
            {
                string logMess = "";
                foreach (ShuttleTask st in ShuttleTasks)
                {
                    logMess += string.Format("{0}:", st.LoadID);
                }
                //Log.Write(string.Format("SHUTTLETASKS - {0} - {1}", Name, logMess));

                foreach (ShuttleTask st in ShuttleTasks)
                {
                    if (st.Destination.ConvType() == ConveyorTypes.OutfeedRack)
                    {
                        var v = ParentMS.ConveyorLocations.Find(c => c.LocName == st.Destination);
                        if (!v.Active) //if outfeed only choose the task if the outfeed location is free
                        {
                            newTask = st;
                            break;
                        }
                    }
                    else
                    {
                        newTask = st;
                        break;
                    }
                }

                if (newTask != null)
                {
                    ShuttleTasks.Remove(newTask);
                    CurrentTask = newTask; //recursive call back to move the shuttle.
                }
            }
            return newTask;
        }

        /// <summary>
        /// Calculates the distance along the rail that the shuttle will travel. i.e. the position that the Shuttle.trackRail.MoveTo ActionPoint is set when executing this task
        /// </summary>
        /// <param name="rackXLocation">The number of the location</param>
        /// <returns> The distance along the rail that the shuttle will travel</returns>
        private float SetShuttlePosition(float rackXLocation, ConveyorTypes convType = ConveyorTypes.NA)
        {
            if (rackXLocation > 0) // Position is within the rack (or it is a drive through)
            {
                if (ParentMS.FREX)
                {
                    if (rackXLocation > ParentMS.FrexPositions)//In the normal rack positions
                    {
                        float frontDistance = (ParentMS.FrexPositions * ParentMS.LocationLength) + (ParentMS.RackConveyorLength + ParentMS.ElevatorConveyorLength); //The rail length for the front positions
                        //totalFrontLocs is the number of positions associated with the frontDistance, note that PSDSlocations are only notional positions as they are not avaiable for the shuttle and they would be a different size compared with ParentMultiShuttle.LocationLength.
                        //however the PS/DS/elevator space is still a bay
                        float totalFrontLocs = ParentMS.FrexPositions + ParentMS.PSDSlocations;
                        return ((rackXLocation - totalFrontLocs) * ParentMS.LocationLength) + frontDistance - (ParentMS.LocationLength / 2);
                    }
                    else // in the Front Rack EXtension
                    {
                        return (rackXLocation * ParentMS.LocationLength) - (ParentMS.LocationLength / 2);
                    }
                }
                else
                {
                    if (!ParentMS.MultiShuttleinfo.DriveThrough)
                    {
                        return ((rackXLocation * ParentMS.LocationLength) + ParentMS.RailLengthOffset) - (ParentMS.LocationLength / 2);
                    }
                    else //Drive through DMS
                    {
                        if (ParentMS.driveThroughLocations.Count > 0)
                        {
                            //Calculate and check valid Locations
                            if (ParentMS.driveThroughLocations.Contains((int)rackXLocation))
                            {
                                Log.Write(string.Format("{0} {1}: Cannot send shuttle to xLocation {2}, this location is excluded for drive through. See 'Unused DriveThrough' property", ParentMS.Name, Name, rackXLocation.ToString()), Color.Red);
                                return -1; //Will not move the shuttle
                            }

                            int lowest = ParentMS.driveThroughLocations.Min();
                            int highest = ParentMS.driveThroughLocations.Max();
                            int numLocations = ParentMS.RackLocations - ParentMS.driveThroughLocations.Count;

                            if ((int)rackXLocation < lowest)
                            {
                                if (ParentMS.SwitchEnds) //X Location 1 is at the other end
                                {
                                    return ParentMS.Raillength - (((rackXLocation - 1) * ParentMS.LocationLength) + ParentMS.ShuttleCarLength / 2);
                                }
                                else
                                {
                                    return ((rackXLocation - 1) * ParentMS.LocationLength) + ParentMS.ShuttleCarLength / 2;
                                }
                            }
                            else
                            {
                                float gapLength =ParentMS.ElevatorGap;
                                if (ParentMS.ElevatorGap == 0)
                                {
                                    gapLength = ParentMS.RackConveyorLength * 2 + ParentMS.ElevatorConveyorLength;
                                }

                                if (ParentMS.SwitchEnds) //X Location 1 is at the other end
                                {
                                    return ParentMS.Raillength - ((((rackXLocation - 1 - ParentMS.driveThroughLocations.Count) * ParentMS.LocationLength) + ParentMS.ShuttleCarLength / 2) + gapLength);
                                }
                                else
                                {
                                    return (((rackXLocation - 1 - ParentMS.driveThroughLocations.Count) * ParentMS.LocationLength) + ParentMS.ShuttleCarLength / 2) + gapLength;
                                }
                            }
                        }
                        else
                        {
                            if ((rackXLocation * ParentMS.LocationLength) > ((ParentMS.Raillength / 2) - ParentMS.ElevatorOffset - ((ParentMS.elevators.First().ElevatorConveyor.Length / 2) + ParentMS.RackConveyorLength)))
                            {
                                //Position is before the gap
                                return ((ParentMS.RackConveyorLength * 2 + (ParentMS.ElevatorConveyorLength)) + (rackXLocation * ParentMS.LocationLength)) - (ParentMS.LocationLength / 2);
                            }
                            else
                            {
                                return (rackXLocation * ParentMS.LocationLength) - (ParentMS.LocationLength / 2);
                            }
                        }
                    }
                }
            }
            else //Must be a rack conveyor instead or a drivethrough
            {
                if(ParentMS.MultiShuttleinfo.DriveThrough && convType == ConveyorTypes.OutfeedRack)
                {
                    return OutfeedRackShuttleLocation;
                }
                else
                {
                    return InfeedRackShuttleLocation;
                }
            }
        }

        /// <summary>
        /// Moves the shuttle to the position provided.
        /// </summary>
        internal void MoveShuttle(float position)
        {
            try
            {
                float currentDistance = Vehicle.DestAP.Distance;
                Vehicle.Distance = currentDistance;

                if (position >= Vehicle.Route.Length)
                {
                    //Log.Write(string.Format("Position too high ({0})", position.ToString()), Color.Red);
                    position = Vehicle.Route.Length - Tolerance;
                }
                else if (position <= 0)
                {
                    //Log.Write(string.Format("Position too low ({0})", position.ToString()), Color.Red);
                    position = Tolerance;
                }

                if (Vehicle.DestAP.Distance != position)
                {
                    Vehicle.DestAP.Distance = position;

                    if (Vehicle.DestAP.Distance < currentDistance)
                    {
                        Track.Route.Motor.Backward();
                    }
                    else// if (Vehicle.DestAP.Distance > currentDistance)
                    {
                        Track.Route.Motor.Forward();
                    }
                    Track.Route.Motor.Start();
                    Vehicle.Release();
                }
                else //no need to move as already at the correct location
                {
                    ShuttleOnArrived(null, null);
                }
            }
            catch (Exception ex)
            {
                Log.Write("MoveShuttle Exception", Color.Red);
                Log.Write(ex.ToString(), Color.Red);
            }
        }

        /// <summary>
        /// This is the location (position along the TrackRail) that the shuttle has to arrive at in order to transfer to the infeed rack conveyor.
        /// For front of aisle MS the calculation comes from the shuttle location as it is aligned with the B location of the rack conveyor
        /// </summary>
        public float InfeedRackShuttleLocation
        {
            get
            {
                if (ParentMS.MultiShuttleinfo.DriveThrough)
                {
                    //return ParentMS.Raillength / 2;
                    return ((ParentMS.Raillength / 2) - ParentMS.ElevatorOffset) + ((ParentMS.ElevatorConveyorLength / 2) +(ParentMS.RackConveyorLength - ParentMS.RailLengthOffset));
                }
                else if (ParentMS.FREX)
                {
                    return ((ParentMS.RackConveyorLength + ParentMS.ElevatorConveyorLength) + (ParentMS.LocationLength * ParentMS.FrexPositions)) - ParentMS.RailLengthOffset;
                }
                else
                {
                    return 0;
                }
            }
        }

        /// <summary>
        /// This is the location (position along the TrackRail) that the shuttle has to arrive at in order to transfer to the outfeed rack conveyor.
        /// </summary>
        public float OutfeedRackShuttleLocation
        {
            get
            {
                if (ParentMS.MultiShuttleinfo.DriveThrough)
                {
                    return ((ParentMS.Raillength / 2) - ParentMS.ElevatorOffset) - ((ParentMS.ElevatorConveyorLength / 2) + (ParentMS.RackConveyorLength - ParentMS.RailLengthOffset));
                }
                else if (ParentMS.FREX)
                {
                    return ((ParentMS.RackConveyorLength + ParentMS.ElevatorConveyorLength) + (ParentMS.LocationLength * ParentMS.FrexPositions)) - ParentMS.RailLengthOffset;
                }
                else
                {
                    return 0;
                }
            }
        }

        private void ShuttleOnArrived(object sender, EventArgs e)
        {
            Track.Route.Motor.Stop();
            if (CurrentTask != null)
            {
                if (Vehicle.DestAP.Distance == InfeedRackShuttleLocation) //workarround for experior bug...if the straight transport section is already at the correct location then the load will not be releasable from the action point but if shuttle moves to th position then it can be released...WTF
                {
                    MoveShuttle(InfeedRackShuttleLocation + 0.01f);
                    //MoveShuttle(InfeedRackShuttleLocation);
                    return;
                }                
                else if (HasArrivedAtRackInfeedConv() && Vehicle.LoadsOnBoard == 0)
                {
                    var cl = ParentMS.ConveyorLocations.Find(x => x.LocName == CurrentTask.Source);
                    if (cl != null && cl.Active)
                    {
                        Vector3 v3 = Vehicle.Position - cl.ActiveLoad.Position;
                        v3.Y = 0; //Y is up so do not move the height.
                        cl.ActiveLoad.Translate(() => cl.ActiveLoad.Switch(Vehicle.shuttleAP, true), v3, ParentMS.TimeToPos1);
                    }
                    return;
                }

                if (Vehicle.DestAP.Distance == CurrentTask.SourcePosition && Vehicle.LoadsOnBoard == 0 && !HasArrivedAtRackInfeedConv()) //Retrieving from a bin location
                {
                    Case_Load boxLoad = ParentMS.Controller.CreateCaseLoad(CurrentTask.caseData);
                    Load.Items.Add(boxLoad);
                    boxLoad.Yaw = (float)Math.PI / 2;
                    ParentMS.LoadCreated(new LoadCreatedEventArgs(boxLoad));
                    boxLoad.Switch(Vehicle.shuttleAP2);

                    float timeToTransfer;
                    Vector3 transferVector;
                    CreateTransferVector(boxLoad, out timeToTransfer, out transferVector);

                    boxLoad.Translate(transferVector, 0);                                                                   //Translate away from the suttle AP in zero time
                    Vehicle.shuttleAP2.ActiveLoad.Translate(() => boxLoad.Switch(Vehicle.shuttleAP), transferVector * -1, timeToTransfer);  //Then translate back (hence -1) in in time to transfer
                }
                else if (Vehicle.LoadsOnBoard != 0) //Delevering to a rackbin location or an outfeed point
                {
                    float timeToTransfer;
                    Vector3 transferVector;
                    CreateTransferVector((Case_Load)Vehicle.LoadOnBoard, out timeToTransfer, out transferVector);

                    int dir = 1;
                    if (CurrentTask.Source.Side() != CurrentTask.Destination.Side())
                    {
                        dir = -1;
                    }
                    Load load = Vehicle.LoadOnBoard;
                    //Vehicle.LoadOnBoard.Translate(() => ArrivedAtDest(Vehicle.LoadOnBoard), transferVector * dir, timeToTransfer);

                    if (load != null)
                    {
                        Vehicle.LoadOnBoard.Translate(() => ArrivedAtDest(load), transferVector * dir, timeToTransfer);
                    }
                    else
                    {
                        Log.Write(string.Format("ERROR {0}: ShuttleOnArrived - load is null!", Name), Color.Red);
                        Core.Environment.Scene.Pause();
                    }

                }
            }
        }

        private void CreateTransferVector(Case_Load boxLoad, out float timeToTransfer, out Vector3 transferVector)
        {
            if ((CurrentTask.Source.Side() == RackSide.Right && !ParentMS.SwitchSides) || (CurrentTask.Source.Side() == RackSide.Left && ParentMS.SwitchSides))
            {
                rackLoc = -shuttleAP_Zpos; 
            }
                else
            {
                rackLoc = shuttleAP_Zpos;
            }

            int loadDepth = CurrentTask.Destination.LoadDepth(); //Get the depth in the rack
            timeToTransfer = ParentMS.TimeToPos1;

            if (loadDepth == -1)  //-1 = IA = Inter Aisle Transfer
            {
                timeToTransfer = ParentMS.TimeToPos1 + ParentMS.TimeToPos2;
                rackLoc = rackLoc * 3; //dropoff into the other aisle
            }
            else if (loadDepth == 2)
            {
                rackLoc = rackLoc * 2;
                timeToTransfer = ParentMS.TimeToPos2;
            }

            Vector3 direction = Trigonometry.DirectionYaw(Trigonometry.Yaw(boxLoad.Route.Orientation));
            transferVector = Trigonometry.CrossProduct(direction, new Vector3(0, rackLoc, 0));
        }

        private void ArrivedAtDest(Load load)
        {
            if (CurrentTask == null)
            {
                Log.Write(string.Format("ERROR {0}: Experior.Catalog.Dematic.Storage.MultiShuttle.Assemblies.MSlevel.ArrivedAtDest() Current task is null!", Name), Color.Red);
                return;
            }

            if (CurrentTask.Destination.ConvType() == ConveyorTypes.OutfeedRack)
            {
                var dest = ParentMS.ConveyorLocations.Find(x => x.LocName == CurrentTask.Destination);
                if (dest != null)
                {
                    if (load != null)
                    {
                        load.Switch(dest, true);
                    }
                    else
                    {
                        Log.Write(string.Format("ERROR {0}: Experior.Catalog.Dematic.Storage.MultiShuttle.Assemblies.MSlevel.ArrivedAtDest() load is null!", Name, CurrentTask.Destination), Color.Red);
                        Core.Environment.Scene.Pause();
                    }
                }
                else
                {
                    Log.Write(string.Format("ERROR {0}: could not find destination {1} in Experior.Catalog.Dematic.Storage.MultiShuttle.Assemblies.MSlevel.ArrivedAtDest()", Name ,CurrentTask.Destination), Color.Red);
                }
            }
            else
            {
                ParentMS.ArrivedAtRackLocation(new TaskEventArgs(CurrentTask, load));
                if (load.Route != null)
                {
                    load.Route.Remove(load);
                }

                load.Dispose();

                CurrentTask = null;
            }
        }

        private bool HasArrivedAtRackInfeedConv()
        {
            return Vehicle.DestAP.Distance <= InfeedRackShuttleLocation + 0.02f && Vehicle.DestAP.Distance >= InfeedRackShuttleLocation - 0.02f;
        }

        private bool HasArrivedAtRackOutfeedConv()
        {
            return Vehicle.DestAP.Distance <= OutfeedRackShuttleLocation + 0.02f && Vehicle.DestAP.Distance >= OutfeedRackShuttleLocation - 0.02f;
        }

        public int Level
        {
            get { return msLevelInfo.level; }
            set { msLevelInfo.level = value; }
        }

        public override string Category
        {
            get { return "MultiShuttle"; }
        }

        public override Image Image
        {
            get { return Common.Icons.Get("CreateDematicMultiShuttle"); }
        }
    }

    public class MSlevelInfo : AssemblyInfo
    {
        public MultiShuttleInfo multiShuttleinfo;
        public int level;
        public MultiShuttle parentMultishuttle;

    }
}
