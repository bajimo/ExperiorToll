using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Xml.Serialization;
using Experior.Catalog.Dematic.Case.Components;
using Experior.Core.Assemblies;
using Experior.Core.Loads;
using Experior.Core.Motors;
using Experior.Core.Routes;
using Microsoft.DirectX;
using Experior.Dematic.Base;
using Experior.Catalog.Assemblies;
using Experior.Catalog.Dematic.Case;

namespace Experior.Catalog.Dematic.Storage.Assemblies
{
    public class MultishuttleElevatorJobData
    {
        public MultiShuttleElevator Parent;
        public JobModes JobMode = JobModes.Load1;
        public MultiShuttleDirections JobType;
        public Case_Load CaseLoadPosition1, CaseLoadPosition2;
        public string[] MissionTelegram;
        public string DestinationsLevel;
        public string DestinationGroupSide;

        public IConvToElevator UnloadConveyor { get; set; }  
        public OutfeedDropStationConveyor DropStationConv { get; set; }

        public enum JobModes
        {
            Load1,
            Load2,
            Unload1,
            Unload2,
            WaitingUnload1,
            WaitingUnload2
        }

    }
   
    public class MultiShuttleElevator : Assembly
    {
        public MultiShuttleDirections ElevatorType { get { return multishuttleinfo.ElevatorType; } }
        public DematicMultiShuttle ParentMultiShuttle;
        public ElevatorConveyor ElevatorConveyor;
        private Shuttle lift;

      //  private ActionPoint loc1 = new ActionPoint();
      //  private ActionPoint loc2 = new ActionPoint();

        MultiShuttleElevatorInfo multishuttleinfo;
        private Shuttle.ShuttleCarController control;

        public Shuttle.ShuttleCarController Control
        {
            get { return control; }      
        }

        [Browsable(false)]
        public ElevatorConveyor LiftConveyor
        {
            get { return ElevatorConveyor; }
        }

        private MultishuttleElevatorJobData currentJobData;

        public MultishuttleElevatorJobData CurrentJobData
        {
            get { return currentJobData; }
            set{ currentJobData = value; }            
        }


        public void Forward()
        {
            ElevatorConveyor.Route.Motor.Forward();
        }

        public void Backward()
        {
            ElevatorConveyor.Route.Motor.Backward();
        }

        public int NumberOfTotes
        {
            get { return ElevatorConveyor.Route.Loads.Count; }
        }

        public float ElevatorSpeed
        {
            get { return lift.Route.Motor.Speed; }
            set { lift.Route.Motor.Speed = value; }
        }

        public float ElevatorConveyorSpeed
        {
            get { return ElevatorConveyor.Route.Motor.Speed; }
            set { ElevatorConveyor.Route.Motor.Speed = value; }
        }

        public string ElevatorName
        {
            get { return multishuttleinfo.ElevatorName; }
        }

        public MultiShuttleElevator(MultiShuttleElevatorInfo info) : base(info)
        {
            multishuttleinfo = info;            
            Embedded = true;
            ParentMultiShuttle = info.Multishuttle;

            //ElevatorConveyor = new StraightTransportSection(Color.Gray, info.multishuttleinfo.ElevatorConveyorLength, info.multishuttleinfo.ElevatorConveyorWidth);
            ElevatorConveyor = new ElevatorConveyor(new ElevatorConveyorInfo { length  = info.multishuttleinfo.ElevatorConveyorLength,
                                                                             width     = info.multishuttleinfo.ElevatorConveyorWidth,
                                                                             thickness = 0.05f,
                                                                             color     = Core.Environment.Scene.DefaultColor,
                                                                             Elevator  = this                                                                     
            });

            Add(ElevatorConveyor);
            ElevatorConveyor.Route.Motor.Speed      = info.multishuttleinfo.ConveyorSpeed;
            ElevatorConveyor.LocalYaw               = (float) Math.PI;
            lift                        = new Shuttle(info.multishuttleinfo, 1, info.Multishuttle);
            lift.UserData               = this;
            lift.Car.OnPositionChanged += Car_PositionChanged; 
            AddPart(lift);
           
            lift.LocalRoll = -(float)Math.PI / 2;

            lift.Route.Motor.Speed = multishuttleinfo.multishuttleinfo.elevatorSpeed;
            lift.Car.Visible = false;
                  
            control = lift.Control;    

      
        }

        void Car_PositionChanged(Load load, Vector3 position)
        {
            ElevatorConveyor.LocalPosition = new Vector3(0, load.Distance - lift.Length / 2, 0);
        }  
         
        public float ElevatorHeight
        {
            get { return lift.Length; }
            set
            {
                if (value <= 0) {return; }
                lift.Length = value;
            }
        }

        public override void Reset()
        {
            control.Reset();
            CurrentJobData = null;
            base.Reset();
        }

        public override void Dispose()
        {
            lift.Car.OnPositionChanged -= Car_PositionChanged; 
            base.Dispose();
        }

        #region Properties

        public override string Category
        {
            get { return "MultiShuttleElevator"; }
        }

        public override Image Image
        {
            get { return Common.Icons.Get("MultiShuttleElevator"); }
        }

        #endregion

    }

    [Serializable]
    [XmlInclude(typeof(MultiShuttleElevatorInfo))]
    public class MultiShuttleElevatorInfo : Experior.Core.Assemblies.AssemblyInfo
    {
        #region Fields

        public DematicMultiShuttleInfo multishuttleinfo;
        public string ElevatorName;
        internal DematicMultiShuttle Multishuttle;

        private static MultiShuttleElevatorInfo properties = new MultiShuttleElevatorInfo();
        public MultiShuttleDirections ElevatorType;

        #endregion

        #region Properties

        public static object Properties
        {
            get
            {
                properties.color = Experior.Core.Environment.Scene.DefaultColor;
                return properties;
            }
        }

        #endregion
    }
}