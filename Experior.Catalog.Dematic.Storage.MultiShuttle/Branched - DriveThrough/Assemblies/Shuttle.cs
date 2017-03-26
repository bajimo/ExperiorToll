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
    public class Shuttle : Assembly
    {
        public ObservableCollection<ShuttleTask> ShuttleTasks = new ObservableCollection<ShuttleTask>();
        private ShuttleTask currentTask;
        public TrackRail trackRail;
        public ActionPoint shuttleAP, shuttleAP2;
        internal StraightTransportSection shuttleConveyor;
        internal MultiShuttle ParentMultiShuttle;
        float shuttleAP_Zpos, rackLoc;

        ////The direction that the load is travelling from not the direction that the load is travelling from
        ////Used to turn the shuttle conveyor
        //private float shuttleConvFromRHS = (float)Math.PI / 2 + (float)Math.PI; 
        //private float shuttleConvFromLHS = (float)Math.PI / 2;
        private ShuttleInfo shuttleInfo;

        public Shuttle(ShuttleInfo info): base(info)
        {
            shuttleInfo = info;
            //trackRail = new TrackRail(info.multiShuttleinfo, info.level, info.parent)
            trackRail = new TrackRail(new TrackRailInfo() {level = info.level, parentMultiShuttle = info.parentMultishuttle, shuttlecarSpeed = info.parentMultishuttle.ShuttleCarSpeed})
            {
                Name = "S" + info.level.ToString().PadLeft(2, '0'),
                ThisShuttle = this
            };

            ParentMultiShuttle = info.parentMultishuttle;
            Add((Core.Parts.RigidPart)trackRail);           

            shuttleConveyor = new StraightTransportSection(Core.Environment.Scene.DefaultColor, 0.1f, 0.1f) { Height = 0.05f };
            Add(shuttleConveyor, new Vector3(trackRail.Length / 2, 0, 0));
            //shuttleConveyor.LocalYaw = (float)Math.PI / 2;

            shuttleAP                        = shuttleConveyor.Route.InsertActionPoint(shuttleConveyor.Length / 2);
            shuttleAP.OnEnter               += shuttleAP_OnEnter;
            shuttleAP2                       = shuttleConveyor.Route.InsertActionPoint(shuttleConveyor.Length / 2);
            shuttleAP_Zpos                   = (info.multiShuttleinfo.RackConveyorWidth / 2) + (info.multiShuttleinfo.carwidth / 2);
            trackRail.Car.OnPositionChanged += Car_OnPositionChanged;           
            ShuttleTasks.CollectionChanged  += ShuttleTasks_CollectionChanged;

            ShuttleTasks.Clear();
            shuttleConveyor.Route.Motor.Stop();
        }

        public override void Reset()
        {
            ShuttleTasks.Clear();
            CurrentTask = null;

            foreach (Load l in shuttleConveyor.Route.Loads)
            {
                l.Dispose();
            }

            base.Reset();
        }

        public override void Dispose()
        {
            shuttleAP.OnEnter               -= shuttleAP_OnEnter;
            trackRail.Car.OnPositionChanged -= Car_OnPositionChanged;
            ShuttleTasks.CollectionChanged  -= ShuttleTasks_CollectionChanged;
            shuttleAP.Dispose();
            shuttleConveyor.Dispose();
            trackRail.Dispose();
            base.Dispose();
        }    

        /// <summary>
        /// A Load has just arrived onto the shuttle; the ShuttleAP is the AP on the conveyor attached to the shuttle.
        /// </summary>
        void shuttleAP_OnEnter(ActionPoint sender, Load load)
        {
            load.Stop();
            if (!CurrentTask.Source.IsRackBinLocation()) //If there was a load at the infeed rack conveyor behind the load that has just arrived onto shuttle release this load so that it arrived at B
            {
                ParentMultiShuttle.ConveyorLocations.Find(x => x.LocName == CurrentTask.Source.Substring(0, CurrentTask.Source.Length - 1) + "A").Release(); //Release a load at A if there is one there
            }

            ParentMultiShuttle.ArrivedAtShuttle(new TaskEventArgs(CurrentTask,load));
            MoveShuttle(CurrentTask.DestPosition);
        }

        public ShuttleTask CurrentTask
        {
            get { return currentTask; }
            set
            {                
                if (value != null)
                {
                    currentTask = value;
                    trackRail.ShuttleCar.ShuttleTaskDisplay = value.ToString();
                    MoveShuttle(value.SourcePosition);
                }
                else if (value == null && ShuttleTasks.Count != 0)
                {
                    ShuttleTask newTask = null;
                    foreach(ShuttleTask st in ShuttleTasks)
                    {
                        if(st.Destination.ConvType() == ConveyorTypes.OutfeedRack)
                        {
                            var v = ParentMultiShuttle.ConveyorLocations.Find(c => c.LocName == st.Destination);
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
                    else
                    {
                        currentTask = null;
                        trackRail.ShuttleCar.ShuttleTaskDisplay = "";
                    }
                }
                else if (value == null)
                {
                    currentTask = value;
                    trackRail.ShuttleCar.ShuttleTaskDisplay = "";
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
                    newTask.DestPosition = SetShuttlePosition(newTask.Destination.RackXLocation());
                    newTask.SourcePosition = SetShuttlePosition(newTask.Source.RackXLocation());
                    stNew = newTask;
                }

                ///// Check for repeated tasks This shouldn't happen however the controller/WMS may issue repeated tasks //////
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
                ///////

                if (CurrentTask == null ) //shuttle not doing a task add this to the current task
                {
                    var loc = ParentMultiShuttle.ConveyorLocations.Find(c => c.LocName == stNew.Destination);

                    if (loc == null || (loc != null && !loc.Active)) //If destination was a rack conveyor then should not be null if it was a rack location then it will be equal to null AND there is space to put down
                    {                    
                        foreach (ShuttleTask newTask in e.NewItems)
                        {
                            CurrentTask = newTask;
                            ShuttleTasks.Remove(newTask);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Calculates the distance along the rail that the shuttle will travel. i.e. the position that the Shuttle.trackRail.MoveTo ActionPoint is set when executing this task
        /// </summary>
        /// <param name="rackXLocation">The number of the location</param>
        /// <returns> The distance along the rail that the shuttle will travel</returns>
        private float SetShuttlePosition(float rackXLocation)
        {
            if (rackXLocation > 0) // Position is within the rack
            {
                if (ParentMultiShuttle.FREX)
                {
                    if (rackXLocation > ParentMultiShuttle.FrexPositions)//In the normal rack positions
                    {
                        float frontDistance = (ParentMultiShuttle.FrexPositions * ParentMultiShuttle.LocationLength) + (ParentMultiShuttle.RackConveyorLength + ParentMultiShuttle.ElevatorConveyorLength); //The rail length for the front positions
                        //totalFrontLocs is the number of positions associated with the frontDistance, note that PSDSlocations are only notional positions as they are not avaiable for the shuttle and they would be a different size compared with ParentMultiShuttle.LocationLength.
                        //however the PS/DS/elevator space is still a bay
                        float totalFrontLocs = ParentMultiShuttle.FrexPositions + ParentMultiShuttle.PSDSlocations;
                        return ((rackXLocation - totalFrontLocs) * ParentMultiShuttle.LocationLength) + frontDistance - (ParentMultiShuttle.LocationLength / 2);
                    }
                    else // in the Front Rack EXtension
                    {
                        return (rackXLocation * ParentMultiShuttle.LocationLength) - (ParentMultiShuttle.LocationLength / 2);
                    }
                }
                else
                {
                    return ((rackXLocation * ParentMultiShuttle.LocationLength) + ParentMultiShuttle.RailLengthOffset) - (ParentMultiShuttle.LocationLength / 2);
                }
            }
            else //Must be a rack conveyor instead
            {
                if (ParentMultiShuttle.FREX) // If the rack is extended past the front then the position along the trackrail will not be zero.
                {
                    return ParentMultiShuttle.InfeedRackShuttleLocation;
                }
                else
                {
                    return 0;
                }
            }
        }

        /// <summary>
        /// Moves the shuttle to the position provided.
        /// </summary>
        internal void MoveShuttle(float position)
        {
            float currentDistance = trackRail.MoveTo.Distance;

            if (trackRail.MoveTo.Distance != position)
            {
                trackRail.MoveTo.Distance = position;

                if (trackRail.MoveTo.Distance < currentDistance)
                {
                    trackRail.Route.Motor.Backward();
                }
                else if (trackRail.MoveTo.Distance > currentDistance)
                {
                    trackRail.Route.Motor.Forward();
                }
                trackRail.Route.Motor.Start();
                trackRail.ShuttleCar.Release();
            }
            else //no need to move as already at the correct location
            {
                ShuttleOnArrived();
            }
        }  

        public void ShuttleOnArrived()
        {
            if (CurrentTask != null)
            {
                if (trackRail.MoveTo.Distance == ParentMultiShuttle.InfeedRackShuttleLocation) //workarround for experior bug...if the straight transport section is already at the correct location then the load will not be releasable from the action point but if shuttle moves to th position then it can be released...WTF
                {
                    MoveShuttle(ParentMultiShuttle.InfeedRackShuttleLocation + 0.01f);
                    return;
                }
                else if (HasArrivedAtRackInfeedConv() && shuttleConveyor.Route.Loads.Count == 0)
                {
                    var cl = ParentMultiShuttle.ConveyorLocations.Find(x => x.LocName == CurrentTask.Source);
                    if (cl != null && cl.Active)
                    {
                        Vector3 v3 = shuttleAP.Position - cl.ActiveLoad.Position;
                        v3.Y = 0; //Y is up so do not move the height.
                        cl.ActiveLoad.Translate(() => cl.ActiveLoad.Switch(shuttleAP, true), v3, ParentMultiShuttle.TimeToPos1);
                    }
                    return;
                }

                if (trackRail.MoveTo.Distance == CurrentTask.SourcePosition && shuttleConveyor.Route.Loads.Count == 0 && !HasArrivedAtRackInfeedConv()) //Retrieving from a bin location
                {
                    Case_Load boxLoad = ParentMultiShuttle.Controller.CreateCaseLoad(CurrentTask.caseData);
                    Load.Items.Add(boxLoad);
                    boxLoad.Yaw = (float)Math.PI / 2;
                    ParentMultiShuttle.LoadCreated(new LoadCreatedEventArgs(boxLoad));
                    boxLoad.Switch(shuttleAP2);

                    float timeToTransfer;
                    Vector3 transferVector;
                    CreateTransferVector(boxLoad, out timeToTransfer, out transferVector);

                    boxLoad.Translate(transferVector, 0);                                                                   //Translate away from the suttle AP in zero time
                    shuttleAP2.ActiveLoad.Translate(() => boxLoad.Switch(shuttleAP), transferVector * -1, timeToTransfer);  //Then translate back (hence -1) in in time to transfer


                }
                else if (shuttleConveyor.Route.Loads.Count != 0) //Delevering to a rackbin location or an outfeed point
                {
                    float timeToTransfer;
                    Vector3 transferVector;
                    CreateTransferVector((Case_Load)shuttleAP.ActiveLoad, out timeToTransfer, out transferVector);

                    //rackLoc = CurrentTask.Destination.Side() == RackSide.Right ? shuttleAP_Zpos * -1 : shuttleAP_Zpos; //Get the side

                    //int loadDepth = CurrentTask.Destination.LoadDepth(); //Get the depth in the rack
                    //float timeToTransfer = ParentMultiShuttle.TimeToPos1;

                    //if (loadDepth == -1)  //-1 = IA = Inter Aisle Transfer
                    //{
                    //    timeToTransfer = ParentMultiShuttle.TimeToPos1 + ParentMultiShuttle.TimeToPos2;
                    //    rackLoc = rackLoc * 3; //dropoff into the other aisle
                    //}
                    //else if(loadDepth == 2)
                    //{
                    //    rackLoc =  rackLoc * 2;
                    //    timeToTransfer = ParentMultiShuttle.TimeToPos2;
                    //}

                    //Vector3 direction = Trigonometry.DirectionYaw(Trigonometry.Yaw(shuttleAP.ActiveLoad.Route.Orientation));
                    //Vector3 rotated = Trigonometry.CrossProduct(direction, new Vector3(0, rackLoc, 0));

                    int dir = 1;
                    if (CurrentTask.Source.Side() != CurrentTask.Destination.Side())
                    {
                        dir = -1;
                    }
                    shuttleAP.ActiveLoad.Translate(() => ArrivedAtDest(shuttleAP.ActiveLoad), transferVector * dir, timeToTransfer);
                }
            }           
        }
        private void CreateTransferVector(Case_Load boxLoad, out float timeToTransfer, out Vector3 transferVector)
        {
            rackLoc = CurrentTask.Source.Side() == RackSide.Right ? shuttleAP_Zpos * -1 : shuttleAP_Zpos; //Get the side

            int loadDepth = CurrentTask.Destination.LoadDepth(); //Get the depth in the rack
            timeToTransfer = ParentMultiShuttle.TimeToPos1;

            if (loadDepth == -1)  //-1 = IA = Inter Aisle Transfer
            {
                timeToTransfer = ParentMultiShuttle.TimeToPos1 + ParentMultiShuttle.TimeToPos2;
                rackLoc = rackLoc * 3; //dropoff into the other aisle
            }
            else if (loadDepth == 2)
            {
                rackLoc = rackLoc * 2;
                timeToTransfer = ParentMultiShuttle.TimeToPos2;
            }

            Vector3 direction = Trigonometry.DirectionYaw(Trigonometry.Yaw(boxLoad.Route.Orientation));
            transferVector = Trigonometry.CrossProduct(direction, new Vector3(0, rackLoc, 0));
        }

        private void ArrivedAtDest(Load load)
        {
            if (CurrentTask.Destination.ConvType() == ConveyorTypes.OutfeedRack)
            {
                load.Switch(ParentMultiShuttle.ConveyorLocations.Find(x => x.LocName == CurrentTask.Destination),true);
            }
            else
            {
                ParentMultiShuttle.ArrivedAtRackLocation(new TaskEventArgs(CurrentTask, load));
                load.Dispose();
                CurrentTask = null;
            }
        }

        private bool HasArrivedAtRackInfeedConv()
        {
            return trackRail.MoveTo.Distance <= ParentMultiShuttle.InfeedRackShuttleLocation + 0.1f && trackRail.MoveTo.Distance >= ParentMultiShuttle.InfeedRackShuttleLocation - 0.1f;
        }

        private bool HasArrivedAtRackOutfeedConv()
        {
            return trackRail.MoveTo.Distance <= ParentMultiShuttle.OutfeedRackShuttleLocation + 0.1f && trackRail.MoveTo.Distance >= ParentMultiShuttle.OutfeedRackShuttleLocation - 0.1f;
        }

        void Car_OnPositionChanged(Load load, Vector3 position)
        {
            shuttleConveyor.LocalPosition = new Vector3(-load.Distance + trackRail.Length / 2, 0, 0);
        }

        public int Level
        {
            get {return shuttleInfo.level;}
            set {shuttleInfo.level = value;}
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

    public class ShuttleInfo : AssemblyInfo 
    {
        public MultiShuttleInfo multiShuttleinfo;
        public int level;
        public MultiShuttle parentMultishuttle;

    }
}
