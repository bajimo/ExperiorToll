using Experior.Catalog.Dematic.Case.Components;
using Experior.Catalog.Logistic.Track;
using Experior.Core;
using Experior.Core.Assemblies;
using Experior.Core.Loads;
using Experior.Core.Parts;
using Experior.Core.Properties;
using Experior.Core.Routes;
using Experior.Core.TransportSections;
using Experior.Dematic.Base;
using Microsoft.DirectX;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Xml.Serialization;
using Xcelgo.Serialization;
using Experior.Dematic.Base.Devices;

namespace Experior.Catalog.Dematic.Case.Devices
{
    public class LoadFeeder : Device
    {
        private LoadFeederInfo loadFeederInfo;

        public ActionPoint FeederActionPoint = new ActionPoint();
        private Experior.Core.Parts.Cube FeederCube;
        private Experior.Core.Parts.Arrow StartArrow;
        private Timer feedTimer;
        private Case_Load lastLoad = null;

        #region Constructors

        public LoadFeeder(LoadFeederInfo info, BaseTrack conv): base(info, conv)
        {
            loadFeederInfo = info;// as LoadFeederInfo;

            FeederCube = new Experior.Core.Parts.Cube(Color.LightGray, 0.651f, 0.351f, 0.451f);
            FeederCube.Rigid = false;
            FeederCube.Selectable = true;
            FeederCube.OnSelected += EaterCube_OnSelected;

            StartArrow = new Core.Parts.Arrow(0.35f);
            StartArrow.Color = Color.Green;
            StartArrow.Selectable = true;
            StartArrow.OnSelected += EaterCube_OnSelected;

            Add((RigidPart)FeederCube);
            Add((RigidPart)StartArrow);

            FeederCube.LocalPosition = new Vector3(0, 0.15f, 0);
            StartArrow.LocalPosition = new Vector3(0, 0.315f, 0);
            conv.TransportSection.Route.InsertActionPoint(FeederActionPoint);

            FeederActionPoint.Distance = info.distance;
            FeederActionPoint.OnEnter += FeederActionPoint_OnEnter;
            FeederActionPoint.Visible = false;

            if (loadFeederInfo.feedInterval != 0)
            {
                feedTimer = new Core.Timer(loadFeederInfo.feedInterval);
                feedTimer.AutoReset = true;
                feedTimer.OnElapsed += feedTimer_OnElapsed;
            }

            Enabled = loadFeederInfo.enabled;            
        }

        void FeederActionPoint_OnEnter(ActionPoint sender, Load load)
        {
            //load.Dispose();
        }

        void feedTimer_OnElapsed(Timer sender)
        {
            if (lastLoad == null || lastLoad.Distance != DeviceDistance)
            {
                //lastLoad = FeedCase.FeedCaseLoad((ITransportSection)conveyor.TransportSection, DeviceDistance);
                lastLoad = FeedLoad.FeedCaseLoad((ITransportSection)conveyor.TransportSection, DeviceDistance, LoadLength, LoadWidth, LoadHeight, LoadWeight, LoadColour, LoadBarcodeLength, Case_Load.GetCaseControllerCaseData());
                StartArrow.Color = Color.Green;
            }
            else
            {
                StartArrow.Color = Color.Red;
            }
        }

        #endregion

        #region Administration methods

        public override void Reset()
        {
            base.Reset();
            StartArrow.Color = Color.Green;
            lastLoad = null;
            feedTimer.Reset();
            if (Enabled)
            {
                feedTimer.Start();
            }
        }

        public override void Dispose()
        {
            if (FeederActionPoint != null)
            {
                FeederActionPoint.OnEnter -= FeederActionPoint_OnEnter; 
            }

            base.Dispose();
        }

        void EaterCube_OnSelected(RigidPart sender)
        {
            Core.Environment.Properties.Set(this);
        }

        public override void Device_OnSizeUpdated(object sender, SizeUpdateEventArgs e)
        { }

        #endregion

        #region Properties

        #region User Interface

        #region Default userinterface items that are removed from properties window
        [Browsable(false)]
        public override bool Visible
        {
            get{return base.Visible;}
            set{ base.Visible = value;}
        }

        [Browsable(false)]
        public override string SectionName
        {
            get{return base.SectionName;}
            set{base.SectionName = value;}
        }

        [Browsable(false)]
        public override Core.Assemblies.EventCollection Events
        {
            get{return base.Events;}
        }

        #endregion

        [Category("Configuration")]
        [DisplayName("Distance (m.)")]
        [Description("The distance from the start of the conveyor")]
        [TypeConverter()]
        public override float DeviceDistance
        {
            get
            {
                return loadFeederInfo.distance;
            }
            set
            {
                FeederActionPoint.Distance  = value;
                loadFeederInfo.distance     = value;

                Experior.Catalog.Logistic.Track.Curve assem = Parent as Experior.Catalog.Logistic.Track.Curve;

                if (assem != null)
                {
                    double theta = value / assem.Radius;
                    double x,z;
                    if (assem.Revolution == Core.Environment.Revolution.Counterclockwise)
                    {
                        z                  = assem.Radius * (Math.Sin(theta));
                        x                  = assem.Radius * (Math.Cos(theta));
                        this.LocalPosition = new Vector3(-(float)x, 0.05f, (float)z);
                        this.LocalYaw = -(float)theta;
                        //commCylinder
                    }
                    else
                    {
                        z                  = assem.Radius * (Math.Cos(theta));
                        x                  = assem.Radius * (Math.Sin(theta));
                        this.LocalPosition = new Vector3((float)x, 0.05f, (float)z);
                        this.LocalYaw = (float)theta;
                    }
                }
                else  //It's a straight conveyor
                {
                    StraightConveyor assem2 = Parent as StraightConveyor;
                    this.LocalPosition = new Vector3(assem2.Length / 2 - value, 0.05f, 0);
                }
            }
        }

