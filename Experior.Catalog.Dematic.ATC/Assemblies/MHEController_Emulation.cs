using Dematic.ATC;
using Experior.Catalog.Dematic.Case.Components;
using Experior.Core.Properties;
using Experior.Core.Routes;
using Experior.Dematic.Base;
using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;

namespace Experior.Catalog.Dematic.ATC.Assemblies
{
    /// <summary>
    /// This is a PLC that handels Datcom messages
    /// </summary>
    public class EmulationATC : BaseATCController, IEmulationController
    {

        EmulationATCInfo emulationATCInfo;

        public EmulationATC(EmulationATCInfo info): base(info)
        {
            emulationATCInfo = info;

            string path = Experior.Core.Directories.Model + @"\ATCipAddress.txt";

            //Note the file will only be created when an existing model is loaded.
            if (Experior.Core.Directories.Model != "" && File.Exists(path))
            {
                IPAddress = File.ReadAllText(path);
            }

        }

        public override void Dispose()
        {
            base.Dispose();
        }

        public override void HandleTelegrams(string[] telegramFields, TelegramTypes type)
        {
            switch (type)
            {
                case TelegramTypes.CreateTuTelegram:
                    CreateTuTelegramReceived(telegramFields);
                    break;
                case TelegramTypes.DeleteTuTelegram:
                    DeleteTuTelegramReceived(telegramFields);
                    break;
                default:
                    break;
            }
        }

        private void CreateTuTelegramReceived(string[] telegramFields)
        {
            if (telegramFields.GetFieldValue(TelegramFields.location) == null)
            {
                return; //Telegram ignored
            }

            if (Core.Assemblies.Assembly.Items.ContainsKey(telegramFields.GetFieldValue(TelegramFields.location)) &&
                Core.Assemblies.Assembly.Items[telegramFields.GetFieldValue(TelegramFields.location)] is StraightConveyor)
            {
                ATCCaseLoad caseLoad = CreateCaseLoad(TelegramTypes.CreateTuTelegram, telegramFields);

                StraightConveyor sourceConv = Core.Assemblies.Assembly.Items[telegramFields.GetFieldValue(TelegramFields.location)] as StraightConveyor;
                caseLoad.SetYaw(sourceConv.Width, sourceConv.CaseOrientation);
                float position = 0;
                if (caseLoad.Yaw == 0)
                {
                    position = position + (caseLoad.Length / 2);
                }
                else
                {
                    position = position + (caseLoad.Width / 2);
                }
                sourceConv.TransportSection.Route.Add(caseLoad, position);

            }
            else
            {
                Log.Write(string.Format("ATC Error {0}: Cannot create load at location from CreateTuTelegram, location {1} does not exist, message ignored", Name, telegramFields.GetFieldValue(TelegramFields.location)), Color.Red);
            }
        }

        private void DeleteTuTelegramReceived(string[] telegramFields)
        {
            Case_Load caseLoad = Case_Load.GetCaseFromIdentification(telegramFields.GetFieldValue(TelegramFields.tuIdent));
            if (caseLoad == null)
            {
                EuroPallet pallet = EuroPallet.GetPalletFromIdentification(telegramFields.GetFieldValue(TelegramFields.tuIdent));
                if (pallet != null)
                {
                    if (pallet.Grouped.Items.Count > 0)
                    {
                        pallet.ResetEuroPallet(true);
                    }
                    pallet.Dispose();
                }
            }
            else
            {
                caseLoad.Dispose();
            }
        }

        public override ATCCaseLoad CreateCaseLoad(TelegramTypes Type, string[] Telegram)
        {
            ATCCaseLoad newLoad = null;

            if (Type == TelegramTypes.CreateTuTelegram)
            {
                string length = Telegram.GetFieldValue(TelegramFields.length);
                string width = Telegram.GetFieldValue(TelegramFields.width);
                string height = Telegram.GetFieldValue(TelegramFields.height);
                string weight = Telegram.GetFieldValue(TelegramFields.weight);
                string color = Telegram.GetFieldValue(TelegramFields.color);

                length = (length == null) ? CaseLoadLength : length;
                width = (width == null) ? CaseLoadWidth : width;
                height = (height == null) ? CaseLoadHeight : height;
                weight = (weight == null) ? CaseLoadWeight : weight;
                color = (Color == null) ? DefaultLoadColor.ToString() : color;

                newLoad = CreateCaseLoad(
                    Telegram.GetFieldValue(TelegramFields.mts),
                    Telegram.GetFieldValue(TelegramFields.tuIdent),
                    Telegram.GetFieldValue(TelegramFields.tuType),
                    Telegram.GetFieldValue(TelegramFields.location), //Location
                    Telegram.GetFieldValue(TelegramFields.destination),
                    Telegram.GetFieldValue(TelegramFields.presetStateCode),
                    height,
                    width,
                    length,
                    weight,
                    color);
            }

            //Deal with additional project specific fields
            if (newLoad != null)
            {
                foreach (string field in ProjectFields)
                {
                    string fieldValue = Telegram.GetFieldValue(field);
                    if (fieldValue != null)
                    {
                        if (newLoad.ProjectFields.ContainsKey(field))
                        {
                            newLoad.ProjectFields[field] = fieldValue;
                        }
                        else
                        {
                            newLoad.ProjectFields.Add(field, fieldValue);
                        }
                    }
                }
            }

            return newLoad;
        }

