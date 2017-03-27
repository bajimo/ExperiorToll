using System.ComponentModel;
using System.Drawing;
using Experior.Core.Assemblies;
using Experior.Core.Parts;
using Experior.Core.Properties;
using Experior.Core.Properties.TypeConverter;
using Experior.Core.Routes;


namespace Experior.Dematic.Storage.Base
{
    public abstract class BaseTrack : Assembly
    {
        #region Fields

        private ActionPoint apUser     = null;
        private FixPoint endFixPoint   = null;
        private FixPoint startFixPoint = null;
        //private FixPoint leftFixPoint  = null;
        //private FixPoint rightFixPoint = null;

        private Experior.Core.TransportSections.ITransportSection transportsection = null;

        #endregion

        #region Constructors

        public BaseTrack(Experior.Core.Assemblies.Logistic.RouteInfo info)
            : base(info)
        {
            startFixPoint = new FixPoint(Color.Red, FixPoint.Types.Start, this);
            endFixPoint   = new FixPoint(Color.Blue, FixPoint.Types.End, this);
            //leftFixPoint  = new FixPoint(Color.Gray, FixPoint.Types.Left, this);
            //rightFixPoint = new FixPoint(Color.Gray, FixPoint.Types.Right, this);

            endFixPoint.OnSnapped   += new FixPoint.SnappedEvent(endFixPoint_Snapped);
            startFixPoint.OnSnapped += new FixPoint.SnappedEvent(startFixPoint_Snapped);
            
            //leftFixPoint.Snapped  += new FixPoint.SnappedEvent(leftFixPoint_Snapped);
            //rightFixPoint.Snapped += new FixPoint.SnappedEvent(rightFixPoint_Snapped);

            endFixPoint.OnUnSnapped   += new FixPoint.UnSnappedEvent(endFixPoint_UnSnapped);
            startFixPoint.OnUnSnapped += new FixPoint.UnSnappedEvent(startFixPoint_UnSnapped);
            //leftFixPoint.UnSnapped  += new FixPoint.UnSnappedEvent(leftFixPoint_UnSnapped);
            //rightFixPoint.UnSnapped += new FixPoint.UnSnappedEvent(rightFixPoint_UnSnapped);
        }

        public override void Dispose() {

            endFixPoint.OnSnapped -= new FixPoint.SnappedEvent(endFixPoint_Snapped);
            startFixPoint.OnSnapped -= new FixPoint.SnappedEvent(startFixPoint_Snapped);

            endFixPoint.OnUnSnapped -= new FixPoint.UnSnappedEvent(endFixPoint_UnSnapped);
            startFixPoint.OnUnSnapped -= new FixPoint.UnSnappedEvent(startFixPoint_UnSnapped);
            base.Dispose();
        }

        #endregion

        #region Properties

        public override string Category
        {
            get{            
                return string.Empty;
            }
        }

        public override bool Enabled
        {
            get{
                return Info.enable;
            }
            set{            
                base.Enabled = value;
                if (TransportSection != null)
                    TransportSection.Route.Motor.Enabled = value;
            }
        }

        public override Image Image
        {
            get{            
                return null;
            }
        }

        [PropertyOrder(0)]
        [DescriptionAttribute("Spacing")]
        [CategoryAttribute("Routing")]
        [TypeConverter(typeof(FloatConverter))]
        public virtual float Spacing
        {
            get{            
                return ((Experior.Core.Assemblies.Logistic.RouteInfo)Info).spacing;
            }
            set{            
                ((Experior.Core.Assemblies.Logistic.RouteInfo)Info).spacing = value;
                TransportSection.Route.Spacing = value;
            }
        }

        [PropertyOrder(2)]
        [DescriptionAttribute("Speed m/s")]
        [CategoryAttribute("Routing")]
        public virtual float Speed
        {
            get{            
                return ((Experior.Core.Assemblies.Logistic.RouteInfo)Info).speed;
            }
            set{            
                ((Experior.Core.Assemblies.Logistic.RouteInfo)Info).speed = value;
                TransportSection.Route.Motor.Speed = value;
            }
        }

        [Browsable(false)]
        public Experior.Core.TransportSections.ITransportSection TransportSection
        {
            get { return transportsection; }
            set{            
                transportsection = value;

                startFixPoint.Route = transportsection.Route;
                endFixPoint.Route = transportsection.Route;

                if (Experior.Core.Routes.ActionPoint.Items != null){                
                    foreach (ActionPoint ap in Experior.Core.Routes.ActionPoint.Items.Values){
                        if (ap.Assembly == this)
                            InsertActionPoint(ap);
                    }
                }

            }
        }

        [Browsable(false)]
        protected FixPoint EndFixPoint
        {
            get { return endFixPoint; }
        }
        
        [Browsable(false)]
        protected FixPoint StartFixPoint
        {
            get { return startFixPoint; }
        }

        //[Browsable(false)]
        //protected FixPoint LeftFixPoint {
        //    get {
        //        return leftFixPoint;
        //    }
        //}

        //[Browsable(false)]
        //protected FixPoint RightFixPoint {
        //    get {
        //        return rightFixPoint;
        //    }
        //}

        #endregion

        #region Methods

        public override void InsertActionPoint(ActionPoint ap){

            if(TransportSection == null) {
                return;
            }

            TransportSection.Route.InsertActionPoint(ap);
            ap.Routing = true;
            ap.Visible = true;
         
            this.apUser = ap;
        }

        private void endFixPoint_Snapped(FixPoint fixpoint, FixPoint.SnapEventArgs e){//fixpoint is the start of the conveyor that has been snapped to
            //Set routes up on conveyor
            if (fixpoint.Type == FixPoint.Types.Start)
                endFixPoint.Route.NextRoute = fixpoint.Route;

            //if(transportsection != null && ((BaseTrack)fixpoint.Parent).TransportSection.Route != null) {
            //    transportsection.Route.NextRoute = ((BaseTrack)fixpoint.Parent).TransportSection.Route;
            //}
        }

        private void startFixPoint_Snapped(FixPoint fixpoint, FixPoint.SnapEventArgs e) {//fixpoint is the start of the conveyor that has been snapped to
            //Set routes up on conveyor
            if(fixpoint.Type == FixPoint.Types.End)
                startFixPoint.Route.LastRoute = fixpoint.Route;
            
            //Set routes up on conveyor
            //if(transportsection != null && ((BaseTrack)fixpoint.Parent).TransportSection.Route != null) {
            //    transportsection.Route.LastRoute = ((BaseTrack)fixpoint.Parent).TransportSection.Route;
            //}

        }

        void endFixPoint_UnSnapped(FixPoint fixpoint) {
            if(TransportSection != null) {
                TransportSection.Route.NextRoute = null;
            }
        }

        private void startFixPoint_UnSnapped(FixPoint fixpoint) {
            if(TransportSection != null) {
                TransportSection.Route.LastRoute = null;
            }
        }
        
        //void rightFixPoint_UnSnapped(FixPoint fixpoint) {
        //    //throw new NotImplementedException();
        //}

        //void leftFixPoint_UnSnapped(FixPoint fixpoint) {
        //    //throw new NotImplementedException();
        //}

        void rightFixPoint_Snapped(FixPoint fixpoint, FixPoint.SnapEventArgs e) {

        }

        void leftFixPoint_Snapped(FixPoint fixpoint, FixPoint.SnapEventArgs e) {

        }

        #endregion
    }
}