        [Category("Configuration")]
        [DisplayName("Feed Interval")]
        [Description("Release frequency of loads (s)econds")]
        [Experior.Core.Properties.AlwaysEditable]
        public float FeedInterval
        {
            get { return loadFeederInfo.feedInterval; }
            set
            {
                if (feedTimer.Running)
                {
                    feedTimer.Stop();
                    feedTimer.Timeout = value;
                    feedTimer.Start();
                }
                else
                    feedTimer.Timeout = value;

                loadFeederInfo.feedInterval = value;
            }
        }

        [Category("Configuration")]
        [DisplayName("Enabled")]
        [Description("Enable or disable the feeder")]
        [Experior.Core.Properties.AlwaysEditable]
        public override bool Enabled
        {
            get
            {
                return loadFeederInfo.enabled;
            }
            set
            {
                loadFeederInfo.enabled = value;
                if (value)
                    feedTimer.Start();
                else
                    feedTimer.Stop();
            }
        }

        [Category("Load")]
        [DisplayName("Length (m.)")]
        [Description("Length of the load when created")]
        [PropertyOrder(1)]
        [Experior.Core.Properties.AlwaysEditable]
        public float LoadLength
        {
            get
            {
                return loadFeederInfo.LoadLength;
            }
            set
            {
                loadFeederInfo.LoadLength = value;
            }
        }

        [Category("Load")]
        [DisplayName("Width (m.)")]
        [Description("Width of the load when created")]
        [PropertyOrder(2)]
        [Experior.Core.Properties.AlwaysEditable]
        public float LoadWidth
        {
            get
            {
                return loadFeederInfo.LoadWidth;
            }
            set
            {
                loadFeederInfo.LoadWidth = value;
            }
        }

        [Category("Load")]
        [DisplayName("Height (m.)")]
        [Description("Height of the load when created")]
        [PropertyOrder(3)]
        [Experior.Core.Properties.AlwaysEditable]
        public float LoadHeight
        {
            get
            {
                return loadFeederInfo.LoadHeight;
            }
            set
            {
                loadFeederInfo.LoadHeight = value;
            }
        }

        [Category("Load")]
        [DisplayName("Weight (kg.)")]
        [Description("Weight of the load when created")]
        [PropertyOrder(4)]
        [Experior.Core.Properties.AlwaysEditable]
        public float LoadWeight
        {
            get
            {
                return loadFeederInfo.LoadWeight;
            }
            set
            {
                loadFeederInfo.LoadWeight = value;
            }
        }

        [Category("Load")]
        [DisplayName("Colour")]
        [Description("Colour of the load when created")]
        [PropertyOrder(5)]
        [Experior.Core.Properties.AlwaysEditable]

        public Color LoadColour
        {
            get
            {
               return loadFeederInfo.loadColour; 
            }
            set
            {
                loadFeederInfo.loadColour = value;                
            }
        }

        // Converter.SerializeColor(this.color);

        [Category("Load")]
        [DisplayName("Barcode Length")]
        [Description("Length of barcode when the load is created")]
        [PropertyOrder(6)]
        [Experior.Core.Properties.AlwaysEditable]
        public int LoadBarcodeLength
        {
            get
            {
                return loadFeederInfo.LoadBarcodeLength;
            }
            set
            {
                loadFeederInfo.LoadBarcodeLength = value;
            }
        }

        #endregion

        #region Catalogue Properties

        [Experior.Core.Properties.Event]
        public override void DoubleClick()
        {
            //This doesn't seem to work! I would like to double click to start the timer and release a load in
            // but the double click does not seem to do anything, even though it works on the other assemblies
            //Also cannot add a double click to the cube or anything else
            if (feedTimer.Running)
                feedTimer.Stop();
            else
            {
                feedTimer.Start();

                FeedLoad.FeedCaseLoad((ITransportSection)conveyor.TransportSection, DeviceDistance, Case_Load.GetCaseControllerCaseData());
            }
            
            base.DoubleClick();
        }

        public override string Category
        {
            get { return "Communication Point"; }
        }

        public override Image Image
        {
            get { return Common.Icons.Get("commpoint"); }
        }

        #endregion

        #endregion
    }

    [Serializable]
    [XmlInclude(typeof(LoadFeederInfo))]
    public class LoadFeederInfo : DeviceInfo
    {
        public float feedInterval = 5;
        public bool enabled = false;
        public float LoadLength = 0.6f;
        public float LoadWidth = 0.4f;
        public float LoadHeight = 0.25f;
     
       // public string loadColour = System.Drawing.Color.MediumBlue.Name;
       // public string colourName;

        public float LoadWeight = 2.3f;
        public int LoadBarcodeLength = FeedLoad.BarcodeLength;

        [XmlIgnore]
        [NonSerialized]
        public Color loadColour = System.Drawing.Color.FromName("MediumBlue");

        [Browsable(false)]
        [XmlElement("Color")]
        new public string Color
        {
            get
            {
                return Converter.SerializeColor(this.loadColour);
            }
            set
            {
                this.loadColour = Converter.DeserializeColor(value);
            }
        }

        public override void SetCustomInfoFields(Assembly assem, object obj, ref DeviceInfo info)
        {
            
        }
    }
}