        #region IEmulationController
        public Case_Load GetCaseLoad(Route route, float position)
        {
            Case_Load caseLoad = CreateCaseLoad(CaseLoadHeight, CaseLoadWidth, CaseLoadLength, CaseLoadWeight, DefaultLoadColor.ToString());
            ((ATCCaseLoad)caseLoad).SetYaw(((StraightConveyor)route.Parent.Parent).Width, ((StraightConveyor)route.Parent.Parent).CaseOrientation);
            ((ATCCaseLoad)caseLoad).PresetStateCode = "OK";

            if (position == 0)
            {
                if (caseLoad.Yaw == 0)
                {
                    position = position + (caseLoad.Length / 2);
                }
                else
                {
                    position = position + (caseLoad.Width / 2);
                }
            }
            route.Add(caseLoad, position);
            return caseLoad;
        }

        public Tray GetTray(Route route, float position, TrayStatus status)
        {
            Tray trayLoad = CreateTray(TrayLoadHeight, TrayLoadWidth, TrayLoadLength, TrayLoadWeight, DefaultLoadColor.ToString(), status, TrayStacks);

            if (route != null)
            {
                ((ATCTray)trayLoad).SetYaw(((StraightConveyor)route.Parent.Parent).Width, ((StraightConveyor)route.Parent.Parent).CaseOrientation);
                ((ATCTray)trayLoad).PresetStateCode = "OK";

                if (position == 0)
                {
                    if (trayLoad.Yaw == 0)
                    {
                        position = position + (trayLoad.Length / 2);
                    }
                    else
                    {
                        position = position + (trayLoad.Width / 2);
                    }
                }
                route.Add(trayLoad, position);
            }
            return trayLoad;
        }

        public EuroPallet GetEuroPallet(Route route, float position, PalletStatus status)
        {
            EuroPallet palletLoad = CreateEuroPallet(PalletLoadHeight, PalletLoadWidth, PalletLoadLength, PalletLoadWeight, DefaultLoadColor.ToString(), status);

            if (route != null)
            {
                ((ATCEuroPallet)palletLoad).SetYaw(((Pallet.Assemblies.PalletStraight)route.Parent.Parent).ConveyorType);
                ((ATCEuroPallet)palletLoad).PresetStateCode = "OK";

                if (position == 0)
                {
                    if (palletLoad.Yaw == 0)
                    {
                        position = position + (palletLoad.Length / 2);
                    }
                    else
                    {
                        position = position + (palletLoad.Width / 2);
                    }
                }
                route.Add(palletLoad, position);
            }
            return palletLoad;
        }
        #endregion


        #region User Interface

        [Category("Connections")]
        [DisplayName("ATC IP Address")]
        [Description("IP Address of the PC that the ATC is running on")]
        [AlwaysEditable()]
        public string IPAddress
        {
            get
            {
                return emulationATCInfo.ipAddress;
            }
            set 
            {

                if (Experior.Core.Directories.Model != "")
                {
                    File.WriteAllText(Experior.Core.Directories.Model + @"\ATCipAddress.txt", value);
                }
                emulationATCInfo.ipAddress = value;

                foreach (Core.Communication.TCPIP.Connection connection in Core.Communication.Connection.Items.Values)
                {
                    connection.Disconnect();
                    connection.Ip = value;
                }
            }
        }
        
        [Category("Connections")]
        [DisplayName("ATC Mode")]
        [Description("Change all ATC connections to be Server or Client")]
        [AlwaysEditable()]
        public CommsModes CommsMode
        {
            get { return emulationATCInfo.CommsMode; }
            set 
            { 
                emulationATCInfo.CommsMode = value;

                foreach (Core.Communication.TCPIP.Connection connection in Core.Communication.Connection.Items.Values)
                {
                    if (value == CommsModes.Server)
                    {
                        connection.AutoConnect = true; //Starts it listening
                    }
                    else if (value == CommsModes.Client)
                    {
                        connection.AutoConnect = false; //Connect manually
                    }
                    connection.Mode = value.ToString();
                }
            }
        }

        //These need converting to integers...
        [Category("Case Loads")]
        [DisplayName("Height (mm)")]
        [PropertyOrder(1)]
        [Description("Default load height to be used when creating loads in mm")]
        public string CaseLoadHeight
        {
            get { return emulationATCInfo.caseLoadHeight; }
            set { emulationATCInfo.caseLoadHeight = value; }
        }

        [Category("Case Loads")]
        [DisplayName("Width (mm)")]
        [PropertyOrder(2)]
        [Description("Default load width to be used when creating loads in mm")]
        public string CaseLoadWidth
        {
            get { return emulationATCInfo.caseLoadWidth; }
            set { emulationATCInfo.caseLoadWidth = value; }
        }

