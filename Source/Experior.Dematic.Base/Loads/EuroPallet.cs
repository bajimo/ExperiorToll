using Experior.Core.Loads;
using Experior.Core.Mathematics;
using Experior.Core.Properties;
using Microsoft.DirectX;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Xml.Serialization;

namespace Experior.Dematic.Base
{

    public class EuroPallet : Experior.Core.Loads.Mesh
    {
        public static List<EuroPallet> AllPallets = new List<EuroPallet>();
        public const string Mesh = "pallet";
        public EuroPalletInfo euroPalletInfo = new EuroPalletInfo();
        public bool FlowControl = false;
        public Load palletLoad;
        public delegate void PalletLoadDisposedEvent(EuroPallet Palletload);
        public static event PalletLoadDisposedEvent PalletLoadDisposed;
        public BasePalletData Pallet_Data = new BasePalletData();
        
        private static bool SentPalletPalletDataMessage = false; //Only want to send the Pallet data warning message once otherwise it will be annoying
        private string sSCCBarcode;
        private static long ulid = 4000000;
        private uint palletStacks = 12;
        private List<EuroPallet> stackedPallets = new List<EuroPallet>();

        public static EuroPallet GetPalletFromIdentification(string Identification)
        {
            return AllPallets.Find(c => c.Identification == Identification);
        }

        public static EuroPallet GetPalletFromSSCCBarcode(string SSCCBarcode)
        {
            return AllPallets.Find(c => c.SSCCBarcode.Trim() == SSCCBarcode.Trim());
        }

        public static BasePalletData GetPalletControllerPalletData()
        {
            BasePalletData PalletData = new BasePalletData();
            IPalletController controller = Core.Assemblies.Assembly.Items.Values.ToList().FirstOrDefault(x => x is IPalletController) as IPalletController;

            if (controller != null)
            {
                PalletData = controller.GetPalletData();
            }
            else if (!SentPalletPalletDataMessage)
            {
                SentPalletPalletDataMessage = true;
                Log.Write("Did not find valid controller on model to create specific PalletData, add valid Pallet controller to model");
            }
            return PalletData;
        }
        
        public EuroPallet(EuroPalletInfo info) : base(info)
        {
            euroPalletInfo = info;
            Color = Color.Peru;
            //Pallet_Data.Parent = this;
            SSCCBarcode = string.Empty;
            SSCCBarcode = UniqueULID.ToString();
            AllPallets.Add(this);
            Movable = false;
            if (!euroPalletInfo.InStack)
            {
                SetupPallet();
            }
        }

        public void SetupPallet()
        {
            ResetEuroPallet(true);

            switch (euroPalletInfo.Status)
            {
                case PalletStatus.Empty:
                    break;
                case PalletStatus.Loaded:
                    LoadPallet();
                    break;
                case PalletStatus.Stacked:
                    StackPallet();
                    break;
                default:
                    break;
            }
        }

        public void LoadPallet()
        {
            Core.Resources.Mesh mesh = Common.Meshes.Get("cube");
            palletLoad = Load.Create(mesh, LoadLength, LoadHeight - euroPalletInfo.height, LoadWidth);
            palletLoad.Color = LoadColor;
            palletLoad.UserDeletable = false;
            palletLoad.OnSelecting += palletLoad_OnSelecting;
            palletLoad.Yaw = Trigonometry.PI(Trigonometry.Angle2Rad(Angle));
            Group(palletLoad, new Vector3(0, (LoadHeight / 2) + 0.001f, 0));
        }

        private void StackPallet()
        {
            var createdloads = 1;
            var loadYaw = Trigonometry.PI(Trigonometry.Angle2Rad(Angle));

            IEmulationController controller = Core.Assemblies.Assembly.Items.Values.ToList().FirstOrDefault(x => x is IEmulationController) as IEmulationController;
            EuroPallet load;
            
            while (createdloads < palletStacks)
            {
                if (controller != null)
                {
                    load = controller.GetEuroPallet(null, 0, PalletStatus.Empty);
                }
                else
                {
                    EuroPalletInfo info = new EuroPalletInfo
                    {
                        length = Length,
                        height = Height,
                        width = Width,
                        color = Color.Peru,
                        InStack = true,
                        Status = PalletStatus.Empty,
                        filename = Mesh,
                    };
                    load = new EuroPallet(info);
                }
                //EuroPallet load = new EuroPallet(info);
                load.UserDeletable = false;
                load.Yaw = loadYaw;
                load.OnSelecting += palletLoad_OnSelecting;
                var stackY = (Height + 0.005f) * createdloads; // create empty space between loads     
                Group(load, new Vector3(0, stackY, 0));
                createdloads++;
                stackedPallets.Add(load);
            }
        }

        public void DetachStackedPallets()
        {
            if (stackedPallets.Count > 0)
            {
                UnGroup();
                foreach (var pallet in stackedPallets)
                {
                    pallet.OnSelecting -= palletLoad_OnSelecting;
                }
                stackedPallets.Clear();
            }
        }

        public void AttachStackedPallets(List<EuroPallet> pallets)
        {
            for (int i = 1; i < pallets.Count; i++)
            {
                var load = pallets[i];
                load.OnSelecting += palletLoad_OnSelecting;
                var stackY = (Height + 0.005f) * i; // create empty space between loads     
                Group(load, new Vector3(0, stackY, 0));
                stackedPallets.Add(load);
            }
        }

