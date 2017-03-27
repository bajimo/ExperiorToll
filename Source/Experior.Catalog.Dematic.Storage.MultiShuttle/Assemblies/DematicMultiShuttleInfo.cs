using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Experior.Core.Assemblies;
using System.Drawing;
using Experior.Catalog.Assemblies;
using System.ComponentModel;


namespace Experior.Catalog.Dematic.Storage.Assemblies
{
    [Serializable]
    public class DematicMultiShuttleInfo : AssemblyInfo
    {
        public DematicMultiShuttle.OutFeedNamingConventions OutfeedNamingConvention = DematicMultiShuttle.OutFeedNamingConventions.OLD_POS1_POS2_002_001;

        public bool MixedInfeedOutfeed = false;
        public bool LeftEvenLevelInfeed = false;

        public bool MultiShuttleDriveThrough = false;
        public string AisleNo = "";
     
        public float shuttlecarSpeed = 4.0f;
        public float loadingSpeed = 0.5f;
        public float raillength = 20;
        public float unloadingSpeed = 0.5f;

        public float carheight = 0.02f;
        public float carwidth = 1.0f;
        public float carlength = 1.0f;

        public int ShuttleNumber = 12;

        public float elevatorSpeed = 3.0f;

        public float ConveyorSpeed = 0.7f;

        public bool ElevatorFR = true;
        public bool ElevatorFL = true;
        public bool ElevatorBR = false;
        public bool ElevatorBL = false;

        public MultiShuttleDirections ElevatorFLtype = MultiShuttleDirections.Infeed;
        public MultiShuttleDirections ElevatorFRtype = MultiShuttleDirections.Outfeed;
        public MultiShuttleDirections ElevatorBLtype = MultiShuttleDirections.Infeed;
        public MultiShuttleDirections ElevatorBRtype = MultiShuttleDirections.Outfeed;

        public List<LevelHeight> LevelHeightPickstations = new List<LevelHeight>();
        public List<LevelHeight> LevelHeightDropstations = new List<LevelHeight>();

        public string ControllerName;

        public DematicMultiShuttleInfo()
            : base()
        {

        }

        public float DistanceLevels = 0.55f;

        public float RackHeightOffset = 0;

        public float ElevatorConveyorLength = 1.2f;

        public float ElevatorConveyorWidth = 0.75f;

        public float RackConveyorLength = 1.2f;

        public float PickStationConveyorLength = 1.2f;

        public float RackConveyorWidth = 0.7f;

        public float PickStation2Timeout = 30f; //seconds to wait for tote nr 2;

        public int RackBays = 40;

        public float DepthDist = 0.7f;
        public float ShuttlePositioningTime = 0.5f;
        public float ToteWidth = 0.45f;
        public float ToteLength = 0.65f;
        public float ToteHeight = 0.32f;
        public float ToteWeight = 2.3f;
        public int ToteColorArgb = System.Drawing.Color.Peru.ToArgb();
        public string FrontRightElevatorGroupName = "F";
        public string FrontLeftElevatorGroupName = "F";
        public string FrontLeftInfeedRackGroupName = "F";
        public string FrontRightInfeedRackGroupName = "F";
        public string FrontLeftOutfeedRackGroupName = "F";
        public string FrontRightOutfeedRackGroupName = "F";

        public float DriveThroughElevatorOffset = 0;
    }

    public class LevelHeight
    {
        public string Level { get; set; }
        public float Height { get; set; }
        public string Side { get; set; } //Only used by drive through 
    }

}