        [Category("Case Loads")]
        [DisplayName("Length (mm)")]
        [PropertyOrder(3)]
        [Description("Default load length to be used when creating loads in mm")]
        public string CaseLoadLength
        {
            get { return emulationATCInfo.caseLoadLength; }
            set { emulationATCInfo.caseLoadLength = value; }
        }

        [Category("Case Loads")]
        [DisplayName("Weight (g)")]
        [PropertyOrder(4)]
        [Description("Default load weight to be used when creating loads in grams")]
        public string CaseLoadWeight
        {
            get { return emulationATCInfo.caseLoadWeight; }
            set { emulationATCInfo.caseLoadWeight = value; }
        }

        [Category("Tray Loads")]
        [DisplayName("Height (mm)")]
        [PropertyOrder(1)]
        [Description("Default load height to be used when creating loads in mm")]
        public float TrayLoadHeight
        {
            get { return emulationATCInfo.trayLoadHeight; }
            set { emulationATCInfo.trayLoadHeight = value; }
        }

        [Category("Tray Loads")]
        [DisplayName("Width (mm)")]
        [PropertyOrder(2)]
        [Description("Default load width to be used when creating loads in mm")]
        public float TrayLoadWidth
        {
            get { return emulationATCInfo.trayLoadWidth; }
            set { emulationATCInfo.trayLoadWidth = value; }
        }

        [Category("Tray Loads")]
        [DisplayName("Length (mm)")]
        [PropertyOrder(3)]
        [Description("Default load length to be used when creating loads in mm")]
        public float TrayLoadLength
        {
            get { return emulationATCInfo.trayLoadLength; }
            set { emulationATCInfo.trayLoadLength = value; }
        }

        [Category("Tray Loads")]
        [DisplayName("Weight (g)")]
        [PropertyOrder(4)]
        [Description("Default load weight to be used when creating loads in grams")]
        public float TrayLoadWeight
        {
            get { return emulationATCInfo.trayLoadWeight; }
            set { emulationATCInfo.trayLoadWeight = value; }
        }

        [Category("Tray Loads")]
        [DisplayName("Stacks")]
        [PropertyOrder(5)]
        [Description("Maximum number of loads allowed within a stack")]
        public uint TrayStacks
        {
            get { return emulationATCInfo.trayStacks; }
            set { emulationATCInfo.trayStacks = value; }
        }

        [Category("Pallet Loads")]
        [DisplayName("Height (m)")]
        [PropertyOrder(1)]
        [Description("Default load height to be used when creating loads in m")]
        public float PalletLoadHeight
        {
            get { return emulationATCInfo.palletLoadHeight; }
            set { emulationATCInfo.palletLoadHeight = value; }
        }

        [Category("Pallet Loads")]
        [DisplayName("Width (m)")]
        [PropertyOrder(2)]
        [Description("Default load width to be used when creating loads in m")]
        public float PalletLoadWidth
        {
            get { return emulationATCInfo.palletLoadWidth; }
            set { emulationATCInfo.palletLoadWidth = value; }
        }

        [Category("Pallet Loads")]
        [DisplayName("Length (m)")]
        [PropertyOrder(3)]
        [Description("Default load length to be used when creating loads in m")]
        public float PalletLoadLength
        {
            get { return emulationATCInfo.palletLoadLength; }
            set { emulationATCInfo.palletLoadLength = value; }
        }

        [Category("Pallet Loads")]
        [DisplayName("Weight (kg)")]
        [PropertyOrder(4)]
        [Description("Default load weight to be used when creating loads in kilograms")]
        public float PalletLoadWeight
        {
            get { return emulationATCInfo.palletLoadWeight; }
            set { emulationATCInfo.palletLoadWeight = value; }
        }

        [Category("Loads")]
        [DisplayName("Color")]
        [PropertyOrder(5)]
        [Description("Default load color to be used when creating loads")]
        public ATCColor DefaultLoadColor
        {
            get { return emulationATCInfo.defaultLoadColor; }
            set { emulationATCInfo.defaultLoadColor = value; }
        }
        #endregion
    }

    [Serializable]
    [TypeConverter(typeof(EmulationATCInfo))]
    public class EmulationATCInfo : BaseATCControllerInfo
    {
        //If these attributes are not included in the CreateTuTelegram, then use these
        public string caseLoadHeight = "350";
        public string caseLoadWidth = "400";
        public string caseLoadLength = "600";
        public string caseLoadWeight = "100";

        public float trayLoadHeight = 0.5f;
        public float trayLoadWidth = 0.45f;
        public float trayLoadLength = 0.65f;
        public float trayLoadWeight = 100;
        public uint trayStacks = 6;

        public float palletLoadHeight = 1.6f;
        public float palletLoadWidth = 0.9f;
        public float palletLoadLength = 1.3f;
        public float palletLoadWeight = 250;

        public ATCColor defaultLoadColor = ATCColor.green;
        public string ipAddress = "127.0.0.1";
        public CommsModes CommsMode = CommsModes.Client;
    }


    public enum CommsModes
    {
        Server,
        Client
    }
}