        public void ResetEuroPallet(bool ungroup)
        {
            if (ungroup)
            {
                UnGroup();
            }
            if (stackedPallets.Count > 0)
            {
                foreach (var pallet in stackedPallets)
                {
                    pallet.OnSelecting -= palletLoad_OnSelecting;
                    Delete(pallet);
                }
                stackedPallets.Clear();
            }
            if (palletLoad != null)
            {
                palletLoad.OnSelecting -= palletLoad_OnSelecting;
                Delete(palletLoad);
                palletLoad = null;
            }
        }

        private void palletLoad_OnSelecting(Load load)
        {
            Core.Environment.Properties.Set(this);
            load.DeSelect();
            this.Select();
        }

        public static long UniqueULID
        {
            get { return ++EuroPallet.ulid; }
        }

        //[Browsable(false)]
        //public BasePalletData Pallet_Data
        //{
        //    get { return Pallet_Data; }
        //    set { Pallet_Data = value; }
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

        public override void Dispose(bool user)
        {
            ResetEuroPallet(true);
            if (AllPallets.Contains(this))
            {
                if (PalletLoadDisposed != null)
                    PalletLoadDisposed(this);
                AllPallets.Remove(this);
            }
            base.Dispose();
        }

        public override void Dispose()
        {
            ResetEuroPallet(false);
            if (AllPallets.Contains(this))
            {
                if (PalletLoadDisposed != null)
                    PalletLoadDisposed(this);
                AllPallets.Remove(this);
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
            get { return sSCCBarcode; }
            set
            {
                sSCCBarcode = value;
            }
        }

        [Category("Pallet Configuration")]
        [DisplayName("Status")]
        [PropertyOrder(1)]
        public PalletStatus Status
        {
            get
            {
                return euroPalletInfo.Status;
            }
            set
            {
                euroPalletInfo.Status = value;
                Core.Environment.Properties.Refresh();
                SetupPallet();
            }
        }

        [Category("Pallet Configuration")]
        [DisplayName("Load Length")]
        [Description("Length of the load not the pallet")]
        [PropertyAttributesProvider("DynamicPropertyLoaded")]
        [PropertyOrder(2)]
        public float LoadLength
        {
            get
            {
                return euroPalletInfo.LoadLength;
            }
            set
            {
                euroPalletInfo.LoadLength = value;
                SetupPallet();
            }
        }

        [Category("Pallet Configuration")]
        [DisplayName("Load Width")]
        [Description("Width of the load not the pallet")]
        [PropertyAttributesProvider("DynamicPropertyLoaded")]
        [PropertyOrder(3)]
        public float LoadWidth
        {
            get
            {
                return euroPalletInfo.LoadWidth;
            }
            set
            {
                euroPalletInfo.LoadWidth = value;
                SetupPallet();
            }
        }

        [Category("Pallet Configuration")]
        [DisplayName("Load Height")]
        [Description("Height of the load not the pallet, includes the height of the pallet")]
        [PropertyAttributesProvider("DynamicPropertyLoaded")]
        [PropertyOrder(4)]
        public float LoadHeight
        {
            get
            {
                return euroPalletInfo.LoadHeight;
            }
            set
            {
                euroPalletInfo.LoadHeight = value;
                SetupPallet();
            }
        }


        [Category("Pallet Configuration")]
        [DisplayName("Load Color")]
        [PropertyAttributesProvider("DynamicPropertyLoaded")]
        [PropertyOrder(5)]
        public Color LoadColor
        {
            get
            {
                return euroPalletInfo.LoadColor;
            }
            set
            {
                euroPalletInfo.LoadColor = value;
                if (palletLoad != null)
                {
                    palletLoad.Color = value;
                }
            }
        }

        public void DynamicPropertyLoaded(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = Status == PalletStatus.Loaded;
        }

        [Category("Pallet Configuration")]
        [DisplayName("Stacks")]
        [PropertyAttributesProvider("DynamicPropertyStacked")]
        [PropertyOrder(2)]
        public uint PalletStacks
        {
            get
            {
                return palletStacks;
            }
            set
            {
                palletStacks = value;
                //Stacks = value;
                SetupPallet();
            }
        }

        public void DynamicPropertyStacked(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = Status == PalletStatus.Stacked;
        }

        [ReadOnly(true)]
        [Browsable(false)]
        public new Color Color
        {
            get { return base.Color; }
            set { base.Color = value; }
        }

        [ReadOnly(true)]
        [Browsable(false)]
        public new bool Transparent
        {
            get { return base.Transparent; }
            set { base.Transparent = value; }
        }
        
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

        [Browsable(true)]
        public bool LoadWaitingForWCS { get; set; }

        [Browsable(false)]
        public bool LoadWaitingForPLC { get; set; }

    }

    [Serializable]
    [XmlInclude(typeof(EuroPalletInfo))]
    public class EuroPalletInfo : MeshInfo
    {
        public PalletStatus Status;
        public bool InStack; // Is this load within a stack
        public float LoadHeight;
        public float LoadLength;
        public float LoadWidth;
        public Color LoadColor;
    }

}