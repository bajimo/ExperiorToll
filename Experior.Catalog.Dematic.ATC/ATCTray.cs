using System;
using System.Collections.Generic;
using Experior.Dematic.Base;
using System.ComponentModel;
using Experior.Core.Properties;
using Dematic.ATC;
using Experior.Catalog.Dematic.Case;

namespace Experior.Catalog.Dematic.ATC
{
    public class ATCTray : Tray, IATCCaseLoadType
    {
        public ATCTray(TrayInfo info): base(info)
        { }

        //SSCC barcode is not used for ATC control as the TU Ident is used instead
        [Browsable(false)]
        public override string SSCCBarcode
        {
            get
            {
                return base.SSCCBarcode;
            }
            set
            {
                base.SSCCBarcode = value;
            }
        }

        //Not interesting for the user (reduce noise)
        [Browsable(false)]
        public override float Angle
        {
            get
            {
                return base.Angle;
            }
            set
            {
                base.Angle = value;
            }
        }

        [Browsable(false)]
        public override string Identification
        {
            get
            {
                return base.Identification;
            }
            set
            {
                base.Identification = value;
            }
        }

        private Dictionary<string, string> _ProjectFields = new Dictionary<string, string>();
        [Category("ATC")]
        [DisplayName("ProjectFields")]
        [PropertyOrder(0)]
        [Experior.Core.Properties.AlwaysEditable]
        public Dictionary<string,string> ProjectFields
        {
            get { return _ProjectFields; }
            set { _ProjectFields = value; }
        }

        private string _TUIdent;
        [Category("ATC")]
        [DisplayName("TU Ident")]
        [ReadOnly(false)]
        [Experior.Core.Properties.AlwaysEditable]
        [PropertyOrder(1)]
        public string TUIdent
        {
            get { return _TUIdent; }
            set 
            { 
                _TUIdent = value;
                Identification = value;
            }
        }

        private string _TUType;
        [Category("ATC")]
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
        [Category("ATC")]
        [DisplayName("Source")]
        [ReadOnly(false)]
        [Experior.Core.Properties.AlwaysEditable]
        [PropertyOrder(3)]
        public string Source
        {
            get { return _Source; }
            set { _Source = value; }
        }

        private string _Destination;
        [Category("ATC")]
        [DisplayName("Destination")]
        [ReadOnly(false)]
        [Experior.Core.Properties.AlwaysEditable]
        [PropertyOrder(4)]
        public string Destination
        {
            get { return _Destination; }
            set { _Destination = value; }
        }

        private string _PresetStateCode;
        [Category("ATC")]
        [DisplayName("PresetStateCode")]
        [ReadOnly(false)]
        [Experior.Core.Properties.AlwaysEditable]
        [PropertyOrder(5)]
        public string PresetStateCode
        {
            get { return _PresetStateCode; }
            set { _PresetStateCode = value; }
        }

        //private string _MTS;
        //[Category("ATC")]
        //[DisplayName("MTS")]
        //[ReadOnly(true)]
        //[PropertyOrder(6)]
        //public string MTS
        //{
        //    get { return _MTS; }
        //    set { _MTS = value; }
        //}
          
        private string _Location;
        [Category("ATC")]
        [DisplayName("Last Location")]
        [ReadOnly(true)]
        [PropertyOrder(7)]
        public string Location
        {
            get { return _Location; }
            set { _Location = value; }
        }

        //Weight on the load doesn't seem to work so have added this and controlled directly in ATC code when creating a load
        private float _CaseWeight;
        [Category("Size")]
        [DisplayName("Weight")]
        [PropertyOrder(10)]
        public float CaseWeight
        {
            get
            {
                return _CaseWeight;
            }
            set
            {
                _CaseWeight = value;
            }
        }

        private int _SortID;
        [Category("Sortation")]
        [DisplayName("Sort ID")]
        [PropertyOrder(1)]
        public int SortID
        {
            get
            {
                return _SortID;
            }
            set
            {
                _SortID = value;
            }
        }

        private int _SortSequence;
        [Category("Sortation")]
        [DisplayName("Sort Sequence")]
        [PropertyOrder(2)]
        public int SortSequence
        {
            get
            {
                return _SortSequence;
            }
            set
            {
                _SortSequence = value;
            }
        }

        private string _SortInfo;
        [Category("Sortation")]
        [DisplayName("Sort Info")]
        [PropertyOrder(3)]
        public string SortInfo
        {
            get
            {
                return _SortInfo;
            }
            set
            {
                _SortInfo = value;
            }
        }

        private int _DropIndex;
        [Category("Multishuttle")]
        [DisplayName("Drop Index")]
        public int DropIndex
        {
            get { return _DropIndex; }
            set { _DropIndex = value; }
        }

        public string GetPropertyValueFromEnum(TelegramFields field)
        {
            switch (field)
            {
                case TelegramFields.tuIdent:     return TUIdent;
                case TelegramFields.tuType:      return TUType;
                //case TelegramFields.mts:         return MTS;
                case TelegramFields.destination: return Destination;
                case TelegramFields.source:      return Source;
                case TelegramFields.location:    return Location;
                case TelegramFields.height:      return (Height * 1000).ToString();
                case TelegramFields.width:       return (Width * 1000).ToString();
                case TelegramFields.length:      return (Length * 1000).ToString();
                case TelegramFields.weight:      return (CaseWeight * 1000).ToString();
            }
            return null;
        }

        public override void DoubleClick()
        {
            if (ProjectFields.Count > 0 && ProjectFields.Count < 6)
            {
                ProjectFieldTools projectFields = new ATC.ProjectFieldTools(this);
                projectFields.ShowDialog();
            }
        }

        /// <summary>
        /// Set the Yaw of the load based on the width of the conveyor
        /// </summary>
        public void SetYaw(float convWidth, CaseOrientation caseOrientation)
        {
            if (caseOrientation == CaseOrientation.LengthLeading || (caseOrientation == CaseOrientation.Auto && Length > Width && Length > convWidth))
            {
                Yaw = 0;
            }
            else
            {
                Yaw = (float)(Math.PI / 2);
            }
        }
    }
}
