using Experior.Dematic.Base;
using System;
using System.Collections.Generic;
using Dematic.ATC;
using System.ComponentModel;
using Experior.Catalog.Dematic.Pallet.Assemblies;
using Experior.Core.Assemblies;
using Experior.Core.Loads;
using System.Drawing;
using Experior.Dematic.Pallet.Devices;

namespace Experior.Catalog.Dematic.ATC.Assemblies.PalletConveyor
{
    public class MHEController_Pallet : BaseATCController, IController, IPalletController
    {
        MHEController_PalletATCInfo palletATCInfo;

        public MHEController_Pallet(MHEController_PalletATCInfo info): base(info)
        {
            palletATCInfo = info;
        }

        public override void Dispose()
        {
            base.Dispose();
        }

        public MHEControl CreateMHEControl(IControllable assem, ProtocolInfo info)
        {
            MHEControl protocolConfig = null;  //generic plc config object
            Dictionary<string, Type> dt = new Dictionary<string, Type>();

            if (assem is TCar)
            {
                protocolConfig = CreateMHEControlGeneric<TCarATCInfo, MHEControl_TCar>(assem, info);
            }
            else if (assem is Lift)
            {
                protocolConfig = CreateMHEControlGeneric<LiftATCInfo, MHEControl_Lift>(assem, info);
            }
            else if (assem is SingleDropStation)
            {
                protocolConfig = CreateMHEControlGeneric<SingleDropStationATCInfo, MHEControl_SingleDropStation>(assem, info);
            }
            else if (assem is LiftTable)
            {
                protocolConfig = CreateMHEControlGeneric<LiftTableATCInfo, MHEControl_LiftTable>(assem, info);
            }
            else if (assem is PalletStraight)
            {
                protocolConfig = CreateMHEControlGeneric<PalletStraightATCInfo, MHEControl_PalletStraight>(assem, info);
            }
            else if (assem is PalletCommunicationPoint)
            {
                protocolConfig = CreateMHEControlGeneric<PalletCommPointATCInfo, MHEControl_PalletCommPoint>(assem, info);
            }
            else
            {
                Experior.Core.Environment.Log.Write("Can't create MHE Control, object is not defined in the 'CreateMHEControl' of the controller");
                return null;
            }
            //......other assemblies should be added here....do this with generics...correction better to do this with reflection...That is BaseController should use reflection
            //and not generics as we do not know the types at design time and it means that the above always has to be edited when adding a new MHE control object.
            protocolConfig.ParentAssembly = (Assembly)assem;
            return protocolConfig as MHEControl;
        }

        public BasePalletData GetPalletData()
        {
            return new ATCPalletData();
        }

        public override void HandleTelegrams(string[] telegramFields, TelegramTypes type)
        {
            switch (type)
            {
                case TelegramTypes.StartTransportTelegram:
                    StartTransportTelegramReceived(telegramFields);
                    break;
                case TelegramTypes.CancelTransportTelegram:
                    //CancelTransportTelegramReceived(telegramFields);
                    break;

                default:
                    break;
            }
        }

