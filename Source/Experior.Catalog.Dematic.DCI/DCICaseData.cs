using Dematic.DCI;
using Experior.Core.Properties;
using Experior.Dematic.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace Experior.Catalog.Dematic.DCI.Assemblies
{
    public class DCICaseData : BaseCaseData
    {
        public DCICaseData()
        {
        }

        private string _TUIdent;
        [Category("DCI")]
        [DisplayName("TU Ident")]
        [ReadOnly(false)]
        [Experior.Core.Properties.AlwaysEditable]
        [PropertyOrder(1)]
        public string TUIdent
        {
            get { return _TUIdent; }
            set { _TUIdent = value; }
        }

        private string _TUType;
        [Category("DCI")]
        [DisplayName("TU Type")]
        [ReadOnly(false)]
        [Experior.Core.Properties.AlwaysEditable]
        [PropertyOrder(2)]
        public string TUType
        {
            get { return _TUType; }
            set { _TUType = value; }
        }

        private string _Source;
        [Category("DCI")]
        [DisplayName("Source")]
        [ReadOnly(false)]
        [Experior.Core.Properties.AlwaysEditable]
        [PropertyOrder(3)]
        public string Source
        {
            get { return _Source; }
            set { _Source = value; }
        }

        private string _Current;
        [Category("DCI")]
        [DisplayName("Current")]
        [ReadOnly(false)]
        [Experior.Core.Properties.AlwaysEditable]
        [PropertyOrder(4)]
        public string Current
        {
            get { return _Current; }
            set { _Current = value; }
        }

        private string _Destination;
        [Category("DCI")]
        [DisplayName("Destination")]
        [ReadOnly(false)]
        [Experior.Core.Properties.AlwaysEditable]
        [PropertyOrder(5)]
        public string Destination
        {
            get { return _Destination; }
            set { _Destination = value; }
        }

        private string _EventCode = "OK";
        [Category("DCI")]
        [DisplayName("EventCode")]
        [ReadOnly(false)]
        [Experior.Core.Properties.AlwaysEditable]
        [PropertyOrder(6)]
        public string EventCode
        {
            get { return _EventCode; }
            set { _EventCode = value; }
        }

        //private float _CaseWeight;
        //[Category("Size")]
        //[DisplayName("Weight")]
        //[PropertyOrder(10)]
        //public float CaseWeight
        //{
        //    get { return _CaseWeight; }
        //    set { _CaseWeight = value; }
        //}

        private int _SortID;
        [Category("Sortation")]
        [DisplayName("Sort ID")]
        [PropertyOrder(1)]
        public int SortID
        {
            get { return _SortID; }
            set { _SortID = value; }

        }

        private int _SortSequence;
        [Category("Sortation")]
        [DisplayName("Sort Sequence")]
        [PropertyOrder(2)]
        public int SortSequence
        {
            get { return _SortSequence; }
            set { _SortSequence = value; }
        }

        private string _SortInfo;
        [Category("Sortation")]
        [DisplayName("Sort Info")]
        [PropertyOrder(3)]
        public string SortInfo
        {
            get { return _SortInfo; }
            set { _SortInfo = value; }
        }

        private int _DropIndex = 0;
        [Category("Multishuttle")]
        [DisplayName("Drop Index")]
        public int DropIndex
        {
            get { return _DropIndex; }
            set { _DropIndex = value; }
        }

        private string _ShuttleDynamics = "????";
        [Category("Multishuttle")]
        [DisplayName("Shuttle Dynamics")]
        public string ShuttleDynamics
        {
            get { return _ShuttleDynamics; }
            set { _ShuttleDynamics = value; }
        }

        private string _LiftDynamics = "????";
        [Category("Multishuttle")]
        [DisplayName("Lift Dynamics")]
        public string LiftDynamics
        {
            get { return _LiftDynamics; }
            set { _LiftDynamics = value; }
        }

        private string _SourceShuttleExtension = "????";
        [Category("Multishuttle")]
        [DisplayName("Source Shuttle Extension")]
        public string SourceShuttleExtension
        {
            get { return _SourceShuttleExtension; }
            set { _SourceShuttleExtension = value; }
        }

        private string _DestinationShuttleExtension = "????";
        [Category("Multishuttle")]
        [DisplayName("Destination Shuttle Extension")]
        public string DestinationShuttleExtension
        {
            get { return _DestinationShuttleExtension; }
            set { _DestinationShuttleExtension = value; }
        }

        private string _CaseConveyorDynamics = "????";
        [Category("Multishuttle")]
        [DisplayName("Case Conveyor Dynamics")]
        public string CaseConveyorDynamics
        {
            get { return _CaseConveyorDynamics; }
            set { _CaseConveyorDynamics = value; }
        }
    }
}
