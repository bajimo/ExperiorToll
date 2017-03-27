using System;
using System.Collections.Generic;
using System.Drawing;
using System.ComponentModel;
using Experior.Core.Loads;
using System.Xml.Serialization;
using Microsoft.DirectX;
using Experior.Core.Properties;
using Experior.Core.Mathematics;
using System.Linq;

namespace Experior.Dematic.Base
{

    public class Tray : Case_Load
    {
        public static new List<Tray> AllCases = new List<Tray>();
        public const string Mesh = "Plastic Box";

        private TrayInfo trayInfo;
        private List<Tray> stackedTrays = new List<Tray>();
        public Load trayLoad;

        public Tray(TrayInfo info): base(info)
        {
            trayInfo = info;
            //case_Data.Parent = this;
            SSCCBarcode = string.Empty;
            SSCCBarcode = UniqueULID.ToString();
            AllCases.Add(this);
            Movable = false; // CN : Added to prevent mousewheel rotating the load
            if (!trayInfo.InStack)
            {
                SetupTray();
            }
        }

        public void SetupTray()
        {
            ResetTray(true);

            switch (trayInfo.Status)
            {
                case TrayStatus.Empty:
                    break;
                case TrayStatus.Loaded:
                    LoadTray();
                    break;
                case TrayStatus.Stacked:
                    StackTray();
                    break;
                default:
                    break;
            }
        }

        private void LoadTray()
        {
            Core.Resources.Mesh mesh = Common.Meshes.Get("cube");
            var loadLength = LoadLength;
            if (loadLength - trayInfo.length <= 0)
            {
                loadLength = loadLength - 0.01f;
            }
            var loadWidth = LoadWidth;
            if (loadWidth - trayInfo.width <= 0)
            {
                loadWidth = loadWidth - 0.01f;
            }
            trayLoad = Load.Create(mesh, loadLength, LoadHeight, loadWidth);
            trayLoad.Color = LoadColor;
            trayLoad.Deletable = false;
            //trayLoad.Selectable = false;
            trayLoad.OnSelecting += trayLoad_OnSelecting;
            Group(trayLoad, new Vector3(0, (LoadHeight / 2) - Height + 0.01f, 0));
        }

        private void StackTray()
        {
            var createdloads = 1;
            var loadYaw = Trigonometry.PI(Trigonometry.Angle2Rad(Angle));
            
            IEmulationController controller = Core.Assemblies.Assembly.Items.Values.ToList().FirstOrDefault(x => x is IEmulationController) as IEmulationController;
            Tray load;

            while (createdloads < trayInfo.TrayStacks)
            {
                if (controller != null)
                {
                    load = controller.GetTray(null, 0, TrayStatus.Empty);
                }
                else
                {
                    TrayInfo info = new TrayInfo
                    {
                        length = Length,
                        height = Height,
                        width = Width,
                        color = Color,
                        InStack = true,
                        Status = TrayStatus.Empty,
                        filename = Mesh,
                        TrayStacks = 1,
                    };
                    load = new Tray(info);
                }

                load.Deletable = false;
                load.Yaw = loadYaw;
                load.OnSelecting += trayLoad_OnSelecting;
                var stackY = (Height + 0.005f) * createdloads; // create empty space between loads     
                Group(load, new Vector3(0, stackY, 0));
                createdloads++;
                stackedTrays.Add(load);
            }
        }

        public void DetachStackedTrays()
        {
            if (stackedTrays.Count > 0)
            {
                UnGroup();
                foreach (var tray in stackedTrays)
                {
                    tray.OnSelecting -= trayLoad_OnSelecting;
                }
                stackedTrays.Clear();
            }
        }

        public void AttachStackedTrays(List<Tray> trays)
        {
            for (int i = 1; i < trays.Count; i++)
            {
                var load = trays[i];
                load.OnSelecting += trayLoad_OnSelecting;
                var stackY = (Height + 0.005f) * i; // create empty space between loads     
                Group(load, new Vector3(0, stackY, 0));
                stackedTrays.Add(load);
            }
        }

