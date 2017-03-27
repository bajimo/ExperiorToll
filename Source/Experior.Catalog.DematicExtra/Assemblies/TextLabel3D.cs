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
using Xcelgo.Serialization;

namespace Experior.Catalog.Assemblies.Extra
{
    public class TextLabel3D : Assembly
    {
        private Text3D text;
        private TextLabel3DInfo textInfo;
        private Font font;

        public TextLabel3D(TextLabel3DInfo info): base(info)
        {
            textInfo = info as TextLabel3DInfo;
            font = new Font("Helvetica", 0.4f, FontStyle.Bold, GraphicsUnit.Pixel);
            text = new Text3D(info.textColor, 1.5f, Depth, font);
            text.Pitch = (float)Math.PI / 2;
          
            Add((RigidPart)text);
            //text.LocalPosition = new Microsoft.DirectX.Vector3(0.57f, -0.447f, -0.5f);
            text.LocalPosition = new Microsoft.DirectX.Vector3(0,0,0);
            Size = textInfo.size;
            text.Text = info.theText;
        }

        [Category("Configuration")]
        [Description("Change the size of the text")]
        public float Size
        {
            get { return textInfo.size; }
            set
            {
                textInfo.size = value;
                UpdateText();
            }
        }

        private void UpdateText()
        {
            text.Dispose();
            text = new Text3D(textInfo.textColor, Size, Depth, font);
            text.Pitch = (float)Math.PI / 2;
            text.Text = textInfo.theText;
            Add((RigidPart)text);//, new Microsoft.DirectX.Vector3(0, 0, 0));
            //text.LocalPosition = new Microsoft.DirectX.Vector3((0.75f * Size), -0.447f, -Size * 0.8f);
            text.LocalPosition = new Microsoft.DirectX.Vector3(0,0,0);
        }

        [Category("Configuration")]
        [DisplayName("Text")]
        public string TheText 
        {
            get { return textInfo.theText;}
            set
            {
                textInfo.theText = value;
                text.Text = value;
            } 
        }

        [Category("Configuration")]
        [DisplayName("Colour")]
        public Color LoadColour
        {
            get
            {
                return textInfo.textColor;
            }
            set
            {
                textInfo.textColor = value;
                text.Color = textInfo.textColor;
            }
        }

        [Category("Configuration")]
        [DisplayName("Depth")]
        public float Depth
        {
            get { return textInfo.depth; }
            set 
            {
                textInfo.depth = value;
                UpdateText();
            }
        }


        [Browsable(false)]
        public override EventCollection Events
        {
            get{ return base.Events;}
        }

        [Browsable(false)]
        public override bool Enabled
        {
            get{return base.Enabled;}
            set{base.Enabled = value;}
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
    public class TextLabel3DInfo : AssemblyInfo
    {
        public float size = 0.5f;
        public string theText = "Text";
        public float depth = 0.15f;

        [XmlIgnore]
        [NonSerialized]
        public Color textColor = System.Drawing.Color.FromArgb(60, 60, 60);

        [Browsable(false)]
        [XmlElement("Color")]
        new public string Color
        {
            get
            {
                return Converter.SerializeColor(this.textColor);
            }
            set
            {
                this.textColor = Converter.DeserializeColor(value);
            }
        }
    }
}