        private void StartTransportTelegramReceived(string[] telegramFields)
        {
            Experior.Dematic.Base.EuroPallet euroPallet = Experior.Dematic.Base.EuroPallet.GetPalletFromIdentification(telegramFields.GetFieldValue(TelegramFields.tuIdent));
            IATCPalletLoadType palletLoad = (IATCPalletLoadType)euroPallet;
            if (palletLoad != null) //The load has been found so some attributes need to be changed (Cannot change the dimensions of the load however)
            {
                // Basic properties
                euroPallet.LoadColor = LoadColor(telegramFields.GetFieldValue(TelegramFields.color));
                // Controller specific properties
                palletLoad.TUType = telegramFields.GetFieldValue(TelegramFields.tuType);
                palletLoad.Source = telegramFields.GetFieldValue(TelegramFields.source);
                palletLoad.Destination = telegramFields.GetFieldValue(TelegramFields.destination);
                palletLoad.PresetStateCode = telegramFields.GetFieldValue(TelegramFields.presetStateCode);
                
                float weight;
                float.TryParse(telegramFields.GetFieldValue(TelegramFields.weight), out weight);
                palletLoad.PalletWeight = weight / 1000;

                //Deal with additional project specific fields
                foreach (string field in ProjectFields)
                {
                    string fieldValue = telegramFields.GetFieldValue(field);
                    if (fieldValue != null)
                    {
                        if (palletLoad.ProjectFields.ContainsKey(field))
                        {
                            palletLoad.ProjectFields[field] = fieldValue;
                        }
                        else
                        {
                            palletLoad.ProjectFields.Add(field, fieldValue);
                        }
                    }
                }

                //The load may be at a request location and the load will need to be released
                if (palletLoad.LoadWaitingForWCS)
                {
                    //Load may be waiting on a straight conveyor so call the straightConveyor 
                    if (palletLoad.Route.Parent.Parent is PalletStraight)
                    {
                        palletLoad.LoadWaitingForWCS = false;
                        PalletStraight palletStraight = palletLoad.Route.Parent.Parent as PalletStraight;
                        palletStraight.ReleaseLoad((Load)palletLoad);
                    }
                    else if (palletLoad.Route.Parent.Parent is SingleDropStation)
                    {
                        palletLoad.LoadWaitingForWCS = false;
                        SingleDropStation dropStation = palletLoad.Route.Parent.Parent as SingleDropStation;
                        SendLocationLeftTelegram(palletLoad);
                        dropStation.RouteLoadStraight((Load)palletLoad);
                        //palletLoad.ReleaseLoad_WCSControl();
                    }
                }
            }
            else //The load has not been found but should one be created? Normally created through the Emulation Control Telegrams
            {
                if (Core.Assemblies.Assembly.Items.ContainsKey(telegramFields.GetFieldValue(TelegramFields.source)) &&
                    Core.Assemblies.Assembly.Items[telegramFields.GetFieldValue(TelegramFields.source)] is BaseStraight)
                {
                    palletLoad =  CreateEuroPallet(TelegramTypes.StartTransportTelegram, telegramFields);
                    BaseStraight sourceConv = Core.Assemblies.Assembly.Items[telegramFields.GetFieldValue(TelegramFields.source)] as BaseStraight;
                    palletLoad.SetYaw(sourceConv.ConveyorType);
                    float position = 0;
                    if (palletLoad.Yaw == 0)
                    {
                        position = position + (palletLoad.Length / 2);
                    }
                    else
                    {
                        position = position + (palletLoad.Width / 2);
                    }
                    sourceConv.TransportSection.Route.Add((Load)palletLoad, position);
                }
                else
                {
                    Log.Write(string.Format("ATC Error {0}: Cannot create load at location from StartTransportTelegram, location {1} does not exist, message ignored", Name, telegramFields.GetFieldValue(TelegramFields.source)), Color.Red);
                }
            }
        }




        #region Send Telegrams

        public void SendLocationArrivedTelegram(IATCLoadType load)
        {
            string telegram = CreateTelegramFromLoad(TelegramTypes.LocationArrivedTelegram, load);
            SendTelegram(telegram, true);
        }

        public void SendLocationLeftTelegram(IATCLoadType load)
        {
            string telegram = CreateTelegramFromLoad(TelegramTypes.LocationLeftTelegram, load);
            SendTelegram(telegram, true);
        }

        public void SendTransportRequestTelegram(IATCLoadType load)
        {
            string telegram = CreateTelegramFromLoad(TelegramTypes.TransportRequestTelegram, load);
            SendTelegram(telegram, true);
        }

        public void SendTransportFinishedTelegram(IATCLoadType load)
        {
            string telegram = CreateTelegramFromLoad(TelegramTypes.TransportFinishedTelegram, load);
            SendTelegram(telegram, true);
        }

        public void SendUnitFillingTelegram(string location, string currentLevel, string maximumLevel)
        {
            string telegram = string.Empty.InsertType(TelegramTypes.UnitFillingTelegram);
            telegram = telegram.AppendField(TelegramFields.mts, Name);
            telegram = telegram.AppendField(TelegramFields.location, location);
            telegram = telegram.AppendField(TelegramFields.currentLevel, currentLevel);
            telegram = telegram.AppendField(TelegramFields.maximumLevel, maximumLevel);
            SendTelegram(telegram, true);
        }
        #endregion


        public void RemoveSSCCBarcode(string ULID)
        {
            throw new NotImplementedException();
        }

        [Category("Project")]
        [DisplayName("Load Type")]
        [Browsable(false)]
        public override LoadTypes LoadType
        {
            get; set;
        }
    }

    [Serializable]
    [TypeConverter(typeof(MHEController_PalletATCInfo))]
    public class MHEController_PalletATCInfo : BaseATCControllerInfo { }
}
