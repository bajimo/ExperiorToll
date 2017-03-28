using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Runtime.Serialization;
using System.Xml.Serialization;

using Experior.Core;
using Experior.Core.Assemblies;
using Experior.Core.Communication.PLC;
using Experior.Core.Loads;
using Experior.Core.Mathematics;
using Experior.Core.Motors;
using Experior.Core.Parts;
using Experior.Core.Properties;
using Experior.Core.Properties.TypeConverter;
using Experior.Core.Routes;

using Microsoft.DirectX;
using Xcelgo.Serialization;

namespace Experior.Catalog.Dematic.Storage.MultiShuttle.Assemblies
{
    public class Box1 : Assembly
    {
        private List<Cube> floatingElements = new List<Cube>();
        private Box1Info box1Info;

        public Box1(Box1Info info)
            : base(info)
        {
            box1Info = info as Box1Info;
            for (int i = 0; i < 13; i++)
            {
                floatingElements.Add(new Cube(box1Info.color, Core.Primitives.IPrimitive.Types.Solid));
                AddPart((RigidPart)floatingElements[i]);
            }

            DisposeBox1();
            CreateBox1();
        }

        private void DisposeBox1()
        {
            foreach (Cube floatingElement in floatingElements)
            {
                floatingElement.Dispose();
            }
        }

        private void CreateBox1()
        {

            floatingElements[0] = new Cube(box1Info.colors, box1Info.length, box1Info.thickness, box1Info.thickness);
            floatingElements[1] = new Cube(box1Info.colors, box1Info.thickness, box1Info.height, box1Info.thickness);
            floatingElements[2] = new Cube(box1Info.colors, box1Info.length, box1Info.thickness, box1Info.thickness);
            floatingElements[3] = new Cube(box1Info.colors, box1Info.thickness, box1Info.height, box1Info.thickness);
            floatingElements[4] = new Cube(box1Info.colors, box1Info.thickness, box1Info.thickness, box1Info.width);
            floatingElements[5] = new Cube(box1Info.colors, box1Info.thickness, box1Info.thickness, box1Info.width);
            floatingElements[6] = new Cube(box1Info.colors, box1Info.thickness, box1Info.thickness, box1Info.width);
            floatingElements[7] = new Cube(box1Info.colors, box1Info.thickness, box1Info.thickness, box1Info.width);
            floatingElements[8] = new Cube(box1Info.colors, box1Info.length, box1Info.thickness, box1Info.thickness);
            floatingElements[9] = new Cube(box1Info.colors, box1Info.thickness, box1Info.height, box1Info.thickness);
            floatingElements[10] = new Cube(box1Info.colors, box1Info.length, box1Info.thickness, box1Info.thickness);
            floatingElements[11] = new Cube(box1Info.colors, box1Info.thickness, box1Info.height, box1Info.thickness);
            floatingElements[12] = new Cube(box1Info.colors, box1Info.length - box1Info.thickness, box1Info.height - box1Info.thickness,
                box1Info.width - box1Info.thickness, Core.Primitives.IPrimitive.Types.SolidTransparent);
            floatingElements[12].RenderOption = RigidPart.RenderingMode.Transparent;

            foreach (Cube floatingElement in floatingElements)
            {
                AddPart(((RigidPart)floatingElement));
            }

            floatingElements[0].LocalPosition = new Vector3(0, 0, 0);
            floatingElements[1].LocalPosition = new Vector3(-(box1Info.length - box1Info.thickness) / 2, box1Info.height / 2, 0);
            floatingElements[2].LocalPosition = new Vector3(0, box1Info.height - box1Info.thickness / 2, 0);
            floatingElements[3].LocalPosition = new Vector3((box1Info.length - box1Info.thickness) / 2, box1Info.height / 2, 0);
            floatingElements[4].LocalPosition = new Vector3(-(box1Info.length - box1Info.thickness) / 2, 0, (box1Info.width - box1Info.thickness) / 2);
            floatingElements[5].LocalPosition = new Vector3(-(box1Info.length - box1Info.thickness) / 2, box1Info.height - box1Info.thickness / 2, (box1Info.width - box1Info.thickness) / 2);
            floatingElements[6].LocalPosition = new Vector3((box1Info.length - box1Info.thickness) / 2, 0, (box1Info.width - box1Info.thickness) / 2);
            floatingElements[7].LocalPosition = new Vector3((box1Info.length - box1Info.thickness) / 2, box1Info.height - box1Info.thickness / 2, (box1Info.width - box1Info.thickness) / 2);
            floatingElements[8].LocalPosition = new Vector3(0, 0, box1Info.width - box1Info.thickness / 2);
            floatingElements[9].LocalPosition = new Vector3(-(box1Info.length - box1Info.thickness) / 2, box1Info.height / 2, box1Info.width - box1Info.thickness / 2);
            floatingElements[10].LocalPosition = new Vector3(0, box1Info.height - box1Info.thickness / 2, box1Info.width - box1Info.thickness / 2);
            floatingElements[11].LocalPosition = new Vector3((box1Info.length - box1Info.thickness) / 2, box1Info.height / 2, box1Info.width - box1Info.thickness / 2);
            floatingElements[12].LocalPosition = new Vector3(box1Info.thickness / 2, (box1Info.height - box1Info.thickness) / 2, (box1Info.width - box1Info.thickness) / 2);
        }

