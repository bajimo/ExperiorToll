using Experior.Catalog.Logistic.Track;
using Experior.Core.Assemblies;
using Microsoft.DirectX;
using System;
using System.ComponentModel;
using System.Linq;

namespace Experior.Dematic.Base.Devices
{
    public abstract class Device : Assembly
    {
        private DeviceInfo deviceInfo;
        public virtual event EventHandler OnDeviceDeleted;
        protected IConstructDevice parent;
        
        public BaseTrack conveyor;

        public Device(DeviceInfo info, BaseTrack conv): base(info)
        {
            deviceInfo = info;
            conveyor = conv;

            SectionName = conv.SectionName;
            OnSectionChanged += Device_OnSectionChanged;

            if (conv is IConstructDevice)
            {
                parent = (IConstructDevice)conv;
                ((IConstructDevice)conv).OnSizeUpdated += Device_OnSizeUpdated;
            }
        }

        private void Device_OnSectionChanged(Assembly assembly, Core.Section oldsection)
        {
            SectionName = assembly.SectionName;
        }

        public abstract void Device_OnSizeUpdated(object sender, SizeUpdateEventArgs e);

        public virtual float DeviceDistance { get; set; }

        public override void Dispose()
        {
            if (OnDeviceDeleted != null){OnDeviceDeleted(this, new EventArgs());}
            base.Dispose();
        }

        public void insertDevice(IConstructDevice ap)
        {
            throw new NotImplementedException();
        }

        public override void DoubleClick()
        {
            base.DoubleClick();
        }

        #region Default userinterface items that are removed from properties window
        [Browsable(false)]
        public override bool Visible
        {
            get { return base.Visible; }
            set { base.Visible = value; }
        }

        [Browsable(false)]
        public override string SectionName
        {
            get { return base.SectionName; }
            set { base.SectionName = value; }
        }

        [Browsable(false)]
        public override Core.Assemblies.EventCollection Events
        {
            get { return base.Events; }
        }

        #endregion

        [Category("Configuration")]
        public override string Name
        {
            set
            {
                IConstructDevice parentAssembly = this.Parent as IConstructDevice;
                if (parentAssembly != null)
                {
                    //Check that no other device on the assembly has the same name
                    DeviceInfo dInfo = parentAssembly.DeviceInfos.FirstOrDefault(i => i.name == value);
                    if (dInfo == null)
                    {
                        deviceInfo.name = value;
                        Core.Environment.SolutionExplorer.Update(this);
                    }
                }
            }
        }

        [Browsable(false)]
        public override Vector3 Position
        {
            get
            {
                return base.Position;
            }
            set
            {
                base.Position = value;
            }
        }

    }

    public abstract class DeviceInfo : AssemblyInfo
    {
        public float distance = 0;
        public string type = string.Empty;

        //public abstract void SetCustomInfoFields<T>(T obj, ref DeviceInfo info); probably no point in making this generic
        public abstract void SetCustomInfoFields(Assembly assem, object obj, ref DeviceInfo info);
    }

}
