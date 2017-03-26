using Experior.Catalog.Extras;
using Experior.Core.Assemblies;
using Experior.Core.Parts;
using Experior.Core.Properties;
using Experior.Core.Properties.TypeConverter;
using Microsoft.DirectX;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Xml.Serialization;
using Xcelgo.Serialization;

namespace Experior.Catalog.Assemblies.Extra
{
    public class DematicBox : Assembly
    {
        private List<Cube> floatingElements = new List<Cube>();
        private DematicBoxInfo boxInfo;

        public DematicBox(DematicBoxInfo info)
            : base(info)
        {
            boxInfo = info as DematicBoxInfo;
            for (int i = 0; i < 13; i++)
            {
                floatingElements.Add(new Cube(boxInfo.color, Core.Primitives.Primitive.Types.Solid));
                Add((RigidPart)floatingElements[i]);
            }
            BoxColor = info.boxColor; //This will draw the initial box
        }

        private void RenderBoxes()
        {
            //First delete them
            foreach (Cube floatingElement in floatingElements)
            {
                floatingElement.Dispose();
            }

            //Then re-draw them
            floatingElements[0] = new Cube(Color, boxInfo.length, boxInfo.thickness, boxInfo.thickness);
            floatingElements[1] = new Cube(Color, boxInfo.thickness, boxInfo.height, boxInfo.thickness);
            floatingElements[2] = new Cube(Color, boxInfo.length, boxInfo.thickness, boxInfo.thickness);
            floatingElements[3] = new Cube(Color, boxInfo.thickness, boxInfo.height, boxInfo.thickness);
            floatingElements[4] = new Cube(Color, boxInfo.thickness, boxInfo.thickness, boxInfo.width);
            floatingElements[5] = new Cube(Color, boxInfo.thickness, boxInfo.thickness, boxInfo.width);
            floatingElements[6] = new Cube(Color, boxInfo.thickness, boxInfo.thickness, boxInfo.width);
            floatingElements[7] = new Cube(Color, boxInfo.thickness, boxInfo.thickness, boxInfo.width);
            floatingElements[8] = new Cube(Color, boxInfo.length, boxInfo.thickness, boxInfo.thickness);
            floatingElements[9] = new Cube(Color, boxInfo.thickness, boxInfo.height, boxInfo.thickness);
            floatingElements[10] = new Cube(Color, boxInfo.length, boxInfo.thickness, boxInfo.thickness);
            floatingElements[11] = new Cube(Color, boxInfo.thickness, boxInfo.height, boxInfo.thickness);
            floatingElements[12] = new Cube(Color, boxInfo.length - boxInfo.thickness, boxInfo.height - boxInfo.thickness,
                boxInfo.width - boxInfo.thickness, Core.Primitives.Primitive.Types.SolidTransparent);
            floatingElements[12].RenderOption = RigidPart.RenderingMode.Transparent;

            foreach (Cube floatingElement in floatingElements)
            {
                Add(((RigidPart)floatingElement));
            }

            floatingElements[0].LocalPosition = new Vector3(0, 0, 0);
            floatingElements[1].LocalPosition = new Vector3(-(boxInfo.length - boxInfo.thickness) / 2, boxInfo.height / 2, 0);
            floatingElements[2].LocalPosition = new Vector3(0, boxInfo.height - boxInfo.thickness / 2, 0);
            floatingElements[3].LocalPosition = new Vector3((boxInfo.length - boxInfo.thickness) / 2, boxInfo.height / 2, 0);
            floatingElements[4].LocalPosition = new Vector3(-(boxInfo.length - boxInfo.thickness) / 2, 0, (boxInfo.width - boxInfo.thickness) / 2);
            floatingElements[5].LocalPosition = new Vector3(-(boxInfo.length - boxInfo.thickness) / 2, boxInfo.height - boxInfo.thickness / 2, (boxInfo.width - boxInfo.thickness) / 2);
            floatingElements[6].LocalPosition = new Vector3((boxInfo.length - boxInfo.thickness) / 2, 0, (boxInfo.width - boxInfo.thickness) / 2);
            floatingElements[7].LocalPosition = new Vector3((boxInfo.length - boxInfo.thickness) / 2, boxInfo.height - boxInfo.thickness / 2, (boxInfo.width - boxInfo.thickness) / 2);
            floatingElements[8].LocalPosition = new Vector3(0, 0, boxInfo.width - boxInfo.thickness / 2);
            floatingElements[9].LocalPosition = new Vector3(-(boxInfo.length - boxInfo.thickness) / 2, boxInfo.height / 2, boxInfo.width - boxInfo.thickness / 2);
            floatingElements[10].LocalPosition = new Vector3(0, boxInfo.height - boxInfo.thickness / 2, boxInfo.width - boxInfo.thickness / 2);
            floatingElements[11].LocalPosition = new Vector3((boxInfo.length - boxInfo.thickness) / 2, boxInfo.height / 2, boxInfo.width - boxInfo.thickness / 2);
            floatingElements[12].LocalPosition = new Vector3(boxInfo.thickness / 2, (boxInfo.height - boxInfo.thickness) / 2, (boxInfo.width - boxInfo.thickness) / 2);
        }

