using System.ComponentModel;
using Experior.Catalog.Dematic.Case.Components;
using Experior.Core;
using Experior.Core.Parts;
using Experior.Core.Properties.TypeConverter;
using Experior.Core.Routes;
using Experior.Dematic.Base.Devices;
using Experior.Dematic.Base;

namespace Experior.Catalog.Dematic.Sorter.Assemblies.Chute
{
    public sealed class SorterChute : StraightConveyor, IChute
    {
        private readonly ActionPoint chutePoint;
        private SorterElementFixPoint sorterElementFixPoint;
        private readonly Timer readyTimer;
        private readonly SorterChuteInfo settings;

        public SorterChute(SorterChuteInfo info)
            : base(info)
        {
            settings = info;
            EndFixPoint.OnSnapped += EndFixPoint_Snapped;
            StartFixPoint.OnSnapped += StartFixPoint_Snapped;
            StartFixPoint.OnUnSnapped += StartFixPoint_UnSnapped;

            chutePoint = TransportSection.Route.InsertActionPoint(0);
            chutePoint.OnEnter += chutePoint_OnEnter;
            readyTimer = new Timer(1);
            readyTimer.OnElapsed += readyTimer_OnElapsed;
        }

        void readyTimer_OnElapsed(Timer sender)
        {
            RouteAvailable = RouteStatuses.Available;
        }

        void chutePoint_OnEnter(ActionPoint sender, Core.Loads.Load load)
        {
            //A load has been discharged to this chute
            if (RecoverTime > 0)
            {
                RouteAvailable = RouteStatuses.Blocked;
                readyTimer.Stop();
                readyTimer.Timeout = RecoverTime;
                readyTimer.Start();
            }
        }

        [Category("Status")]
        [DisplayName(@"Chute Ready")]
        public bool Ready
        {
            get { return RouteAvailable == RouteStatuses.Available && this.Enabled; }
        }

        public override string Category
        {
            get
            {
                return "Sorter chute";
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

        [Category("Sorter fixpoint")]
        [Description("Type of sorter fixpoint")]
        [DisplayName(@"Dump Chute")]
        public bool SorterFixPointDumpChute
        {
            get
            {
                if (sorterElementFixPoint != null)
                    return sorterElementFixPoint.DumpChute;

                return false;
            }
            set
            {
                if (sorterElementFixPoint != null)
                    sorterElementFixPoint.DumpChute = value;
            }
        }

        [Category("Configuration")]
        [Description("The time the chute is occupied after a discharge")]
        [DisplayName(@"Time to recover")]
        [TypeConverter(typeof(TimeConverterSeconds))]
        public float RecoverTime
        {
            get { return settings.RecoverTime; }
            set { settings.RecoverTime = value; }
        }

        public override void Reset()
        {
            RouteAvailable = RouteStatuses.Available;
            base.Reset();
        }

        public override void Dispose()
        {
            if (sorterElementFixPoint != null)
            {
                sorterElementFixPoint.ChutePoint = null;
                sorterElementFixPoint = null;
            }

            EndFixPoint.OnSnapped -= EndFixPoint_Snapped;
            StartFixPoint.OnSnapped -= StartFixPoint_Snapped;
            StartFixPoint.OnUnSnapped -= StartFixPoint_UnSnapped;

            chutePoint.OnEnter -= chutePoint_OnEnter;
            chutePoint.Dispose();
                  
            readyTimer.OnElapsed -= readyTimer_OnElapsed;
            readyTimer.Dispose();

            base.Dispose();
        }

        private void EndFixPoint_Snapped(FixPoint fixpoint, FixPoint.SnapEventArgs e)
        {
            if (fixpoint.UserData is SorterElementFixPoint)
            {
                e.Cancel = true;
            }
        }

        private void StartFixPoint_Snapped(FixPoint fixpoint, FixPoint.SnapEventArgs e)
        {
            if (sorterElementFixPoint != null)
            {
                sorterElementFixPoint.ChutePoint = null;
                sorterElementFixPoint = null;
            }

            if (fixpoint.UserData is SorterElementFixPoint)
            {
                sorterElementFixPoint = (SorterElementFixPoint)fixpoint.UserData;
                sorterElementFixPoint.ChutePoint = chutePoint;
                return;
            }

            e.Cancel = true;
        }

        private void StartFixPoint_UnSnapped(FixPoint fixpoint)
        {
            if (sorterElementFixPoint != null)
            {
                sorterElementFixPoint.ChutePoint = null;
                sorterElementFixPoint = null;
            }
        }

    }
}