using Experior.Catalog.Dematic.Pallet;
using Experior.Catalog.Logistic.Track;
using Experior.Core.Assemblies;
using Experior.Dematic.Base.Devices;
using Microsoft.DirectX;
using System;
using System.Collections.Generic;

namespace Experior.Dematic.Pallet.Devices
{
    public  class ConstructDevice 
    {
        public System.Windows.Forms.ToolStripMenuItem[] subMnu;
        public IConstructDevice conveyor;

        private static int deviceNum;
        private delegate void Mouse_Click(object sender, EventArgs e);   //needed for submenu interaction

        private Dictionary<string, Tuple<string, string, string>> deviceTypes = new Dictionary<string, Tuple<string, string, string>>();

        public Dictionary<string, Tuple<string, string, string>> DeviceTypes
        {
            get { return deviceTypes; }
            set { deviceTypes = value; }
        }

        public Tuple<string, string, string> DeviceType;

        #region Constructors

        public ConstructDevice(string convName)
        {
            //To add a new device just create a new Tuple as below and that is all you need to do.
            DeviceType = new Tuple<string, string, string>
                ("Experior.Catalog.Dematic.Pallet.Devices.PalletPhotocell",
                "Experior.Catalog.Dematic.Pallet.Devices.PalletPhotocellInfo",
                "photoeye");

            DeviceTypes.Add("Add Photocell", DeviceType);

            DeviceType = new Tuple<string, string, string>
               ("Experior.Dematic.Pallet.Devices.PalletCommunicationPoint",
               "Experior.Dematic.Pallet.Devices.PalletCommunicationPointInfo",
               "commpoint");
            DeviceTypes.Add("Add Comm Point", DeviceType);

            subMnu = new System.Windows.Forms.ToolStripMenuItem[DeviceTypes.Count];
            int menuCount = 0;
            foreach (string key in DeviceTypes.Keys)
            {
                Tuple<string, string, string> tuple = DeviceTypes[key];
                subMnu[menuCount] = new System.Windows.Forms.ToolStripMenuItem(key, Common.Icons.Get(tuple.Item3));
                subMnu[menuCount].Click += new EventHandler(AddDevice);
                subMnu[menuCount].Enabled = true;
                menuCount++;
            }

            //If you are adding a device before the assembly has been created the conveyor will not have been created
            //conveyor is needed only when a user adds a device from a right click event
            //Linefull photoeyes are created as default during construction and not by a user
            if (Core.Assemblies.Assembly.Items.ContainsKey(convName))
            {
                conveyor = Core.Assemblies.Assembly.Items[convName] as IConstructDevice;
            }
        }

        #endregion

        #region Comm Points

        public void InsertDevices(IConstructDevice conv)
        {
            try
            {
                conv.DeviceInfos.ForEach(deviceInfo => InsertDevice(deviceInfo, conv));
            }
            catch (Exception ex)
            {
                Core.Environment.Log.Write(ex);
            }
        }

        public Device InsertDevice(DeviceInfo deviceInfo, IConstructDevice conv)
        {
            Device device = null;
            try
            {
                deviceInfo.SetCustomInfoFields(conv as Assembly, null, ref deviceInfo);

                Type type = Type.GetType(deviceInfo.type);
                device = (Device)Activator.CreateInstance(type, new object[] { deviceInfo, conv });
                conv.addAssembly(device, new Vector3(0, 0, 0));
                device.DeviceDistance = deviceInfo.distance;
                device.OnDeviceDeleted += device_OnDeviceDeleted;
            }
            catch (Exception ex)
            {
                Log.Write(ex.Message);
            }
            return device;
        }

        void device_OnDeviceDeleted(object sender, EventArgs e)
        {
            Device device = sender as Device;
            if (device != null)
            {
                try { 
                    ((IConstructDevice)device.conveyor).removeAssembly(device); 
                }
                catch (Exception ex)
                {
                    Log.Write(ex.Message);
                }
                ((IConstructDevice)device.conveyor).DeviceInfos.Remove(device.Info as DeviceInfo);
                device.OnDeviceDeleted -= device_OnDeviceDeleted;
            }   
        }
        #endregion

        #region Sub Menu methods etc

        /// <summary>
        /// Method assigned to context menu event handler. This will create the communication point 
        /// and add its info object ComPoints array in the conveyor.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void AddDevice(object sender, EventArgs e)
        {
            try
            {
                if (Core.Environment.InvokeRequired)
                {
                    Core.Environment.Invoke(() => AddDevice(sender,e));
                    return ;
                }
            }
            catch (Exception ex)
            {
                Core.Environment.Log.Write(ex);
            }

            try
            {
                Type type1         = Type.GetType(deviceTypes[sender.ToString()].Item2);
                DeviceInfo devInfo = (DeviceInfo)Activator.CreateInstance(type1);
                devInfo.SetCustomInfoFields(conveyor as Assembly, null, ref devInfo);
                devInfo.name       = "Device " + deviceNum++;
                devInfo.type       = deviceTypes[sender.ToString()].Item1;
                //Type type          = Type.GetType(deviceTypes[sender.ToString()].Item1);
                //Device device = (Device)Activator.CreateInstance(type, new object[] { devInfo, (BaseTrack)conveyor });
                devInfo.distance   = ((BaseTrack)conveyor).TransportSection.Route.Length / 2;
                InsertDevice(devInfo, conveyor);
                conveyor.DeviceInfos.Add(devInfo);  //save the info class so that it can be recreated
            }
            catch (Exception ex)
            {
                Core.Environment.Log.Write(ex);
            }
            return;
        }

        #endregion

        #region Sub Menu methods etc


        #endregion
    }

}