        #region Catalogue Properties

        public override string Category { get { return "Dematic Box"; } }
        public override Image Image { get { return Common.Icons.Get("Box"); } }
        
        [Browsable(false)]
        public override bool Visible
        {
            get
            {
                return base.Visible;
            }
            set
            {
                base.Visible = value;
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

        [Browsable(false)]
        public override EventCollection Events
        {
            get
            {
                return base.Events;
            }
        }

        [Category("Size")]
        [Description("Thickness of the box frame in mm")]
        [DisplayName("Thickness")]
        [PropertyOrder(0)]
        [TypeConverter(typeof(FloatConverter))]
        public virtual float Thickness
        {
            get { return boxInfo.thickness; }
            set
            {
                boxInfo.thickness = value; 
                RenderBoxes();
            }
        }

        [Category("Size")]
        [Description("Length of the box in meters")]
        [DisplayName("Length")]
        [PropertyOrder(1)]
        [TypeConverter]//(typeof(FloatConverter))]
        public virtual float Length
        {
            get { return boxInfo.length; }
            set
            {
                boxInfo.length = value; 
                RenderBoxes();
            }
        }

        [Category("Size")]
        [Description("Width of the box in meters")]
        [DisplayName("Width")]
        [PropertyOrder(2)]
        [TypeConverter]//(typeof(FloatConverter))]
        public virtual float Width
        {
            get { return boxInfo.width; }
            set
            {
                boxInfo.width = value; 
                RenderBoxes();
            }
        }

        [Category("Size")]
        [Description("Height of the box in meters")]
        [DisplayName("Height")]
        [PropertyOrder(3)]
        [TypeConverter]//(typeof(FloatConverter))]
        public virtual float Height
        {
            get { return boxInfo.height; }
            set
            {
                boxInfo.height = value; 
                RenderBoxes();
            }
        }

        [Category("Visualization")]
        [Description("Colour of the box")]
        [DisplayName("Color")]
        [PropertyOrder(0)]
        public Color BoxColor
        {
            get { return boxInfo.boxColor; }
            set
            {
                boxInfo.boxColor = value;
                Transparency = boxInfo.transparency; //Set the transparency which will re-render the cubes
            }
        }

        [Category("Visualization")]
        [PropertyOrder(1)]
        [DisplayName("Transparency")]
        [Description("Value between 0 and 255, the higher the number the more transparent the box will be")]
        public int Transparency
        {
            get { return boxInfo.transparency; }
            set
            {
                if (value > 255) { value = 255; }
                else if (value < 0) { value = 0; }
                boxInfo.transparency = value;
                Color = System.Drawing.Color.FromArgb(value, boxInfo.boxColor);
                RenderBoxes();
            }
        }
        #endregion
    }

    [Serializable]
    [XmlInclude(typeof(AssemblyInfo))]
    public class DematicBoxInfo : AssemblyInfo
    {
        public float thickness = 0.015f;
        new public float length = 1;
        new public float width = 1;
        new public float height = 1;
        public int transparency = 150;

        [XmlIgnore]
        [NonSerialized]
        public Color boxColor = Core.Environment.Scene.DefaultColor;

        [Browsable(false)]
        [XmlElement("BoxColor")]
        public string SaveColor
        {
            get { return Converter.SerializeColor(this.boxColor); }
            set { this.boxColor = Converter.DeserializeColor(value); }
        }
    }
}
