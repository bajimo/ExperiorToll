using System;
using System.ComponentModel;
using System.Drawing;
using System.Xml.Serialization;
using Experior.Catalog.Assemblies;
using Experior.Core.Properties;
using Experior.Core.Properties.Collections;
using Experior.Dematic.Base;
using Xcelgo.Serialization;
using Experior.Catalog.Dematic.Pallet.Assemblies;

namespace Experior.Catalog.Dematic.Storage.PalletCrane.Assemblies
{
    [Serializable]
    public class PalletCraneInfo : HBWMiniLoadInfo, IControllableInfo
    {
        public ExpandablePropertyList<StationConfiguration> PickStations { get; set; }
        public ExpandablePropertyList<StationConfiguration> DropStations { get; set; }
        public float DepthDist = 1;
        public int RackBays = 1;
        public int RackLevels = 1;
        public float RackOffsetFront = 1;
        //public float RackLength = 19;
        public float RackWidth = 1.5f;
        public float RackOffsetBottom = 0;
        //public float RackHeight = 10;
        public int RackTransparency = 200;

        public float AisleWidth = 2.1f;
        public int DepthsInRack = 2;
        public float TimeToDepth1 = 1.5f;
        public float TimeToDepth2 = 2;
        public float TimeToDepth3 = 2.5f;

        public string LHDname = "";

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
        public Color RackColor = Core.Environment.Scene.DefaultColor;

        [Browsable(false)]
        [XmlElement("RackColor")]
        public string SaveColor
        {
            get { return Converter.SerializeColor(RackColor); }
            set { RackColor = Converter.DeserializeColor(value); }
        }
    }

    [Serializable]
    [XmlInclude(typeof(StationConfiguration))]
    [TypeConverter(typeof(ObjectConverter))]
    public class StationConfiguration : PalletStraightInfo
    {
        public event EventHandler<StationConfigurationChangedEventArgs> StationConfigurationChanged;

        private float levelHeight;
        private float distanceX;
        private PalletCraneStationSides side;

        [DisplayName(@"Level Height (m.)")]
        [Category("Config")]
        public float LevelHeight
        {
            get { return levelHeight; }
            set
            {
                levelHeight = value;
                StationConfigurationChanged?.Invoke(this, new StationConfigurationChangedEventArgs(this));
            }
        }

        [DisplayName(@"X offset (m.)")]
        [Category("Config")]
        public float DistanceX
        {
            get { return distanceX; }
            set
            {
                distanceX = value;
                StationConfigurationChanged?.Invoke(this, new StationConfigurationChangedEventArgs(this));
            }
        }

        [DisplayName(@"Name")]
        [Category("Config")]
        [Description("Name of the station as received in the messages from ATC")]
        public string Name
        {
            get { return name; }
            set { name = value; }
        }

        [Category("Config")]
        [PropertyOrder(2)]
        [Description("Which side of the aisle the station is relative to the front of the aisle")]
        [DisplayName(@"Side")]
        public PalletCraneStationSides Side
        {
            get { return side; }
            set
            {
                if (side != value)
                {
                    side = value;
                    StationConfigurationChanged?.Invoke(this, new StationConfigurationChangedEventArgs(this));
                }
            }
        }

        [Browsable(false)]
        public PalletCraneStationTypes StationType { get; set; }

        public override string ToString()
        {
            return "";
        }
    }

    public class StationConfigurationChangedEventArgs : EventArgs
    {
        public readonly StationConfiguration Configuration;
        public StationConfigurationChangedEventArgs(StationConfiguration configuration)
        {
            Configuration = configuration;
        }
    }
}
