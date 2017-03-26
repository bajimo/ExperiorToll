using System;
using System.ComponentModel;
using System.Drawing;
using Experior.Core.Mathematics;
using Experior.Core.Parts;
using Experior.Core.Properties;
using Microsoft.DirectX;

namespace Experior.Catalog.Dematic.Sorter.Assemblies
{
    public sealed class SorterElementStraight : SorterElement
    {
        #region Fields

        private Cube straightsorter;

        #endregion

        #region Constructors

        public SorterElementStraight(SorterElementInfo info)
            : base(info)
        {
            Refresh();
        }

        #endregion

        #region Properties

        public override string Category
        {
            get
            {
                return "Sorter Straight";
            }
        }

        public override Image Image
        {
            get
            {
                return Common.Icons.Get(Category);
            }
        }

        [TypeConverter(typeof(Core.Properties.TypeConverter.FloatConverter))]
        public override float Length
        {
            get
            {
                return base.Length;
            }
            set
            {
                if (value > 0)
                {
                    base.Length = value;
                    Refresh();
                }
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
                straightsorter.Visible = value;
            }
        }

        internal override Matrix OrientationElement
        {
            get { return straightsorter.Orientation; }
        }

        #endregion

        #region Methods

        protected override void ConfigureFixPoint(SorterElementFixPoint c)
        {
            c.Fixpoint.LocalPosition = new Vector3(((SorterElementInfo)Info).SorterElementLength / 2 - c.Distance, 0, 0) + c.LocalOffset;
            c.Fixpoint.LocalYaw = c.LocalYaw / 180f * (float)Math.PI;
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
                straightsorter.Color = value;
            }
        }


        /// <summary>
        /// Gets or sets the position end.
        /// </summary>
        /// <value>The position end.</value>
        [PropertyOrder(1)]
        [DisplayName(@"End")]
        [TypeConverter(typeof(Core.Properties.TypeConverter.Vector3Converter))]
        [Category("Position")]
        [Description("End Position in mm")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public Vector3 PositionEnd
        {
            get { return EndFixPoint.Position; }
            set
            {
                Info.yaw = Trigonometry.Yaw(StartFixPoint.Position, value);
                Info.roll = Trigonometry.Roll(StartFixPoint.Position, value);
                Info.position = Trigonometry.Center(StartFixPoint.Position, value);
                Info.orientation = Matrix.RotationYawPitchRoll(Info.yaw, 0, Info.roll);
                ((SorterElementInfo)Info).SorterElementLength = Trigonometry.Length(StartFixPoint.Position, value);
                InvokeRefresh();
            }
        }

        public override void Refresh()
        {
            base.Refresh();

            if (straightsorter != null)
            {
                RemovePart(straightsorter);
                straightsorter.Dispose();
            }

            straightsorter = new Cube(Info.color, ((SorterElementInfo)Info).SorterElementLength, 0, ((SorterElementInfo)Info).SorterWidth);
            straightsorter.RenderOption = RigidPart.RenderingMode.PrimitiveAndNormal;
            Add(straightsorter, new Vector3(0, 0, 0));
            StartFixPoint.LocalPosition = new Vector3(((SorterElementInfo)Info).SorterElementLength / 2, 0, 0);
            EndFixPoint.LocalPosition = new Vector3(-((SorterElementInfo)Info).SorterElementLength / 2, 0, 0);

            Orientation = Info.orientation;
            Position = Info.position;

        }

        /// <summary>
        /// Gets or sets the position start.
        /// </summary>
        /// <value>The position start.</value>
        [PropertyOrder(0)]
        [DisplayName(@"Start")]
        [TypeConverter(typeof(Core.Properties.TypeConverter.Vector3Converter))]
        [Category("Position")]
        [Description("Start Position in mm")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public Vector3 PositionStart
        {
            get { return StartFixPoint.Position; }
            set
            {
                Info.yaw = Trigonometry.Yaw(value, EndFixPoint.Position);
                Info.roll = Trigonometry.Roll(value, EndFixPoint.Position);
                Info.position = Trigonometry.Center(value, EndFixPoint.Position);
                Info.orientation = Matrix.RotationYawPitchRoll(Info.yaw, 0, Info.roll);
                ((SorterElementInfo)Info).SorterElementLength = Trigonometry.Length(value, EndFixPoint.Position);
                InvokeRefresh();
            }
        }

        #endregion
    }
}