        #region Catalogue Properties

        public override string Category { get { return "Box1"; } }

        [Category("Size")]
        [Description("Thickness")]
        [DisplayName("Thickness")]
        [PropertyOrder(0)]
        [TypeConverter(typeof(FloatConverter))]
        public virtual float Thickness
        {
            get { return box1Info.thickness; }
            set
            {
                box1Info.thickness = value; DisposeBox1(); CreateBox1();
            }
        }

        [Category("Size")]
        [Description("Length")]
        [DisplayName("Length")]
        [PropertyOrder(1)]
        [TypeConverter(typeof(FloatConverter))]
        public virtual float Length
        {
            get { return box1Info.length; }
            set
            {
                box1Info.length = value; DisposeBox1(); CreateBox1();
            }
        }

        [Category("Size")]
        [Description("Width")]
        [DisplayName("Width")]
        [PropertyOrder(2)]
        [TypeConverter(typeof(FloatConverter))]
        public virtual float Width
        {
            get { return box1Info.width; }
            set
            {
                box1Info.width = value; DisposeBox1(); CreateBox1();
            }
        }

        [Category("Size")]
        [Description("Height")]
        [DisplayName("Height")]
        [PropertyOrder(3)]
        [TypeConverter(typeof(FloatConverter))]
        public virtual float Height
        {
            get { return box1Info.height; }
            set
            {
                box1Info.height = value; DisposeBox1(); CreateBox1();
            }
        }

        //[Browsable(true)]
        //[Category("Visualization")]
        //[Description("Colour")]
        //[PropertyOrder(0)]
        //public override Color Color
        //{
        //    get { return box1Info.colors; }
        //    set
        //    {
        //        box1Info.colors = value; DisposeBox1(); CreateBox1();
        //    }
        //}

        public override Image Image { get { return Common.Icons.Get("box1"); } }

        #endregion

    }

    [Serializable]
    [XmlInclude(typeof(AssemblyInfo))]
    public class Box1Info : AssemblyInfo
    {
        public float thickness;
        public float length;
        public float width;
        public float height;
        public Color colour;

        //[XmlIgnore]
        //[NonSerialized]
        public Color colors;

        //[Browsable(false)]
        //[XmlElement("Color")]
        //public string Color
        //{
        //    get { return Converter.SerializeColor(this.colors); }
        //    set { this.colors = Converter.DeserializeColor(value); }
        //}
    }
}
