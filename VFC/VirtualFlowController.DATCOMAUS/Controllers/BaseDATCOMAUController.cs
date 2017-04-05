using Dematic.DATCOMAUS;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml;
using VirtualFlowController.Controllers;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace VirtualFlowController.DATCOMAUS.Controllers
{
    public abstract class BaseDATCOMAUSController: ControllerTabbed, IDATCOMAUSTelegrams
    {
        protected System.Timers.Timer liveTimer = new System.Timers.Timer();
        protected bool waitingForAck = false;
        protected string waitingAckTelegram = "";
        protected List<string> sendList = new List<string>();

        public BaseDATCOMAUSController(XmlNode xmlNode) : base(xmlNode)
        {
            //liveTimer.Elapsed += liveTimer_Elapsed;
        }

        #region Properties
        private bool _LiveTelegrams = true;
        [DisplayName("LIVE Telegrams")]
        [Category("Connection")]
        [Description("Send LIVE telegrams to the PLC to maintain TCP/IP connection")]
        [PropertyOrder(2)]
        public bool LiveTelegrams
        {
            get { return _LiveTelegrams; }
            set
            {
                _LiveTelegrams = value;
                SetAllBrowsable();
                UpdateConfig();
            }
        }

        private int _LiveInterval = 30;
        [DisplayName("LIVE Interval")]
        [Category("Connection")]
        [Description("Interval between LIVE telegrams being sent (s)")]
        [Browsable(false)]
        [PropertyOrder(3)]
        public int LiveInterval
        {
            get { return _LiveInterval; }
            set
            {
                _LiveInterval = value;
                UpdateConfig();
            }
        }
        #endregion

        #region IDCITelegrams
        private TelegramTemplate _Template = new TelegramTemplate();
        [Browsable(false)]
        [SaveVFCConfiguration(false)]
        public TelegramTemplate Template
        {
            get { return _Template; }
            set { _Template = value; }
        }

        public int GetTelegramLength(TelegramTypes telegramType)
        {
            if (telegramValidation != null)
            {
                return telegramValidation.GetTelegramBodyLength(Template.GetTelegramName(telegramType)) + 30;
            }
            return 0;
        }
        #endregion

        #region Browsable
        public override void SetAllBrowsable()
        {
            base.SetAllBrowsable();
            SetBrowsable("LiveInterval", LiveTelegrams);
        }
        #endregion

        #region Communications
        public override void Disconnect(string reasonMessage = "")
        {
            try
            {
                connection.Disconnect(reasonMessage);
                liveTimer.Stop();
            }
            catch { }
        }

        public override void Send(string Telegram, bool noAckResend = false)
        {
            if (controllerStatus == ControllerStatus.Running || controllerStatus == ControllerStatus.AutoNoMove)
            {
                string message;
                byte[] bTelegram = telegramValidation.FormatSendMessage(Telegram, out message);

                if (bTelegram != null)
                {
                    if (connection.ConnectionStatus != ConnectionStatus.Connected)
                    {
                        vfc.LogEventMessage(new LogEventMessageEventArgs(string.Format("Controller {0}: Cannot send telegrams as connection not established", Name), EventSeverity.Warning));
                        return;
                    }

                    vfc.LogTelegramMessage(new LogTelegramMessageEventArgs(Telegram, Name, TelegramDirection.Sent));
                    connection.Send(bTelegram);
                }
                else
                {
                    vfc.LogEventMessage(new LogEventMessageEventArgs(string.Format("Error {0}: Sending message - {1}", Name, Telegram), EventSeverity.Warning));
                }
            }
            else
            {
                lock (sendList)
                {
                    sendList.Add(Telegram);
                }
            }
        }

        protected void SendBuffer()
        {
            try
            {
                lock (sendList)
                {
                    if (sendList.Count != 0)
                    {
                        if ((controllerStatus == ControllerStatus.Running || controllerStatus == ControllerStatus.AutoNoMove) && !waitingForAck)
                        {
                            string telegram = string.Copy(sendList[0]);
                            sendList.Remove(sendList[0]);
                            Send(telegram);
                        }
                    }
                }
            }
            catch { }
        }

        public override void connection_OnTelegramReceived(object sender, ConnectionTelegramReceivedEventArgs e)
        {
            try
            {
                //Check if ack received for previous telegram
                bool error = false;
                string validTelegram = telegramValidation.FormatReceivedMessage(e._telegram, e._telegrambytes, out error);
                if (error)
                {
                    vfc.LogEventMessage(new LogEventMessageEventArgs(string.Format("Warning: Processing telegram: VFC>{0}: {1}", Name, validTelegram), EventSeverity.Warning));
                    return;
                }

                string[] splitTelegram = validTelegram.Split(new Char[] { ',' });
                //message can be subscribed to outside of application in project controller
                OnTelegramReceived(new ControllerTelegramReceivedEventArgs(splitTelegram));

                //Log the telegram
                vfc.LogTelegramMessage(new LogTelegramMessageEventArgs(validTelegram, Name, TelegramDirection.Received));

                //Process the telegram
                processTelegram(validTelegram);
            }
            catch (Exception ex)
            {
                vfc.LogEventMessage(new LogEventMessageEventArgs(ex.ToString(), EventSeverity.Information));
                vfc.LogEventMessage(new LogEventMessageEventArgs(string.Format("Controller {0}: Error - connection_telegramReceived", Name), EventSeverity.Warning));
                Disconnect("Error - connection_telegramReceived");
            }
        }

        void liveTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            liveTimer.Interval = LiveInterval * 1000;
            if (LiveTelegrams && connection.ConnectionStatus == ConnectionStatus.Connected)
            {
                Send(Template.CreateTelegram(this, TelegramTypes.HeartBeat));
            }
        }

        public virtual void processTelegram(string telegram) { }
        #endregion

    }
}
