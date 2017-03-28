using System.ComponentModel;
using System.Drawing;
using Experior.Catalog.Dematic.Case.Components;
using Experior.Core;
using Experior.Core.Parts;
using Experior.Core.Properties.TypeConverter;
using Experior.Core.Routes;
using Experior.Core.Loads;

namespace Experior.Catalog.Dematic.Sorter.Assemblies.Induction
{
    public sealed class SorterInduction : StraightConveyor
    {
        #region Fields

        private readonly ActionPoint inductionPoint;
        private SorterElementFixPoint sorterElementFixPoint;
        private Load currentLoad;
        #endregion

        #region Constructors

        public SorterInduction(SorterInductionInfo info)
            : base(info)
        {
            EndFixPoint.OnSnapped += EndFixPoint_Snapped;
            EndFixPoint.OnUnSnapped += EndFixPoint_UnSnapped;
            StartFixPoint.OnSnapped += StartFixPoint_Snapped;

            if (info.InductionDisctance > Length)
                info.InductionDisctance = Length / 2;

            inductionPoint = TransportSection.Route.InsertActionPoint(info.InductionDisctance);
            inductionPoint.Edge = ActionPoint.Edges.Leading;
        }

        #endregion

        #region Properties

        public override string Category
        {
            get
            {
                return "Sorter induction";
            }
        }

        [Browsable(false)]
        public ActionPoint InductionPoint
        {
            get { return inductionPoint; }
        }

        [Browsable(false)]
        public Load CurrentLoad
        {
            get { return currentLoad; }
            set { currentLoad = value; }
        }

        
        [DisplayName(@"Distance")]
        [Description("Select the distance in mm for the induction point (Communication point with controller)")]
        [TypeConverter(typeof(FloatConverter))]
        public float InductionPointDistance
        {
            get { return ((SorterInductionInfo)Info).InductionDisctance; }
            set
            {
                if (value > Length || value < 0)
                {
                    Environment.Log.Write("Not a valid value!", Color.Red);
                    return;
                }

                ((SorterInductionInfo)Info).InductionDisctance = value;
                inductionPoint.Distance = value;
            }
        }

        [Category("Sorter fixpoint")]
        [Description("Name of sorter fixpoint")]
        [DisplayName(@"Name")]
        public string SorterFixPointName
        {
            get
            {
                if (sorterElementFixPoint != null)
                    return sorterElementFixPoint.Name;

                return string.Empty;
            }
            set
            {
                if (sorterElementFixPoint != null)
                    sorterElementFixPoint.Name = value;
            }
        }

        public override float Length
        {
            get
            {
                return base.Length;
            }
            set
            {
                base.Length = value;
                if (((SorterInductionInfo)Info).InductionDisctance > value)
                {
                    ((SorterInductionInfo)Info).InductionDisctance = value;
                }
            }
        }

        [Browsable(false)]
        public SorterElementFixPoint SorterElementFixPoint
        {
            get { return sorterElementFixPoint; }
        }

        #endregion

        #region Methods

        public override void Dispose()
        {
            EndFixPoint.OnSnapped -= EndFixPoint_Snapped;
            EndFixPoint.OnUnSnapped -= EndFixPoint_UnSnapped;
            StartFixPoint.OnSnapped -= StartFixPoint_Snapped;
            if (sorterElementFixPoint != null)
            {
                sorterElementFixPoint.InductionPoint = null;
                sorterElementFixPoint = null;
            }
            base.Dispose();
        }

        private void EndFixPoint_Snapped(FixPoint fixpoint, FixPoint.SnapEventArgs e)
        {
            if (fixpoint.UserData is SorterElementFixPoint)
            {
                sorterElementFixPoint = (SorterElementFixPoint)fixpoint.UserData;
                sorterElementFixPoint.InductionPoint = inductionPoint;
                return;
            }

            e.Cancel = true;
        }

        private void EndFixPoint_UnSnapped(FixPoint fixpoint)
        {
            if (sorterElementFixPoint != null)
            {
                sorterElementFixPoint.InductionPoint = null;
                sorterElementFixPoint = null;
            }
        }

        private void StartFixPoint_Snapped(FixPoint fixpoint, FixPoint.SnapEventArgs e)
        {
            if (fixpoint.UserData is SorterElementFixPoint)
            {
                e.Cancel = true;
            }
        }

        #endregion
    }
}