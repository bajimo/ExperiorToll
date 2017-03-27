using System;
using System.ComponentModel;
using System.Drawing;
using Experior.Core.Parts;
using Experior.Core.Properties;
using Experior.Core.Properties.TypeConverter;
using Microsoft.DirectX;

namespace Experior.Catalog.Dematic.Sorter.Assemblies
{
    public sealed class SorterElementCurve : SorterElement
    {
        private CurveArea curvesorter;

        public SorterElementCurve(SorterElementInfo info)
            : base(info)
        {
            Refresh();
        }

        public override string Category
        {
            get
            {
                return "Sorter Curve";
            }
        }

        [PropertyOrder(4)]
        [Category("Size")]
        [Description("Angle")]
        [DisplayName(@"Angle")]
        [TypeConverter(typeof(AngleConverter))]
        public float Degrees
        {
            get { return ((SorterElementInfo)Info).CurveDegrees; }
            set
            {
                if (value < 0 || value > 360)
                    return;
                ((SorterElementInfo)Info).CurveDegrees = value;
                InvokeRefresh();
            }
        }

        public override Image Image
        {
            get
            {
                return Common.Icons.Get("SorterCurveClockwise");
            }
        }

        [TypeConverter(typeof(FloatConverter))]
        public override float Length
        {
            get
            {
                return base.Length;
            }
            set
            {

            }
        }

        [Browsable(false)]
        public override Vector3 LocalOffset
        {
            get
            {
                return base.LocalOffset;
            }
            set
            {
                base.LocalOffset = value;
            }
        }

        [Browsable(false)]
        public override float LocalYawAllFixpoints
        {
            get
            {
                return base.LocalYawAllFixpoints;
            }
            set
            {
                base.LocalYawAllFixpoints = value;
            }
        }

        [Browsable(false)]
        public override int NumberOfFixPoints
        {
            get
            {
                return base.NumberOfFixPoints;
            }
            set
            {
                base.NumberOfFixPoints = value;
            }
        }

        public override float Pitch
        {
            get
            {
                return base.Pitch;
            }
            set
            {

            }
        }

        [PropertyOrder(3)]
        [Category("Size")]
        [Description("Radius in mm.")]
        [DisplayName(@"Radius")]
        [TypeConverter(typeof(FloatConverter))]
        public float Radius
        {
            get { return ((SorterElementInfo)Info).CurveRadius; }
            set
            {
                if (value < 0 || value > 100)
                    return;
                ((SorterElementInfo)Info).CurveRadius = value;
                InvokeRefresh();
            }
        }


        [PropertyOrder(4)]
        [Category("Size")]
        [Description("Height difference")]
        [DisplayName(@"Height Difference")]
        [TypeConverter(typeof(FloatConverter))]
        public float HeightDifference
        {
            get { return ((SorterElementInfo)Info).HeightDifference; }
            set
            {
                ((SorterElementInfo)Info).HeightDifference = value;
                InvokeRefresh();
            }
        }

        public override float Roll
        {
            get
            {
                return base.Roll;
            }
            set
            {

            }
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
                curvesorter.Visible = value;
            }
        }

        internal override Matrix OrientationElement
        {
            get
            {
                return curvesorter.Orientation;
            }
        }

        [Browsable(true)]
        public override Color Color
        {
            get
            {
                return base.Color;
            }
            set
            {
                base.Color = value;
                curvesorter.Color = value;
            }
        }
        public override void Refresh()
        {
            UnSnap();

            if (curvesorter != null)
            {
                RemovePart(curvesorter);
                curvesorter.Dispose();
            }

            curvesorter = new CurveArea(0, ((SorterElementInfo) Info).SorterWidth,
                ((SorterElementInfo) Info).CurveRadius, ((SorterElementInfo) Info).CurveDegrees,
                ((SorterElementInfo) Info).Revolution)
            {
                Color = Info.color,
                RenderOption = RigidPart.RenderingMode.PrimitiveAndNormal,
                HeightDifference = ((SorterElementInfo) Info).HeightDifference
            };
            Add(curvesorter);
            if (((SorterElementInfo)Info).Revolution == Core.Environment.Revolution.Clockwise)
            {
                curvesorter.LocalYaw = (float)Math.PI / 2 - ((SorterElementInfo)Info).CurveDegrees / 180 * (float)Math.PI;
                Vector3 dir = new Vector3(1, 0, 0);
                dir.TransformCoordinate(Matrix.RotationY(-((SorterElementInfo)Info).CurveDegrees / 180 * (float)Math.PI));
                StartFixPoint.LocalYaw = -((SorterElementInfo)Info).CurveDegrees / 180 * (float)Math.PI - (float)Math.PI / 2;
                StartFixPoint.LocalPosition = dir * ((SorterElementInfo)Info).CurveRadius;
                EndFixPoint.LocalYaw = -(float)Math.PI / 2;
                EndFixPoint.LocalPosition = new Vector3(((SorterElementInfo)Info).CurveRadius, curvesorter.HeightDifference, 0);
            }
            else
            {
                curvesorter.LocalYaw = ((SorterElementInfo)Info).CurveDegrees / 180 * (float)Math.PI - (float)Math.PI / 2;
                Vector3 dir = new Vector3(-1, 0, 0);
                dir.TransformCoordinate(Matrix.RotationY(((SorterElementInfo)Info).CurveDegrees / 180 * (float)Math.PI));
                StartFixPoint.LocalYaw = ((SorterElementInfo)Info).CurveDegrees / 180 * (float)Math.PI - (float)Math.PI / 2;
                StartFixPoint.LocalPosition = dir * ((SorterElementInfo)Info).CurveRadius;
                EndFixPoint.LocalYaw = -(float)Math.PI / 2;
                EndFixPoint.LocalPosition = new Vector3(-((SorterElementInfo)Info).CurveRadius, curvesorter.HeightDifference, 0);
            }

            base.Length = 2 * (float)Math.PI * ((SorterElementInfo)Info).CurveRadius * ((SorterElementInfo)Info).CurveDegrees / 360;
        }

        protected override void ConfigureFixPoint(SorterElementFixPoint c)
        {
        }

    }
}