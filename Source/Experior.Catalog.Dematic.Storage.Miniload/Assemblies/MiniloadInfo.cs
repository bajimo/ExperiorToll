using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Experior.Core.Assemblies;
using System.Drawing;
using Experior.Catalog.Assemblies;
using System.ComponentModel;
using Experior.Dematic.Base;
using Xcelgo.Serialization;
using System.Xml.Serialization;

namespace Experior.Catalog.Dematic.Storage.Miniload.Assemblies
{
    [Serializable]
    public class MiniloadInfo : HBWMiniLoadInfo, IControllableInfo
    {
        public float DepthDist = 1;
        public int RackBays = 1;
        public int RackLevels = 1;
        public float RackOffsetFront = 1;
        //public float RackLength = 19;
        public float RackWidth = 1.5f;
        public float RackOffsetBottom = 0;
        //public float RackHeight = 10;
        public int RackTransparency = 200;
        public float PdOffsetHeight = 1;
        public float AisleWidth = 2.1f;
        public Miniload.PickDropSide PickAndDropSide = Miniload.PickDropSide.PickLeft_DropRight;
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
            get { return Converter.SerializeColor(this.RackColor); }
            set { this.RackColor = Converter.DeserializeColor(value); }
        }
    }
}
