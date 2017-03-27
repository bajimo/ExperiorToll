using System.Drawing;
using Experior.Dematic.Base;
using Experior.Catalog.Dematic.Case.Components;
using Experior.Dematic;
using Experior.Core.Routes;
using Experior.Core.Loads;

namespace Experior.Catalog.Dematic.Storage.MultiShuttle.Assemblies
{
    public class Task{}

    public class ElevatorTask : Task
    {
        public Elevator Elevator { get; set; }

        public string LoadA_ID 
        { 
            get { return loadA_ID;}
            private set 
            {
                if (!string.IsNullOrWhiteSpace(value)) 
                {
                    loadA_ID = value;
                    numberOfLoadsInTask++; 
                }

            } 
        }

        public string LoadB_ID 
        {
            get { return loadB_ID; }
            private set 
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    loadB_ID = value;
                    numberOfLoadsInTask++;
                }
            }
        }

        public string SourceLoadA { get; set; }
        public string SourceLoadB { get; set; }
        public string DestinationLoadA { get; set; }
        public string DestinationLoadB { get; set; }
        public Cycle LoadCycle { get; set; }
        public Cycle UnloadCycle { get; set; }
        public TaskType Flow { get; set; }
        public BaseCaseData caseDataA;
        public BaseCaseData caseDataB;

        public StraightConveyor SourceLoadAConv, SourceLoadBConv, DestinationLoadAConv, DestinationLoadBConv;

        private int numberOfLoadsInTask;
        private string loadA_ID, loadB_ID;

        public ElevatorTask(string _loadA_ID, string _loadB_ID)
        {
            numberOfLoadsInTask = 0;
            LoadA_ID = _loadA_ID;
            LoadB_ID = _loadB_ID;
        }

        /// <summary>
        /// Keeps a count of how many of the current tasks loads have arrived on the conveyor this is then used to determine if loads are loading onto the conveyor of of of the conveyor
        /// </summary>
        public int TasksLoadsArrivedOnElevator;

        /// <summary>
        /// Checks to see if the load that at either 1 or 2 locations is part of the task
        /// </summary>     
        public bool RelevantElevatorTask(ActionPoint apX, ActionPoint apY = null )
        {
            if (apX != null && apX.Active)
            {
                if (LoadA_ID == apX.ActiveLoad.Identification || LoadB_ID == apX.ActiveLoad.Identification)
                {
                    return true;
                }
            }

            if (apY != null && apY.Active)
            {
                if (LoadA_ID == apY.ActiveLoad.Identification || LoadB_ID == apY.ActiveLoad.Identification)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Specify a load to check if it is part of the task
        /// </summary>
        public bool RelevantElevatorTask(Load load)
        {
            if (load == null) return false;

            if (LoadA_ID == load.Identification || LoadB_ID == load.Identification)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Returns the Destination conveyor of load
        /// </summary>
        public StraightConveyor GetDestConvOfLoad(Case_Load load)
        {
            if (load == null || !RelevantElevatorTask(load))
            {
                return null;
            }

            if (load.Identification == LoadA_ID)
            {
                return DestinationLoadAConv;
            }
            return DestinationLoadBConv;
        }

        public StraightConveyor GetSourceConvOfLoad(Case_Load load, bool UseOtherLoad)
        {
            if (load == null || !RelevantElevatorTask(load))
            {
                return null;
            }

            if (!UseOtherLoad)
            {
                if (load.Identification == this.LoadA_ID)
                {
                    return SourceLoadAConv;
                }

                return SourceLoadBConv;
            }
            else  //get the conveyor of the other load to pick up
            {
                if (load.Identification == this.LoadA_ID)
                {
                    return SourceLoadBConv;
                }
            }
            return SourceLoadAConv;

        }

        public override string ToString()
        {
            return string.Format("Aisle: {0}, LoadA_ID: {1}, LoadB_ID{2}, SourceLoadA: {3}, SourceLoadB: {4}, DestLoadA: {5}, DestLoadB: {6}, LoadCycle: {7}, UnloadCycle: {8}, Flow: {9}",
                Elevator == null ? "" : Elevator.AisleNumber.ToString(),
                LoadA_ID == null ? "" : LoadA_ID.ToString(),
                LoadB_ID == null ? "" : LoadB_ID.ToString(),
                SourceLoadA == null ? "" : SourceLoadA,
                SourceLoadB == null ? "" : SourceLoadB,
                DestinationLoadA == null ? "" : DestinationLoadA,
                DestinationLoadB == null ? "" : DestinationLoadB,
                LoadCycle.ToString(),
                UnloadCycle.ToString(),
                Flow.ToString()
                );
        }

        //public override bool Equals(object obj)
        public override bool Equals(object obj)            
        {
            if (obj == null) return false;

            ElevatorTask et = obj as ElevatorTask;
            if (et == null) return false;

            //Not checking CaseData 

            bool retVal = false;

            if(et.DestinationLoadAConv == null && DestinationLoadAConv == null){
                retVal = true;                
            }
            else if (et.DestinationLoadAConv != null && et.DestinationLoadAConv.Equals(DestinationLoadAConv))
            {
                retVal = true;
            }

            if (!(retVal && et.DestinationLoadBConv == null && DestinationLoadBConv == null))
            {
                retVal = false;
            }
            else if (et.DestinationLoadBConv != null && et.DestinationLoadBConv.Equals(DestinationLoadBConv))
            {
                retVal = true;
            }
                
            return retVal && et.ToString() == this.ToString(); 
        }

        public override int GetHashCode()
        {
            return this.ToString().GetHashCode();
        }

        public int NumberOfLoadsInTask
        {
            get { return numberOfLoadsInTask; }
        } 
    }

    public class ShuttleTask : Task
    {
        public int Level { get; set; }
        public string LoadID { get; set; }

        public BaseCaseData caseData;

        public string Source { get; set; }  //Can be either rack conveyor or rack bin location
        public string Destination { get; set; } //sxxxyydd, s=Side (L or R), xxx=Xlocation (e.g. 012), yy=Level (e.g. 02), dd=Depth (01 or 02)
        public float SourcePosition, DestPosition;

        public override bool Equals(object obj)
        {
            if(obj == null) return false;

            ShuttleTask st = obj as ShuttleTask;
            if (st == null) return false;

            return st.ToString() == this.ToString();
        }

        public override int GetHashCode()
        {
            return this.ToString().GetHashCode();
        }

        public override string ToString()
        {
            return string.Format("Level: {0}, LoadID: {1}, Source: {2}, Dest: {3}, SourcePos: {4}, DestPos: {5}", 
                Level == 0 ? "" : Level.ToString(),
                !string.IsNullOrEmpty(LoadID) ? "" : LoadID, 
                !string.IsNullOrEmpty(Source) ? "" : Source, 
                !string.IsNullOrEmpty(Destination) ? "" : Destination, 
                SourcePosition.ToString(),  //zero is valid 
                DestPosition.ToString());   //zero is valid
        }
        
    }
}