using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;

namespace Experior.Dematic.Base
{

    public class Case_Load : Experior.Core.Loads.Mesh
    {
        public static List<Case_Load> AllCases = new List<Case_Load>();
        public const string GraphicsMesh = "Plastic Box"; // "Warehouse Bin";

        //If the load is stopped by the model and is waiting for a message from the WCS this must be set, 
        //load can only be released by a message if this is set and can only be released if this is clear
        //If the load is stopped by the PLC control and is waiting for the conveyor in front to clear then 
        //load can only be released when the conveyor in front is cleared.
        public bool FlowControl = false;

        public static Case_Load GetCaseFromIdentification(string Identification)
        {
            return AllCases.Find(c => c.Identification == Identification);
        }

        public static Case_Load GetCaseFromSSCCBarcode(string SSCCBarcode)
        {
            return AllCases.Find(c => c.SSCCBarcode.Trim() == SSCCBarcode.Trim());
        }

        private static bool SentCaseCaseDataMessage = false; //Only want to send the case data warning message once otherwise it will be annoying
        public static BaseCaseData GetCaseControllerCaseData()
        {
            BaseCaseData caseData = new BaseCaseData();
            ICaseController controller = Core.Assemblies.Assembly.Items.Values.ToList().FirstOrDefault(x => x is ICaseController) as ICaseController;

            if (controller != null)
            {
                caseData = controller.GetCaseData();
            }
            else if (!SentCaseCaseDataMessage)
            {
                SentCaseCaseDataMessage = true;
                Log.Write("Did not find valid controller on model to create specific CaseData, add valid case controller to model");
            }
            return caseData;
        }

        private static bool SentCaseMSDataMessage = false; //Only want to send the case data warning message once otherwise it will be annoying
        public static BaseCaseData GetMultiShuttleControllerCaseData()
        {
            BaseCaseData caseData = new BaseCaseData();

            IMultiShuttleController controller = Core.Assemblies.Assembly.Items.Values.ToList().FirstOrDefault(x => x is IMultiShuttleController) as IMultiShuttleController;
            if (controller != null)
            {
                caseData = controller.GetCaseData();
            }
            else if (!SentCaseMSDataMessage)
            {
                SentCaseMSDataMessage = true;
                Log.Write("Did not find valid controller on model to create specific CaseData, add valid multishuttle controller to model");
            }
            return caseData;
        }

        //private string sSCCBarcode;
       // public static List<Case_Load> AllCases = new List<Case_Load>();
        public delegate void CaseLoadDisposedEvent(Case_Load caseload);
        public static event CaseLoadDisposedEvent CaseLoadDisposed;
        public BaseCaseData Case_Data = new BaseCaseData();
        private static long ulid = 4000000;

        public Case_Load(Experior.Core.Loads.MeshInfo info): base(info)
        {
            //case_Data.Parent = this;
            SSCCBarcode = string.Empty;
            SSCCBarcode = UniqueULID.ToString();
            AllCases.Add(this);
            Movable = false; // CN : Added to prevent mousewheel rotating the load
        }

        public static long UniqueULID
        {
            get { return ++Case_Load.ulid; }
        }

        //[Browsable(false)]
        //public BaseCaseData Case_Data
        //{
        //    get { return case_Data; }
        //    set { case_Data = value; }
        //}

        public virtual void ReleaseLoad()
        {
            if (!LoadWaitingForWCS && !LoadWaitingForPLC)
            {
                Release();
            }
        }

        public virtual void StopLoad()
        {
            Stop();
        }

        public virtual void StopLoad_WCSControl()
        {
            LoadWaitingForWCS = true;
            Stop();
        }

        public virtual void ReleaseLoad_WCSControl()
        {
            LoadWaitingForWCS = false;
            if (!LoadWaitingForPLC)
            {
                Release();
            }
        }

        public virtual void StopLoad_PLCControl()
        {
            LoadWaitingForPLC = true;
            Stop();
        }

        public virtual void ReleaseLoad_PLCControl()
        {
            LoadWaitingForPLC = false;
            if (!LoadWaitingForWCS)
            {
                Release();
            }
        }

        /// <summary>
        /// This will release the load and clear all stoping references WCS and PLC
        /// </summary>
        public virtual void ReleaseLoadAndClear()
        {
            LoadWaitingForPLC = false;
            LoadWaitingForWCS = false;
            Release();
        }

        public override void Dispose()
        {
            if (AllCases.Remove(this))
            {
                if (CaseLoadDisposed != null)
                {
                    CaseLoadDisposed(this);
                }
            }
            base.Dispose();
        }

        [Browsable(false)]
        public bool Deleted { get; set; }

        [Category("Identification")]
        [DisplayName("SSCCBarcode")]
        [ReadOnly(false)]
        [Experior.Core.Properties.AlwaysEditable]
        public virtual string SSCCBarcode
        {
            get { return Identification; }
            set
            { 
                Identification = value;
            }
        }

        [Category("Size")]
        [DisplayName("Length")]
        public float CaseLength
        {
            get
            {
                return Info.length;
            }
        }

        [Category("Size")]
        [DisplayName("Width")]
        public float CaseWidth
        {
            get
            {
                return Info.width;
            }
        }

        [Category("Size")]
        [DisplayName("Height")]
        public float CaseHeight
        {
            get
            {
                return Info.height;
            }
        }

        [Category("Exception Data")]
        [DisplayName("Profile")]
        [Description("Change to report this value as an exception instead of the load value, project specific")]
        [Experior.Core.Properties.AlwaysEditable]
        public string ExceptionProfile { get; set; }

        [Category("Exception Data")]
        [DisplayName("Height")]
        [Description("Change to report this value as an exception instead of the load value, project specific")]
        [Experior.Core.Properties.AlwaysEditable]
        public string ExceptionHeight { get; set; }

        [Category("Exception Data")]
        [DisplayName("Weight")]
        [Description("Change to report this value as an exception instead of the load value, project specific")]
        [Experior.Core.Properties.AlwaysEditable]
        public string ExceptionWeight { get; set; }

        [Browsable(false)]
        protected override string MoveLoadTo
        {
            get
            {
                return base.MoveLoadTo;
            }
            set
            {
                base.MoveLoadTo = value;
            }
        }

        [Browsable(false)]
        public bool LoadWaitingForWCS { get; set; }

        [Browsable(false)]
        public bool LoadWaitingForPLC { get; set; }


    }
}