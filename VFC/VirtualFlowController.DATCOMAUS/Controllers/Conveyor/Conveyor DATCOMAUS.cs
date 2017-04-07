using VirtualFlowController.DATCOMAUS;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml;
using VirtualFlowController.Controllers;
using VirtualFlowController.DATCOMAUS.Controls;
using VirtualFlowController.Routings;
using Dematic.DATCOMAUS;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace VirtualFlowController.DATCOMAUS.Controllers
{
    public class DATCOMAUSConveyorControllersItemsSource : Xceed.Wpf.Toolkit.PropertyGrid.Attributes.IItemsSource
    {
        public Xceed.Wpf.Toolkit.PropertyGrid.Attributes.ItemCollection GetValues()
        {
            Xceed.Wpf.Toolkit.PropertyGrid.Attributes.ItemCollection ConvControllers = new Xceed.Wpf.Toolkit.PropertyGrid.Attributes.ItemCollection();

            foreach (Controller controller in vfc.AllControllers.Values)
            {
                if (controller is ConveyorDATCOMAUS)
                {
                    ConveyorDATCOMAUS ConvController = controller as ConveyorDATCOMAUS;
                    ConvControllers.Add(ConvController, ConvController.Name);
                }
            }
            return ConvControllers;
        }
    }

    public class ConveyorDATCOMAUS : BaseDATCOMAUSController
    {
        public ControlConveyorDATCOMAUS control = new ControlConveyorDATCOMAUS();
        
        #region Properties
        private string _LoadType = "0000";
        [DisplayName("TU Types")]
        [Category("Data")]
        [Description("Detail which are the applicable TU types for the conveyor, types must be 4 characters long and seperated by ',' e.g. 'CA01,PA01'")]
        [Browsable(true)]
        public string LoadType
        {
            get { return _LoadType; }
            set
            {
                try
                {
                    LoadTypes.Clear();
                    string[] splitLoadType = value.Split(',');
                    foreach (string loadType in splitLoadType)
                    {
                        if (loadType.Length == 4)
                            LoadTypes.Add(loadType);
                        else
                            throw new Exception();
                    }
                    _LoadType = value;
                    UpdateConfig();
                }
                catch
                {
                    LoadType = _LoadType;
                    LoadTypes = new List<string> { "0000" };
                }
            }
        }
        public List<string> LoadTypes = new List<string> { "0000" };
        #endregion

        #region Constructor
        public ConveyorDATCOMAUS(XmlNode xmlNode) : base(xmlNode)
        {
            overrideReadOnlyProperties = true;
            statusLabel.ToolTip = "DATCOM AUS Conveyor Controller";
            controllerTypeName = "DATCOM AUS Conveyor";
            control.controller = this;
            controlTabItem.Content = control;

            control.send01.Click += Send01_Click;
            control.send02.Click += Send02_Click;
        }

        private void Send02_Click(object sender, RoutedEventArgs e)
        {
            string telegramSend = Template.CreateTelegram(this, TelegramTypes.Arrival);
            telegramSend = telegramSend.SetFieldValue(this, TelegramFields.Current, control.current.Text);
            telegramSend = telegramSend.SetFieldValue(this, TelegramFields.Destination, control.destination.Text);
            telegramSend = telegramSend.SetFieldValue(this, TelegramFields.ULStatus, control.ulStatus.Text);
            telegramSend = telegramSend.SetFieldValue(this, TelegramFields.Barcode1, control.barcode1.Text);
            telegramSend = telegramSend.SetFieldValue(this, TelegramFields.Barcode2, control.barcode2.Text);
            telegramSend = telegramSend.SetFieldValue(this, TelegramFields.Profile, control.profile.Text);
            telegramSend = telegramSend.SetFieldValue(this, TelegramFields.CarrierSize, control.carrierSize.Text);
            telegramSend = telegramSend.SetFieldValue(this, TelegramFields.SpecialData, control.specialData.Text);
            telegramSend = telegramSend.SetFieldValue(this, TelegramFields.Weight, control.weight.Text);
            telegramSend = telegramSend.SetFieldValue(this, TelegramFields.Height, control.height.Text);
            telegramSend = telegramSend.SetFieldValue(this, TelegramFields.Length, control.length.Text);
            telegramSend = telegramSend.SetFieldValue(this, TelegramFields.Width, control.width.Text);
            Send(telegramSend);
        }

        private void Send01_Click(object sender, RoutedEventArgs e)
        {
            string telegramSend = Template.CreateTelegram(this, TelegramTypes.TransportOrder);
            telegramSend = telegramSend.SetFieldValue(this, TelegramFields.Current, control.current.Text);
            telegramSend = telegramSend.SetFieldValue(this, TelegramFields.Destination, control.destination.Text);
            telegramSend = telegramSend.SetFieldValue(this, TelegramFields.ULStatus, control.ulStatus.Text);
            telegramSend = telegramSend.SetFieldValue(this, TelegramFields.Barcode1, control.barcode1.Text);
            telegramSend = telegramSend.SetFieldValue(this, TelegramFields.Barcode2, control.barcode2.Text);
            telegramSend = telegramSend.SetFieldValue(this, TelegramFields.Profile, control.profile.Text);
            telegramSend = telegramSend.SetFieldValue(this, TelegramFields.CarrierSize, control.carrierSize.Text);
            telegramSend = telegramSend.SetFieldValue(this, TelegramFields.SpecialData, control.specialData.Text);
            telegramSend = telegramSend.SetFieldValue(this, TelegramFields.Weight, control.weight.Text);
            telegramSend = telegramSend.SetFieldValue(this, TelegramFields.Height, control.height.Text);
            telegramSend = telegramSend.SetFieldValue(this, TelegramFields.Length, control.length.Text);
            telegramSend = telegramSend.SetFieldValue(this, TelegramFields.Width, control.width.Text);
            Send(telegramSend);
        }

        public override void ConfigLoadComplete()
        {
        }
        #endregion

        #region Helper Methods
        //private void ChangeConveyorControllerStatus(AvailabilityStatus status)
        //{
        //    Action action = () => changeConveyorControllerStatus(status);
        //    Application.Current.Dispatcher.BeginInvoke(action);
        //}

        //private void changeConveyorControllerStatus(AvailabilityStatus status)
        //{
        //    controlTabItem.SetLabelBackColour(status.StatusColor());
        //}
        #endregion

        #region Communications
        
        public override void connection_OnStatusChanged(object sender, ConnectionStatusChangedEventArgs e)
        {
            try
            {
                if (e._connectionStatus == ConnectionStatus.Connected)
                {
                    controllerStatus = ControllerStatus.Running;
                    string telegram = Template.CreateTelegram(this, TelegramTypes.SetSystemStatus);
                    telegram = telegram.SetFieldValue(this, TelegramFields.SystemStatus, "02"); //Set ready status
                    //telegram = telegram.SetFieldValue(this, TelegramFields.DeviceIdent, "ALL");
                    Send(telegram);

                    //Start the live timer
                    liveTimer.Interval = LiveInterval * 1000;
                    liveTimer.Start();
                }
                else if (e._connectionStatus == ConnectionStatus.Disconnected)
                {
                    //When disconnecting reset the status of all the devices
                    controllerStatus = ControllerStatus.Stopped;
                }
            }
            catch (Exception ex)
            {
                vfc.LogEventMessage(new LogEventMessageEventArgs(ex.ToString(), EventSeverity.Information));
                vfc.LogEventMessage(new LogEventMessageEventArgs(string.Format("Conveyor {0}: Error - connection_statusChanged", Name), EventSeverity.Warning));
                Disconnect("Error - connection_statusChanged");
            }
        }

        public override void processTelegram(string telegram)
        {
            if (telegram.GetTelegramType(this) == TelegramTypes.RequestAllData)
            {
                //Send remap data???


            }
            else if (telegram.GetTelegramType(this) == TelegramTypes.EndRemap)
            {
                if (connection.ConnectionStatus == ConnectionStatus.Connected)
                {
                    controllerStatus = ControllerStatus.Ready;
                }
            }

            TelegramTypes type = telegram.GetTelegramType(this);
            foreach (var eType in Enum.GetValues(typeof(DATCOMAUSRouting.RoutingTypesDATCOMAUS)))
            {
                if (eType.ToString() == type.ToString()) //First check if the message type is valid for routings
                {
                    string location = telegram.GetFieldValue(this, TelegramFields.Current);
                    if (location != null && vfc.AllRoutings.ContainsKey(location)) //Then check if the location matches a routing
                    {
                        DATCOMAUSRouting routing = vfc.AllRoutings[location] as DATCOMAUSRouting;
                        if (routing.RoutingTrigger.ToString() == type.ToString()) //Then check that the trigger for the routing is correct
                        {
                            vfc.AllRoutings[location].triggerRouting(telegram);
                        }
                    }
                }
            }
        }
        #endregion
    }
}