        private void ResetTray(bool ungroup)
        {
            if (ungroup)
            {
                UnGroup();
            }
            if (stackedTrays.Count > 0)
            {
                foreach (var tray in stackedTrays)
                {
                    tray.OnSelecting -= trayLoad_OnSelecting;
                    Delete(tray);
                }
                stackedTrays.Clear();
            }
            if (trayLoad != null)
            {
                trayLoad.Deletable = true;
                trayLoad.OnSelecting -= trayLoad_OnSelecting;
                Delete(trayLoad);
            }
        }

        private void trayLoad_OnSelecting(Load load)
        {
            Core.Environment.Properties.Set(this);
            load.DeSelect();
            this.Select();
        }


        [Category("Tray Configuration")]
        [DisplayName("Status")]
        [PropertyOrder(1)]
        public TrayStatus Status
        {
            get
            {
                return trayInfo.Status;
            }
            set
            {
                trayInfo.Status = value;
                Core.Environment.Properties.Refresh();
                SetupTray();
            }
        }

        [Category("Tray Configuration")]
        [DisplayName("Load Length")]
        [Description("Length of the load not the tray")]
        [PropertyAttributesProvider("DynamicPropertyLoaded")]
        [PropertyOrder(2)]
        public float LoadLength
        {
            get
            {
                return trayInfo.LoadLength;
            }
            set
            {
                trayInfo.LoadLength = value;
                SetupTray();
            }
        }

        [Category("Tray Configuration")]
        [DisplayName("Load Width")]
        [Description("Width of the load not the tray")]
        [PropertyAttributesProvider("DynamicPropertyLoaded")]
        [PropertyOrder(3)]
        public float LoadWidth
        {
            get
            {
                return trayInfo.LoadWidth;
            }
            set
            {
                trayInfo.LoadWidth = value;
                SetupTray();
            }
        }

        [Category("Tray Configuration")]
        [DisplayName("Load Height")]
        [Description("Height of the load not the tray, includes the height of the tray")]
        [PropertyAttributesProvider("DynamicPropertyLoaded")]
        [PropertyOrder(4)]
        public float LoadHeight
        {
            get
            {
                return trayInfo.LoadHeight;
            }
            set
            {
                trayInfo.LoadHeight = value;
                SetupTray();
            }
        }


        [Category("Tray Configuration")]
        [DisplayName("Load Color")]
        [PropertyAttributesProvider("DynamicPropertyLoaded")]
        [PropertyOrder(5)]
        public Color LoadColor
        {
            get
            {
                return trayInfo.LoadColor;
            }
            set
            {
                trayInfo.LoadColor = value;
                trayLoad.Color = value;
            }
        }

        public void DynamicPropertyLoaded(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = Status == TrayStatus.Loaded;
        }

        [Category("Tray Configuration")]
        [DisplayName("Stacks")]
        [PropertyAttributesProvider("DynamicPropertyStacked")]
        [PropertyOrder(2)]
        public uint TrayStacks
        {
            get
            {
                return trayInfo.TrayStacks;
            }
            set
            {
                trayInfo.TrayStacks = value;
                SetupTray();
            }
        }

        public void DynamicPropertyStacked(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = Status == TrayStatus.Stacked;
        }

        [ReadOnly(true)]
        [Browsable(false)]
        public new Color Color
        {
            get { return base.Color; }
            set { base.Color = value; }
        }
    }

    [Serializable]
    [XmlInclude(typeof(TrayInfo))]
    public class TrayInfo : MeshInfo
    {
        public TrayStatus Status;
        public bool InStack; // Is this load within a stack
        public float LoadHeight;
        public float LoadLength;
        public float LoadWidth;
        public Color LoadColor;
        public uint TrayStacks;
    }
}