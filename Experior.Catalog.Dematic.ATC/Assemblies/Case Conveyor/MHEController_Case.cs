using Experior.Catalog.Dematic.Case.Components;
using Experior.Catalog.Dematic.Case.Devices;
using Experior.Core.Assemblies;
using Experior.Core.Loads;
using Experior.Dematic.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using Dematic.ATC;

namespace Experior.Catalog.Dematic.ATC.Assemblies.CaseConveyor
{
    /// <summary>
    /// This is a PLC that handels ATC messages
    /// </summary>
    public class MHEController_Case : BaseATCController, IController, ICaseController
    {
        MHEController_CaseATCInfo caseATCInfo;

        public MHEController_Case(MHEController_CaseATCInfo info): base(info)
        {
            caseATCInfo = info;
        }

        public override void Dispose()
        {
            base.Dispose();
        }

        public MHEControl CreateMHEControl(IControllable assem, ProtocolInfo info)
        {
            MHEControl protocolConfig = null;  //generic plc config object
            Dictionary<string, Type> dt = new Dictionary<string, Type>();

            if (assem is CommunicationPoint)
            {
                protocolConfig = CreateMHEControlGeneric<CommPointATCInfo, MHEControl_CommPoint>(assem, info);
            }
            else if (assem is MergeDivertConveyor)
            {
                protocolConfig = CreateMHEControlGeneric<MergeDivertATCInfo, MHEControl_MergeDivert>(assem, info);
            }
            else if (assem is MiniloadPickStation)
            {
                protocolConfig = CreateMHEControlGeneric<MiniloadPickStationATCInfo, MHEControl_MiniloadPickStation>(assem, info);
            }
            else if (assem is Transfer)
            {
                protocolConfig = CreateMHEControlGeneric<TransferATCInfo, MHEControl_Transfer>(assem, info);
            }
            else if (assem is StraightAccumulationConveyor)
            {
                //protocolConfig = CreateMHEControlGeneric<ManualPickingATCInfo, MHEControl_ManualPicking>(assem, info);
            }
            else if (assem is BeltSorterDivert)
            {
                protocolConfig = CreateMHEControlGeneric<BeltSorterDivertATCInfo, MHEControl_BeltSorterDivert>(assem, info);
            }
            else if (assem is AngledDivert)
            {
                //protocolConfig = CreateMHEControlGeneric<AngledDivertATCInfo, MHEControl_AngledDivert>(assem, info);
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
        public override void HandleTelegrams(string[] telegramFields, TelegramTypes type)
        {
            switch (type)
            {
                case TelegramTypes.StartTransportTelegram:
                    StartTransportTelegramReceived(telegramFields);
                    break;
                case TelegramTypes.CancelTransportTelegram:
                    CancelTransportTelegramReceived(telegramFields);
                    break;

                default:
                    break;
            }
        }

        private void StartTransportTelegramReceived(string[] telegramFields)
        {
            //Look for the load somewhere in the model, if the load is found then change it's status, if not create it at the source location
            //ATCCaseLoad caseLoad = (ATCCaseLoad)Case_Load.GetCaseFromIdentification(telegramFields.GetFieldValue(TelegramFields.tuIdent));
            IATCCaseLoadType caseLoad = (IATCCaseLoadType)Case_Load.GetCaseFromIdentification(telegramFields.GetFieldValue(TelegramFields.tuIdent));


            if (caseLoad != null) //The load has been found so some attributes need to be changed (Cannot change the dimensions of the load however)
            {
                caseLoad.TUType          = telegramFields.GetFieldValue(TelegramFields.tuType);
                caseLoad.Source          = telegramFields.GetFieldValue(TelegramFields.source);
                caseLoad.Destination     = telegramFields.GetFieldValue(TelegramFields.destination);
                caseLoad.PresetStateCode = telegramFields.GetFieldValue(TelegramFields.presetStateCode);
                caseLoad.Color           = LoadColor(telegramFields.GetFieldValue(TelegramFields.color));

                float weight;
                float.TryParse(telegramFields.GetFieldValue(TelegramFields.weight), out weight);
                caseLoad.CaseWeight = weight / 1000;

                //Deal with additional project specific fields
                foreach (string field in ProjectFields)
                {
                    string fieldValue = telegramFields.GetFieldValue(field);
                    if (fieldValue != null)
                    {
                        if (caseLoad.ProjectFields.ContainsKey(field))
                        {
                            caseLoad.ProjectFields[field] = fieldValue;
                        }
                        else
                        {
                            caseLoad.ProjectFields.Add(field, fieldValue);
                        }
                    }
                }

                //The load may be at a request location and the load will need to be released
                if (caseLoad.LoadWaitingForWCS && caseLoad.Stopped)
                {
                    //Load may be waiting on a transfer so call the mergeDivert 
                    if (caseLoad.Route.Parent.Parent is MergeDivertConveyor)
                    {
                        MergeDivertConveyor mergeDivert = caseLoad.Route.Parent.Parent as MergeDivertConveyor;
                        //if (mergeDivert.divertArrival != null)
                        //{
                        //    mergeDivert.divertArrival(caseLoad);
                        //}
                        mergeDivert.ControlDivertPoint((Load)caseLoad);
                    }

                    caseLoad.LoadWaitingForWCS = false;
                    caseLoad.ReleaseLoad();
                }
            }
            else //The load has not been found but should one be created? Normally created through the Emulation Control Telegrams
            {
                if (Core.Assemblies.Assembly.Items.ContainsKey(telegramFields.GetFieldValue(TelegramFields.source)) &&
                    Core.Assemblies.Assembly.Items[telegramFields.GetFieldValue(TelegramFields.source)] is StraightConveyor)
                {
                    caseLoad = CreateCaseLoad(TelegramTypes.StartTransportTelegram, telegramFields);
                    StraightConveyor sourceConv = Core.Assemblies.Assembly.Items[telegramFields.GetFieldValue(TelegramFields.source)] as StraightConveyor;
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
                    sourceConv.TransportSection.Route.Add((Load)caseLoad, position);
                }
                else
                {
                    Log.Write(string.Format("ATC Error {0}: Cannot create load at location from StartTransportTelegram, location {1} does not exist, message ignored", Name, telegramFields.GetFieldValue(TelegramFields.source)), Color.Red);
                }
            }
        }

        private void CancelTransportTelegramReceived(string[] telegramFields)
        {
            //If cancel telegram is received, find the load in the model, delete it and send a Confirm
            string tuIdent = telegramFields.GetFieldValue(TelegramFields.tuIdent);
            IATCCaseLoadType atcLoad =  (IATCCaseLoadType)Case_Load.GetCaseFromIdentification(tuIdent);
            if (atcLoad != null)
            {
                string telegram = string.Empty.InsertType(TelegramTypes.ConfirmCancelTelegram);
                telegram = telegram.AppendField(TelegramFields.tuIdent, atcLoad.TUIdent);
                telegram = telegram.AppendField(TelegramFields.mts, telegramFields.GetFieldValue(TelegramFields.mts));
                telegram = telegram.AppendField(TelegramFields.stateCode, telegramFields.GetFieldValue(TelegramFields.presetStateCode));
                telegram = telegram.AppendField(TelegramFields.location, atcLoad.Location);
                SendTelegram(telegram, true);
                atcLoad.Dispose();
            }
            else
            {
                //Could not find the load, it has not been deleted
                Log.Write(string.Format("ATC Error {0}: Could not find load {1} in CancelTransportTelegram, load has not been deleted", Name, tuIdent), Color.Red);
            }
        }


        #region Send Telegrams
        /// <summary>
        /// Send a Location Left Telegram
        /// </summary>
        /// <param name="load">The load</param>
        /// <param name="weight">default null: Should Location Left Contain Weight</param>
        public void SendLocationArrivedTelegram(IATCCaseLoadType load, string weight = null)
        {
            string telegram = CreateTelegramFromLoad(TelegramTypes.LocationArrivedTelegram, load);
            if (weight != null)
            {
                telegram = telegram.InsertField("weight", weight);
            }
            SendTelegram(telegram, true);
        }

        public void SendLocationLeftTelegram(IATCCaseLoadType load)
        {
            string telegram = CreateTelegramFromLoad(TelegramTypes.LocationLeftTelegram, load);
            SendTelegram(telegram, true);
        }

        public void SendTransportRequestTelegram(IATCCaseLoadType load)
        {
            string telegram = CreateTelegramFromLoad(TelegramTypes.TransportRequestTelegram, load);
            telegram = telegram.InsertField("dropIndex", load.DropIndex.ToString().PadLeft(4, '0')); //Additional drop index added to build sequencing into ATC
            SendTelegram(telegram, true);
        }

        public void SendTransportFinishedTelegram(IATCCaseLoadType load)
        {
            string telegram = CreateTelegramFromLoad(TelegramTypes.TransportFinishedTelegram, load);
            SendTelegram(telegram, true);
        }

        public void SendMultipleTransportFinishedTelegram(IATCCaseLoadType load1, IATCCaseLoadType load2)
        {
            string telegram1 = CreateTelegramFromLoad(TelegramTypes.TransportFinishedTelegram, load1);
            string telegram2 = CreateTelegramFromLoad(TelegramTypes.TransportFinishedTelegram, load2);
            string telegram = Telegrams.CreateMultipalMessage(new List<string>() { telegram1, telegram2 }, TelegramTypes.MultipleTransportFinishedTelegram, Name);
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

        #region ICaseController
        public BaseCaseData GetCaseData()
        {
            return new ATCCaseData();
        }
        #endregion

        public void RemoveSSCCBarcode(string ULID)
        {
            throw new NotImplementedException();
        }


        //public Case_Load CreateCaseLoad(BaseCaseData caseData)
        //{
        //    throw new NotImplementedException();
        //}
    }

    [Serializable]
    [TypeConverter(typeof(MHEController_CaseATCInfo))]
    public class MHEController_CaseATCInfo : BaseATCControllerInfo{}
}