using Experior.Catalog.Dematic.Case;
using Experior.Core.Assemblies;
using Experior.Dematic.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Serialization;
using Xcelgo.Serialization;


namespace Experior.Catalog.Dematic.Storage.MultiShuttle.Assemblies
{
    [Serializable]
    [XmlInclude(typeof(MultiShuttleInfo))]
    public class MultiShuttleInfo : AssemblyInfo, IControllableInfo
    {
       // public CreateDematicMultiShuttle.OutFeedNamingConventions OutfeedNamingConvention = CreateDematicMultiShuttle.OutFeedNamingConventions.OLD_POS1_POS2_002_001;

        public bool MixedInfeedOutfeed       = false;
        public bool LeftEvenLevelInfeed      = false;
        public bool DriveThrough = false;
        public string DriveThroughLocations = "";
        public int AisleNo;
        public float shuttlecarSpeed         = 4.0f;
        public float loadingSpeed            = 0.5f;
        public float raillength              = 20;
        public float unloadingSpeed          = 0.5f;
        public float carheight               = 0.02f;
        public float carwidth                = 1.0f;
        public float carlength               = 0.5f;
        public int ShuttleNumber             = 12;
        public float elevatorSpeed           = 3.0f;
        public float ConveyorSpeed           = 0.7f;
        public bool ElevatorFR               = true;
        public bool ElevatorFL               = true;
        public bool ElevatorBR               = false;
        public bool ElevatorBL               = false;
        public string flElevatorGroupName    = "A";
        public string frElevatorGroupName    = "A";
        public float ElevatorGap = 0;
        public int transparency              = 200;
        public bool autoNewElevatorTask      = true;
        public bool frex                     = false;
        public int frexPositions             = 0;
        public float frexElevatorGap         = 0;
        public bool showBoxGraphic           = false;
        public int psdsLocations             = 5;
        public bool SwitchEnds = false;
        

        public MultiShuttleDirections ElevatorFLtype = MultiShuttleDirections.Infeed;
        public MultiShuttleDirections ElevatorFRtype = MultiShuttleDirections.Outfeed;
        public MultiShuttleDirections ElevatorBLtype = MultiShuttleDirections.Infeed;
        public MultiShuttleDirections ElevatorBRtype = MultiShuttleDirections.Outfeed;

        public string pickStationConfig = "3:01;4:02";   //level and id or name eg : "3:01;4:02" -> 3m and id of "01" and 4m and id of "02"
        public string dropStationConfig = "1:01;2:02";

        public OutfeedLength psOutfeed = OutfeedLength._125mm;
        //public float dsInfeed = 0.375f;
        public string dsInfeedConfig = "0.375:01;0:02";

        public float rackHeight = 0;   //Drivethrough only

        public float psTimeout = 5;

        public MultiShuttleInfo(): base(){}

        public float DistanceLevels                  = 0.55f;
        public float RackHeightOffset                = 0;
        public float ElevatorConveyorLength          = 1.2f;
        public float ElevatorConveyorWidth           = 0.75f;
        public float RackConveyorLength              = 1.2f;
        public float PickStationConveyorLength       = 1.2f;
        public float RackConveyorWidth               = 0.7f;
        public float PickStation2Timeout             = 30f; //seconds to wait for tote nr 2;
        public int rackLocations                          = 40;
        public bool SideSwitch = false;
        //public float DepthDistPos1                   = 0.7f;
        //public float DepthDistPos2                   = 1.4f;
        public float ShuttlePositioningTime          = 0.5f;
        public float elevatorOffset      = 0;
        public float rackAdj = 0;
        public AccumulationPitch PSAccPitch = AccumulationPitch._500mm;

        public float timeToPos1 = 1;
        public float timeToPos2 = 2;
        public float rackWidth = 1.5f;

        private string controllerName = "No Controller";
        public string ControllerName
        {
            get { return controllerName; }
            set { controllerName = value; }
        }

        private ProtocolInfo protocolInfo;
        public ProtocolInfo ProtocolInfo
        {
            get { return protocolInfo; }
            set { protocolInfo = value; }
        }

        [XmlIgnore]
        [NonSerialized]
        public System.Drawing.Color boxColor = System.Drawing.Color.Black;

        [Browsable(false)]
        [XmlElement("BoxColor")]
        public string BoxColor
        {
            get { return Converter.SerializeColor(this.boxColor); }
            set { this.boxColor = Converter.DeserializeColor(value); }
        }

    }

    public class LevelID  
    {
        public string ID { get; set; }
        public float Height { get; set; }
        public string Side { get; set; } //Only used by drive through 
    }

}
