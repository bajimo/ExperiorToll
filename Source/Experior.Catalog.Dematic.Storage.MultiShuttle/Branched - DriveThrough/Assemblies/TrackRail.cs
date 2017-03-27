using Experior.Core.Assemblies;
using Experior.Core.Loads;
using Experior.Core.TransportSections;
using Microsoft.DirectX;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;

namespace Experior.Catalog.Dematic.Storage.MultiShuttle.Assemblies
{
    public class TrackRail : StraightTransportSection, Core.IEntity
    {
        //internal TrackVehicle trackVehicle;
        private TrackRailInfo trackRailInfo;
        private int level; //Y-coordinate
        private MultiShuttle parentMultishuttle;

        public TrackRail(TrackRailInfo info) : base(Color.Gray, info.parentMultiShuttle.Raillength, 0.05f)
        {
            trackRailInfo = info;
            parentMultishuttle = info.parentMultiShuttle;
            ListSolutionExplorer = true;
            level = info.level;
            Name = "S" + level.ToString().PadLeft(2, '0');

            //TrackVehicleInfo trackVehicleInfo = new TrackVehicleInfo() { trackRail = this, moveToDistance = parentMultishuttle.InfeedRackShuttleLocation };
            //trackVehicle = new TrackVehicle(trackVehicleInfo);
            //Load.Items.Add(trackVehicle);

            route.Motor.Speed = info.shuttlecarSpeed;
            route.Motor.Stop();

           // info.controlAssembly.Add((Core.Parts.RigidPart)this);
            Visible = true;
            
            if (!ParentMultishuttle.ShowBoxGraphic)
            {
                Visible = true;
            }

            Core.Environment.Scene.OnLoaded += Scene_OnLoaded;

        }

        private void Scene_OnLoaded()
        {
           // trackRailInfo.controlAssembly.Add((Core.Parts.RigidPart)this);
        }

        //public override void Dispose()
        //{
        //    if (trackVehicle != null)
        //    {
        //        trackVehicle.Dispose();
        //    }
        //    base.Dispose();
        //}

        [Browsable(false)]
        public MultiShuttle ParentMultishuttle
        {
            get { return parentMultishuttle; }
        }

        //[Browsable(false)]
        //public TrackVehicle ShuttleCar
        //{
        //    get { return trackVehicle; }
        //}

        [Browsable(false)]
        public bool Deletable { get; set; }
        [Browsable(false)]
        public ulong EntityId { get; set; }
        [Browsable(false)]
        public Image Image { get { return null; } }
        [Browsable(false)]
        public bool ListSolutionExplorer { get; set; }
        [Browsable(false)]
        public bool Warning { get { return false; } }

        #region Public Get

        [Browsable(false)]
        public override float Pitch
        {
            get { return base.Pitch; }
            set { base.Pitch = value; }
        }

        [Browsable(false)]
        public override float Roll
        {
            get { return base.Roll; }
            set { base.Roll = value; }
        }

        [Browsable(false)]
        public override float Yaw
        {
            get { return base.Yaw; }
            set { base.Yaw = value; }
        }

        [Browsable(false)]
        public override float Length
        {
            get { return base.Length; }
            set { base.Length = value; }
        }

        [Browsable(false)]
        public override Color Color
        {
            get { return base.Color; }
            set { base.Color = value; }
        }

        [Browsable(false)]
        public override Matrix Orientation
        {
            get { return base.Orientation; }
            set { base.Orientation = value; }
        }
        [Browsable(false)]
        public override Vector3 Position
        {
            get { return base.Position; }
            set {
                base.Position = value;
            }
        }

        //[Browsable(false)]
        //public Box Car
        //{
        //    get { return trackVehicle; }
        //}

        [CategoryAttribute("Level")]
        [DescriptionAttribute("Level")]
        [DisplayName("Level")]
        public int Level
        {
            get { return level; }
        }

        #endregion

    }

    public class TrackRailInfo
    {
        public float shuttlecarSpeed;
        public int level;
        public MultiShuttle parentMultiShuttle;
       // public Elevator elevator = null;
        public Assembly controlAssembly;
    }

}
