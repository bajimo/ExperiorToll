using Experior.Core.Assemblies;
using Experior.Core.Parts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.Drawing;
using System.ComponentModel;
using Experior.Catalog.Extras;

namespace Experior.Catalog.Assemblies.Extra
{
    class Dematic_Logo  : Assembly
    {
        private Cube floatingElement;
        private Text3D dematicText;
        private DematicLogoInfo dematicLogoInfo;
        private Font font;

        public Dematic_Logo(DematicLogoInfo info): base(info)
        {
            dematicLogoInfo = info as DematicLogoInfo;
            floatingElement = new Cube(Color.FromArgb(249, 178, 0), 0.17f, 0.075f, 1f);
            font = new Font("Helvetica", 0.4f, FontStyle.Bold, GraphicsUnit.Pixel);

            dematicText = new Text3D(Color.FromArgb(60, 60, 60), 0.5f, 0.15f, font);
            dematicText.Pitch = (float)Math.PI / 2;
            dematicText.Text = "DEMATIC";

            Add((RigidPart)floatingElement);
            Add((RigidPart)dematicText);

            floatingElement.LocalPosition = new Microsoft.DirectX.Vector3(0, -0.41f, 0);
            dematicText.LocalPosition = new Microsoft.DirectX.Vector3(0.57f, -0.447f, -0.5f);

            Size = dematicLogoInfo.size;
        }

        [Category("Logo")]
        [Description("Change the size of the logo")]
        public float Size
        {
            get { return dematicLogoInfo.size; }
            set
            {
                dematicText.Dispose();
                dematicText = new Text3D(Color.FromArgb(60, 60, 60), value, 0.15f, font);
                dematicText.Pitch = (float)Math.PI / 2;
                dematicText.Text = "DEMATIC";
                Add((RigidPart)dematicText);//, new Microsoft.DirectX.Vector3(0, 0, 0));

                floatingElement.Size = new Microsoft.DirectX.Vector3((0.32f * value), 0.075f, (1.6f * value));
                dematicText.LocalPosition = new Microsoft.DirectX.Vector3((0.75f * value), -0.447f, -value * 0.8f);
                dematicLogoInfo.size = value;
            }
        }

        [Browsable(false)]
        public override EventCollection Events
        {
            get
            {
                return base.Events;
            }
        }

        [Browsable(false)]
        public override bool Enabled
        {
            get
            {
                return base.Enabled;
            }
            set
            {
                base.Enabled = value;
            }
        }



        #region Catalogue Properties

        public override string Category
        {
            get { return "Extra"; }
        }

        public override Image Image
        {


            get { return Common.Icons.Get("dematic"); }
        }

        #endregion

    }

    [Serializable]
    [XmlInclude(typeof(DematicLogoInfo))]
    public class DematicLogoInfo : AssemblyInfo
    {
        public float size = 0.5f;
    }
}
