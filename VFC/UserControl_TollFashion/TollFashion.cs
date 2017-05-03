using System.Timers;
using System.Windows;
using VirtualFlowController.Controllers;
using System.Xml;
using System;
using System.ComponentModel;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace VirtualFlowController.Core
{
    //class name must be the same as the project name
    //change the name of the dll to be the same as the project name
    public class TollFashion : ProjectController
    {
        TollFashionControl control;
        EMUDAIUK EMU = vfc.AllControllers["EMU"] as EMUDAIUK;

        Timer DecantTimer = new Timer();
        private int productToteBarcodeCount;

        private bool _Decant1Enabled = true;
        [Category("Decant")]
        [DisplayName("Station 1")]
        [Description("Create loads at this station when the timer elapses")]
        [PropertyOrder(1)]
        public bool Decant1Enabled
        {
            get { return _Decant1Enabled; }
            set { _Decant1Enabled = value; UpdateConfig(); }
        }

        private bool _Decant2Enabled = true;
        [Category("Decant")]
        [DisplayName("Station 2")]
        [Description("Create loads at this station when the timer elapses")]
        [PropertyOrder(2)]
        public bool Decant2Enabled
        {
            get { return _Decant2Enabled; }
            set { _Decant2Enabled = value; UpdateConfig(); }
        }

        private bool _Decant3Enabled = true;
        [Category("Decant")]
        [DisplayName("Station 3")]
        [Description("Create loads at this station when the timer elapses")]
        [PropertyOrder(3)]
        public bool Decant3Enabled
        {
            get { return _Decant3Enabled; }
            set { _Decant3Enabled = value; UpdateConfig(); }
        }

        private bool _Decant4Enabled = true;
        [Category("Decant")]
        [DisplayName("Station 4")]
        [Description("Create loads at this station when the timer elapses")]
        [PropertyOrder(4)]
        public bool Decant4Enabled
        {
            get { return _Decant4Enabled; }
            set { _Decant4Enabled = value; UpdateConfig(); }
        }

        private bool _Decant5Enabled = true;
        [Category("Decant")]
        [DisplayName("Station 5")]
        [Description("Create loads at this station when the timer elapses")]
        [PropertyOrder(5)]
        public bool Decant5Enabled
        {
            get { return _Decant5Enabled; }
            set { _Decant5Enabled = value; UpdateConfig(); }
        }

        private bool _Decant6Enabled = true;
        [Category("Decant")]
        [DisplayName("Station 6")]
        [Description("Create loads at this station when the timer elapses")]
        [PropertyOrder(6)]
        public bool Decant6Enabled
        {
            get { return _Decant6Enabled; }
            set { _Decant6Enabled = value; UpdateConfig(); }
        }

        private bool _Decant7Enabled = true;
        [Category("Decant")]
        [DisplayName("Station 7")]
        [Description("Create loads at this station when the timer elapses")]
        [PropertyOrder(7)]
        public bool Decant7Enabled
        {
            get { return _Decant7Enabled; }
            set { _Decant7Enabled = value; UpdateConfig(); }
        }

        private bool _Decant8Enabled = true;
        [Category("Decant")]
        [DisplayName("Station 8")]
        [Description("Create loads at this station when the timer elapses")]
        [PropertyOrder(8)]
        public bool Decant8Enabled
        {
            get { return _Decant8Enabled; }
            set { _Decant8Enabled = value; UpdateConfig(); }
        }

        private bool _Decant9Enabled = true;
        [Category("Decant")]
        [DisplayName("Station 9")]
        [Description("Create loads at this station when the timer elapses")]
        [PropertyOrder(9)]
        public bool Decant9Enabled
        {
            get { return _Decant9Enabled; }
            set { _Decant9Enabled = value; UpdateConfig(); }
        }

        private bool _Decant10Enabled = true;
        [Category("Decant")]
        [DisplayName("Station 10")]
        [Description("Create loads at this station when the timer elapses")]
        [PropertyOrder(10)]
        public bool Decant10Enabled
        {
            get { return _Decant10Enabled; }
            set { _Decant10Enabled = value; UpdateConfig(); }
        }

        private bool _Decant11Enabled = true;
        [Category("Decant")]
        [DisplayName("Station 11")]
        [Description("Create loads at this station when the timer elapses")]
        [PropertyOrder(11)]
        public bool Decant11Enabled
        {
            get { return _Decant11Enabled; }
            set { _Decant11Enabled = value; UpdateConfig(); }
        }

        private bool _Decant12Enabled = true;
        [Category("Decant")]
        [DisplayName("Station 12")]
        [Description("Create loads at this station when the timer elapses")]
        [PropertyOrder(12)]
        public bool Decant12Enabled
        {
            get { return _Decant12Enabled; }
            set { _Decant12Enabled = value; UpdateConfig(); }
        }

        private bool _Decant13Enabled = true;
        [Category("Decant")]
        [DisplayName("Station 13")]
        [Description("Create loads at this station when the timer elapses")]
        [PropertyOrder(13)]
        public bool Decant13Enabled
        {
            get { return _Decant13Enabled; }
            set { _Decant13Enabled = value; UpdateConfig(); }
        }

        private bool _Decant14Enabled = true;
        [Category("Decant")]
        [DisplayName("Station 14")]
        [Description("Create loads at this station when the timer elapses")]
        [PropertyOrder(14)]
        public bool Decant14Enabled
        {
            get { return _Decant14Enabled; }
            set { _Decant14Enabled = value; UpdateConfig(); }
        }

        private bool _Decant15Enabled = true;
        [Category("Decant")]
        [DisplayName("Station 15")]
        [Description("Create loads at this station when the timer elapses")]
        [PropertyOrder(15)]
        public bool Decant15Enabled
        {
            get { return _Decant15Enabled; }
            set { _Decant15Enabled = value; UpdateConfig(); }
        }

        private bool _Decant16Enabled = true;
        [Category("Decant")]
        [DisplayName("Station 16")]
        [Description("Create loads at this station when the timer elapses")]
        [PropertyOrder(16)]
        public bool Decant16Enabled
        {
            get { return _Decant16Enabled; }
            set { _Decant16Enabled = value; UpdateConfig(); }
        }

        private int _DecantInterval = 5;
        [Category("Decant")]
        [DisplayName("Release Interval")]
        [Description("How often do you want to release loads to the decant area")]
        [PropertyOrder(17)]
        public int DecantInterval
        {
            get { return _DecantInterval; }
            set { _DecantInterval = value; UpdateConfig(); }
        }

        public TollFashion(XmlNode xmlNode) : base(xmlNode)
        {
            //project control is a generic user control for the main application project extension so that VFC can load the custom user control
            projectControl = new TollFashionControl();
            //reference the project user control so that the code has a handle on the objects within custom user control
            control = projectControl as TollFashionControl;

            control.DecantStart.Click += DecantStart_Click;
            control.DecantStop.Click += DecantStop_Click;

            DecantTimer.AutoReset = false;
            DecantTimer.Elapsed += DecantTimer_Elapsed;

            EMU.telegramReceived += EMU_telegramReceived;
        }

        void EMU_telegramReceived(object sender, ControllerTelegramReceivedEventArgs e)
        {

        }

        void Reset_Click(object sender, RoutedEventArgs e)
        {
   
        }

        #region Decant
        void DecantTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            OrderStartGo();
        }

        void DecantStart_Click(object sender, RoutedEventArgs e)
        {
            OrderStartGo();
        }
        
        void DecantStop_Click(object sender, RoutedEventArgs e)
        {
            DecantTimer.Stop();
        }

        private void OrderStartGo()
        {
            if (EMU.controllerStatus == ControllerStatus.Running)
            {
                if (Decant1Enabled) FeedTelegram("DC1", "600", "400", "365", "200");
                if (Decant2Enabled) FeedTelegram("DC2", "600", "400", "365", "200");
                if (Decant3Enabled) FeedTelegram("DC3", "600", "400", "365", "200");
                if (Decant4Enabled) FeedTelegram("DC4", "600", "400", "365", "200");
                if (Decant5Enabled) FeedTelegram("DC5", "600", "400", "365", "200");
                if (Decant6Enabled) FeedTelegram("DC6", "600", "400", "365", "200");
                if (Decant7Enabled) FeedTelegram("DC7", "600", "400", "365", "200");
                if (Decant8Enabled) FeedTelegram("DC8", "600", "400", "365", "200");
                if (Decant9Enabled) FeedTelegram("DC9", "600", "400", "365", "200");
                if (Decant10Enabled) FeedTelegram("DC10", "600", "400", "365", "200");
                if (Decant11Enabled) FeedTelegram("DC11", "600", "400", "365", "200");
                if (Decant12Enabled) FeedTelegram("DC12", "600", "400", "365", "200");
                if (Decant13Enabled) FeedTelegram("DC13", "600", "400", "365", "200");
                if (Decant14Enabled) FeedTelegram("DC14", "600", "400", "365", "200");
                if (Decant15Enabled) FeedTelegram("DC15", "600", "400", "365", "200");
                if (Decant16Enabled) FeedTelegram("DC16", "600", "400", "365", "200");

                //Reset the timer and start again
                DecantTimer.Interval = DecantInterval * 1000;
                DecantTimer.Start();
            }
            else
            {
                DecantTimer.Stop();
            }
        }

        private string NewProductToteBarcode()
        {
            //Prefix 9 + 6 digit number 000000 to 999999
            productToteBarcodeCount++;
            if (productToteBarcodeCount > 999999)
                productToteBarcodeCount = 0;
            return $"9{productToteBarcodeCount:D6}";
        }

        private void FeedTelegram(string location, string length, string width, string height, string weight)
        {
            var telegram = $"FEEDF001000001,{location},{NewProductToteBarcode()},{length},{width},{height},{weight}";

            EMU.Send(telegram);
        }
        #endregion
    }
}
