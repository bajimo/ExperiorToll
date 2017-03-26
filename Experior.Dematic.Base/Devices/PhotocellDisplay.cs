using Experior.Core.Assemblies;
using Experior.Core.Parts;
using Microsoft.DirectX;
using System;
using System.Drawing;

namespace Experior.Dematic.Base.Devices
{
    public class PhotocellDisplay : Assembly
    {
        public Cylinder cylinder = new Cylinder(Color.Green, (0.78f), 0.02f, 20);
        private Cube leftCube = new Cube(Color.LightGray, 0.04f, 0.1f, 0.04f);
        private Cube rightCube = new Cube(Color.LightGray, 0.04f, 0.1f, 0.04f);
        public event EventHandler OnPhotocellDisplayDeleted;
        public bool deleteFromUser = true;

        public PhotocellDisplay(PhotocellDisplayInfo info) : base(info)
        {
            Add(cylinder);
            Add(leftCube);
            Add(rightCube);

            cylinder.Length = info.width + 0.07f;
            cylinder.LocalPosition = new Vector3(0, 0.05f, 0);
            leftCube.LocalPosition = new Vector3(0, 0.025f, info.width / 2 + 0.02f);
            rightCube.LocalPosition = new Vector3(0, 0.025f, -(info.width / 2 + 0.02f));

            //Generate select event to select object into properties window
            cylinder.OnSelected += OnDeviceSelected;
            leftCube.OnSelected += OnDeviceSelected;
            rightCube.OnSelected += OnDeviceSelected;
        }

        public void OnDeviceSelected(RigidPart sender)
        {
            Core.Environment.Properties.Set(this.Parent);
            cylinder.Select();
            leftCube.Select();
            rightCube.Select();
        }

        public override void Dispose()
        {
            //RemoveAssembly(this);
            cylinder.Dispose();
            leftCube.Dispose();
            rightCube.Dispose();

            if (OnPhotocellDisplayDeleted != null)
                OnPhotocellDisplayDeleted(this, new EventArgs());

            base.Dispose();
        }

        public override bool Visible
        {
            get
            {
                return base.Visible;
            }

            set
            {
                base.Visible = value;
                cylinder.Visible = value;
                leftCube.Visible = value;
                rightCube.Visible = value;
            }
        }

        public virtual float Distance
        {
            get;
            set;
        }

        #region Catalogue Properties

        public override string Category
        {
            get { return "Photcell Point"; }
        }

        public override Image Image
        {
            get { return Common.Icons.Get("photoeye"); }
        }

        #endregion
    }
}
