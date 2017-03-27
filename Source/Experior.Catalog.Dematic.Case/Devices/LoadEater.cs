using Experior.Catalog.Dematic.Case.Components;
using Experior.Catalog.Logistic.Track;
using Experior.Core.Assemblies;
using Experior.Core.Loads;
using Experior.Core.Parts;
using Experior.Core.Routes;
using Experior.Dematic.Base.Devices;
using Microsoft.DirectX;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Xml.Serialization;

namespace Experior.Catalog.Dematic.Case.Devices
{
    public class LoadEater : Device
    {
        private LoadEaterInfo loadEaterInfo;

        //public Experior.Core.Parts.Cube EaterCube;
        public ActionPoint EaterActionPoint = new ActionPoint();

        public delegate void EaterPointArrival(LoadEater loadEater, Load load);
        public EaterPointArrival eaterPointArrival;
        Experior.Core.Parts.Cube EaterCube;
        Experior.Core.Parts.Cube StopCube;


        #region Constructors

        public LoadEater(LoadEaterInfo info, BaseTrack conv): base(info, conv)
        {
            loadEaterInfo = info;// as LoadEaterInfo;

            EaterCube = new Experior.Core.Parts.Cube(Color.LightGray, 0.651f, 0.351f, 0.451f);
            EaterCube.Selectable = true;
            EaterCube.OnSelected += EaterCube_OnSelected;

            StopCube = new Core.Parts.Cube(Color.Red, 0.35f, 0.35f, 0.35f);
            StopCube.Selectable = true;
            StopCube.OnSelected += EaterCube_OnSelected;



            Add((RigidPart)EaterCube);
            Add((RigidPart)StopCube);

            EaterCube.LocalPosition = new Vector3(0, 0.15f, 0);
            StopCube.LocalPosition = new Vector3(0, 0.17f, 0);
            conv.TransportSection.Route.InsertActionPoint(EaterActionPoint);

            EaterActionPoint.Distance = info.distance;
            EaterActionPoint.OnEnter += EaterActionPoint_OnEnter;
            EaterActionPoint.Visible = false;
        }

        void EaterActionPoint_OnEnter(ActionPoint sender, Load load)
        {
            load.Dispose();
        }

        #endregion

        #region Administration methods

        public override void Dispose()
        {
            if (EaterActionPoint != null)
            {
                EaterActionPoint.OnEnter -= EaterActionPoint_OnEnter; 
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
                return loadEaterInfo.distance;
            }
            set
            {
                EaterActionPoint.Distance  = value;
                loadEaterInfo.distance     = value;

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

        #endregion

        #region Catalogue Properties

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
    [XmlInclude(typeof(LoadEaterInfo))]
    public class LoadEaterInfo : DeviceInfo
    {

        public override void SetCustomInfoFields(Assembly assem, object obj, ref DeviceInfo info)
        {
            
        }
    }
}