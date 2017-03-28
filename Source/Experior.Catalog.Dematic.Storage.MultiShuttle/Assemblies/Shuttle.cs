using Experior.Core.Assemblies;
using Experior.Core.Loads;
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
        public ActionPoint shuttleAP;
        private ActionPoint enterPointDepth1, enterPointDepth2, exitPointDepth1, exitPointDepth2, transferIn, transferOut;
        internal StraightTransportSection shuttleConveyor;
        internal MultiShuttle ParentMultiShuttle;

        //The direction that the load is travelling from not the direction that the load is travelling from
        //Used to turn the shuttle conveyor
        private float shuttleConvFromRHS = (float)Math.PI / 2 + (float)Math.PI; 
        private float shuttleConvFromLHS = (float)Math.PI / 2;

        private ShuttleInfo shuttleInfo;

        public Shuttle(ShuttleInfo info): base(info)
        {
            shuttleInfo = info;
            trackRail = new TrackRail(info.multiShuttleinfo, info.level, info.parent)
            {
                Name        = "S" + info.level.ToString().PadLeft(2, '0'),
                ThisShuttle = this
            };

            ParentMultiShuttle = info.parent;
            AddPart(trackRail);           

            shuttleConveyor = new StraightTransportSection(Core.Environment.Scene.DefaultColor, ParentMultiShuttle.DepthDistPos2 * 2, 0.5f) { Height = 0.05f };
            Add(shuttleConveyor, new Vector3(trackRail.Length / 2, 0, 0));
            shuttleConveyor.LocalYaw = (float)Math.PI / 2;

            transferIn          = shuttleConveyor.Route.InsertActionPoint(ParentMultiShuttle.DepthDistPos2 - ((info.multiShuttleinfo.RackConveyorWidth / 2) + (info.multiShuttleinfo.carwidth / 2)));
            transferIn.OnEnter += transferIn_OnEnter;

            transferOut          = shuttleConveyor.Route.InsertActionPoint(ParentMultiShuttle.DepthDistPos2 + ((info.multiShuttleinfo.RackConveyorWidth / 2) + (info.multiShuttleinfo.carwidth / 2)));
            transferOut.OnEnter += transferOut_OnEnter;

            shuttleAP          = shuttleConveyor.Route.InsertActionPoint(shuttleConveyor.Length / 2);
            shuttleAP.OnEnter += shuttleAP_OnEnter;

            enterPointDepth1          = shuttleConveyor.Route.InsertActionPoint(ParentMultiShuttle.DepthDistPos2 - ParentMultiShuttle.DepthDistPos1);
            enterPointDepth1.OnEnter += enterPointDepth1_OnEnter;

            enterPointDepth2 = shuttleConveyor.Route.InsertActionPoint(0);

            exitPointDepth1          = shuttleConveyor.Route.InsertActionPoint(ParentMultiShuttle.DepthDistPos2 + ParentMultiShuttle.DepthDistPos1);
            exitPointDepth1.OnEnter += exitPointDepth1_OnEnter;

            exitPointDepth2          = shuttleConveyor.Route.InsertActionPoint(shuttleConveyor.Length);
            exitPointDepth2.OnEnter += exitPointDepth2_OnEnter;

            trackRail.Car.OnPositionChanged += Car_OnPositionChanged;

            ShuttleTasks.Clear();
            ShuttleTasks.CollectionChanged += ShuttleTasks_CollectionChanged;

           // MoveShuttle(ParentMultiShuttle.workarround);
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
            shuttleAP.OnEnter -= shuttleAP_OnEnter;
            enterPointDepth1.OnEnter -= enterPointDepth1_OnEnter;
            exitPointDepth1.OnEnter -= exitPointDepth1_OnEnter;
            exitPointDepth2.OnEnter -= exitPointDepth2_OnEnter;
            trackRail.Car.OnPositionChanged -= Car_OnPositionChanged;
            ShuttleTasks.CollectionChanged -= ShuttleTasks_CollectionChanged;
            shuttleAP.Dispose();
            enterPointDepth1.Dispose();
            enterPointDepth2.Dispose();
            exitPointDepth1.Dispose();
            exitPointDepth2.Dispose();
            shuttleConveyor.Dispose();
            trackRail.Dispose();
            base.Dispose();
        }


        /// <summary>
        /// dropping off at the rack conveyor
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="load"></param>
        void transferOut_OnEnter(ActionPoint sender, Load load)
        {
            var ap = ParentMultiShuttle.ConveyorLocations.Find(x => x.LocName == CurrentTask.Destination);
            if (ap != null)
            {
                load.Switch(ap);
                load.Rotate((float)Math.PI / 2, 0, 0);
            }
        }     

        void exitPointDepth2_OnEnter(ActionPoint sender, Load load)
        {
            if (CurrentTask.Destination.LoadDepth() == 2) // can only be dropping off at a bin location rack conveyors don't exist aat depth 2
            {
                ParentMultiShuttle.ArrivedAtRackLocation(new TaskEventArgs(CurrentTask, load));
                load.Dispose();
                CurrentTask = null;
            }
        }

        void exitPointDepth1_OnEnter(ActionPoint sender, Load load)
        {
             if (trackRail.Destination.Distance > ParentMultiShuttle.workarround && CurrentTask.Destination.LoadDepth() == 1) // not at a rack conveyor so dropping off at a bin location
             {
                 ParentMultiShuttle.ArrivedAtRackLocation(new TaskEventArgs(CurrentTask, load));
                 load.Dispose();                 
                 CurrentTask = null;
             }             
        }

        void shuttleAP_OnEnter(ActionPoint sender, Load load)
        {
            load.Stop();
            if (!CurrentTask.Source.IsRackBinLocation())
            {
                ParentMultiShuttle.ConveyorLocations.Find(x => x.LocName == CurrentTask.Source.Substring(0, CurrentTask.Source.Length - 1) + "A").Release(); //Release a load at A if there is one there
            }

            ChangeShuttleConvDirection(CurrentTask.Destination.Side(), ShuttleConvDirRef.Unloading);
            ParentMultiShuttle.ArrivedAtShuttle(new TaskEventArgs(CurrentTask,load));
            MoveShuttle(CurrentTask.DestPosition);
        }

        void enterPointDepth1_OnEnter(ActionPoint sender, Load load)
        {
            load.Rotate((float)Math.PI, 0, 0);
            load.Release();
            sender.Release();
        }

        void transferIn_OnEnter(ActionPoint sender, Load load)
        {
            load.Rotate((float)Math.PI, 0, 0);
            load.Release();
            sender.Release();
        }  

        public ShuttleTask CurrentTask
        {
            get { return currentTask; }
            set
            {                
                //if (value != null)
                //{
                //    currentTask = value;
                //    ShuttleTasks.RemoveAt(0);
                //    trackRail.ShuttleCar.ShuttleTaskDisplay = value.ToString();
                //    MoveShuttle(value.SourcePosition);
                //}
                //else if (value == null)
                //{
                //    currentTask = value;
                //    trackRail.ShuttleCar.ShuttleTaskDisplay = "";

                //    if (ShuttleTasks.Any())
                //    {
                //        CurrentTask = value;
                //    }

                //}

                if (value != null)
                {
                    currentTask = value;
                    trackRail.ShuttleCar.ShuttleTaskDisplay = value.ToString();
                    MoveShuttle(value.SourcePosition);
                }
                else if (value == null && ShuttleTasks.Count != 0)
                {
                    ShuttleTask sT = ShuttleTasks[0];
                    ShuttleTasks.RemoveAt(0);
                    CurrentTask = sT; //recursive call back to move the shuttle.
                }
                else if (value == null)
                {

                    currentTask = value;
                    trackRail.ShuttleCar.ShuttleTaskDisplay = "";
                }
            }
        }

        void ShuttleTasks_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                ShuttleTask stNew = null;
                foreach (ShuttleTask newTask in e.NewItems)
                {
                    newTask.SourcePosition = newTask.Source.RackXLocation() * ParentMultiShuttle.BayLength;
                    newTask.DestPosition = newTask.Destination.RackXLocation() * ParentMultiShuttle.BayLength;                 
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

                //if (CurrentTask == null && stNew != null) //shuttle not doing a task add this to the current task
                //{
                //    CurrentTask = stNew;
                //}

                if (CurrentTask == null) //shuttle not doing a task add this to the current task
                {
                    foreach (ShuttleTask newTask in e.NewItems)
                    {
                        CurrentTask = newTask;
                        ShuttleTasks.Remove(newTask);
                    }
                }
            }
        }

        internal void MoveShuttle(float position)
        {
            if(CurrentTask.Source.ConvType() == ConveyorTypes.InfeedRack)
            {
                RackConveyor sourceConv = ParentMultiShuttle.GetConveyorFromLocName(CurrentTask.Source) as RackConveyor ;

                if (CurrentTask.Level == Level && sourceConv != null && (shuttleAP.Active || (!shuttleAP.Active && (sourceConv.LocationB.Active && sourceConv.LocationB.ActiveLoad.Identification == CurrentTask.LoadID))))
                {
                    //continue....temp debug code!
                }
                else
                {
                    Log.Write("Error in CurrentTask removing current task", Color.Red);
                    CurrentTask = null;
                    return;
                }
            }

            float currentDistance = trackRail.Destination.Distance;

            if (trackRail.Destination.Distance != position)
            {
                trackRail.Destination.Distance = position;

                if (trackRail.Destination.Distance < currentDistance)
                {
                    trackRail.Route.Motor.Backward();
                }
                else if (trackRail.Destination.Distance > currentDistance)
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

        internal void MoveShuttle(int position)
        {
            if (position != 0)
            {
                float currentDistance = trackRail.Destination.Distance;

                if (trackRail.Destination.Distance != position)
                {
                    trackRail.Destination.Distance = position;

                    if (trackRail.Destination.Distance < currentDistance)
                    {
                        trackRail.Route.Motor.Backward();
                    }
                    else if (trackRail.Destination.Distance > currentDistance)
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
            else 
            {
                MoveShuttle(ParentMultiShuttle.workarround);
            }
        }

        /// <summary>
        /// Depending on the side that you are loading of offloading to turn the whole conveyor arround rather than reverse the motor.
        /// If Loading FROM the Left then conveyor needs to travel from left to right.
        /// if UnLoading TO the left then conveyor needs to travel from left to right.
        /// If Loading FROM the Right then conveyor needs to travel from right to left.
        /// if UnLoading TO the Right then conveyor needs to travel from right to left.
        /// </summary>
        /// <param name="side">Left or right</param>
        /// <param name="taskType">loading or unloading</param>
        private void ChangeShuttleConvDirection(RackSide side, ShuttleConvDirRef taskType)
        {
            if (taskType == ShuttleConvDirRef.Loading)
            {
                if (side == RackSide.Right)
                {
                    shuttleConveyor.LocalYaw = shuttleConvFromRHS;
                }
                else if (side == RackSide.Left)
                {
                    shuttleConveyor.LocalYaw = shuttleConvFromLHS;
                }
            }
            else if (taskType == ShuttleConvDirRef.Unloading)
            {
                if (side == RackSide.Left)
                {
                    shuttleConveyor.LocalYaw = shuttleConvFromRHS;
                }
                else if (side == RackSide.Right)
                {
                    shuttleConveyor.LocalYaw = shuttleConvFromLHS;
                }
            }
        }

        public void ShuttleOnArrived()
        {
            if (CurrentTask != null)
            {
                if (trackRail.Destination.Distance == 0) //workarround for experior bug...if the straight transport section is already at the correct location then the load will not be releasable from the action point but if shuttle moves to th position then it can be released...WTF
                {
                    MoveShuttle(ParentMultiShuttle.workarround);
                    return;
                }
                else if (trackRail.Destination.Distance == ParentMultiShuttle.workarround)
                {                   
                    var cl= ParentMultiShuttle.ConveyorLocations.Find(x => x.LocName == CurrentTask.Source);                  
                    if (cl != null && cl.Active)
                    {
                        cl.ActiveLoad.Switch(transferIn); //switch from rack to shuttleAP      
                    }
                }

                if (trackRail.Destination.Distance == CurrentTask.SourcePosition && shuttleConveyor.Route.Loads.Count == 0) //Have arrived at the rack bin location
                {

                    MeshInfo boxInfo = new MeshInfo()
                    {
                        color    =  CurrentTask.caseData.colour,
                        filename = Case_Load.GraphicsMesh,
                        length   = CurrentTask.caseData.Length,
                        width   = CurrentTask.caseData.Width,
                        height   = CurrentTask.caseData.Height
                    };

                    Case_Load boxLoad = new Case_Load(boxInfo)
                    {
                        Weight         = CurrentTask.caseData.Weight,
                        //SSCCBarcode    = CurrentTask.LoadID,
                        Identification = CurrentTask.LoadID,
                        Case_Data = CurrentTask.caseData  //[BG] 23/03/15 Removed cast
                    };
                   
                    Load.Items.Add(boxLoad);
                    ParentMultiShuttle.LoadCreated(new LoadCreatedEventArgs(boxLoad));

                    ChangeShuttleConvDirection(CurrentTask.Source.Side(),ShuttleConvDirRef.Loading);

                    if (CurrentTask.Source.LoadDepth() == 1)
                    {
                        boxLoad.Switch(enterPointDepth1);
                    }
                    else
                    {
                        boxLoad.Switch(enterPointDepth2);
                    }
                }
                else if (trackRail.Destination.Distance == ParentMultiShuttle.workarround && CurrentTask.DestPosition == 0 || trackRail.Destination.Distance == CurrentTask.DestPosition)
                {
                    if (CurrentTask.Destination.RackXLocation() == 0) //Have arrived at rack conveyor
                    {
                        var v = ParentMultiShuttle.ConveyorLocations.Find(x => x.LocName == CurrentTask.Destination);
                        if (v == null)
                        {
                            Core.Environment.Log.Write(string.Format("Destination {0} does not exist. Are you trying to drop at an infeed rack conveyor or pick from an outfeed rackconveyor", CurrentTask.Destination), Color.Red);
                            Core.Environment.Scene.Pause();
                        }
                        else if (v.Active)
                        {
                            Core.Environment.Log.Write("Shuttle can't drop off destination is blocked",Color.Red);
                            return;
                        }
   
                        shuttleAP.Release();
                    }
                    else
                    {
                        //ParentMultiShuttle.ArrivedAtRackLocation(new TaskEventArgs(CurrentTask,shuttleAP.ActiveLoad));
                        shuttleAP.ActiveLoad.Release();
                    }
                }
                else
                {
                    ChangeShuttleConvDirection(CurrentTask.Source.Side(), ShuttleConvDirRef.Loading);
                }
            }           
        }

        void Car_OnPositionChanged(Load load, Vector3 position)
        {
            shuttleConveyor.LocalPosition = new Vector3(-load.Distance + trackRail.Length / 2, 0, 0);
        }

        public int Level
        {
            get { return shuttleInfo.level;}
            set
            {
                shuttleInfo.level = value;
            }
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
        public MultiShuttle parent;

    }
}
