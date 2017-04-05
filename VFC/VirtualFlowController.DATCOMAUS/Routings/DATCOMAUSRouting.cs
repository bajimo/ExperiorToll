using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Reflection;
using System.Xml;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;
using VirtualFlowController.Controllers;
using VirtualFlowController.DATCOMAUS;
using VirtualFlowController.DATCOMAUS.Controllers;
using Dematic.DATCOMAUS;

namespace VirtualFlowController.Routings
{
    public class RoutingsDATCOMAUSStringItemsSource : Xceed.Wpf.Toolkit.PropertyGrid.Attributes.IItemsSource
    {
        public Xceed.Wpf.Toolkit.PropertyGrid.Attributes.ItemCollection GetValues()
        {
            Xceed.Wpf.Toolkit.PropertyGrid.Attributes.ItemCollection routings = new Xceed.Wpf.Toolkit.PropertyGrid.Attributes.ItemCollection();
            foreach (Routing routing in vfc.AllRoutings.Values)
            {
                if (routing is DATCOMAUSRouting)
                {
                    DATCOMAUSRouting route = routing as DATCOMAUSRouting;
                    routings.Add(routing.Name, routing.Name);
                }
            }
            routings.Add("", null);
            return routings;
        }
    }
    
    [DisplayName("Routing - DATCOM AUS Transport Unit Mission")]
    public class DATCOMAUSRouting : Routing, IConveyorController
    {
        List<string> DestinationsPrimary = new List<string>();
        List<string> DestinationsSecondary = new List<string>();
        ConveyorDATCOMAUS DestController;
        string lastDestination = string.Empty;
        List<string> timerTelegrams = new List<string>();

        #region Constructor
        public DATCOMAUSRouting(XmlNode xmlNode)
            : base(xmlNode)
        {

        }
        #endregion

        #region Properties
        [Browsable(false)]
        public override TriggerMessageCase triggerMessType
        {
            get { return base.triggerMessType; }
            set { base.triggerMessType = value; }
        }

        private Controller _RouteController;
        [DisplayName("Routing Controller")]
        [Category("Routing")]
        [Description("Controller that this rotuing is to be sent to")]
        [ItemsSource(typeof(DATCOMAUSConveyorControllersItemsSource))]
        [PropertyOrder(1)]
        public Controller RouteController
        {
            get { return _RouteController; }
            set
            {
                _RouteController = value;
                if (value != null)
                {
                    DestController = value as ConveyorDATCOMAUS;
                }
                else
                {
                    DestController = null;
                }
                UpdateConfig();
            }
        }

        private RoutingTypesDATCOMAUS _RoutingTrigger;
        [DisplayName("Routing Trigger")]
        [Category("Routing")]
        [Description("Message type that triggers the routing to be sent")]
        [PropertyOrder(2)]
        public RoutingTypesDATCOMAUS RoutingTrigger
        {
            get { return _RoutingTrigger; }
            set
            {
                _RoutingTrigger = value;
                UpdateConfig();
            }
        }

        private DistributionDATCOMAUS _Distribution;
        [DisplayName("Distribution")]
        [Category("Routing")]
        [Description("First: Always choose the first destination\r\nRandom: Randomly choose and destination\r\nRoundRobin: Route to each destination in turn")]
        [PropertyOrder(3)]
        public DistributionDATCOMAUS Distribution
        {
            get { return _Distribution; }
            set
            {
                _Distribution = value;
                UpdateConfig();

            }
        }

        private string _RoutingCode = "";
        [DisplayName("Primary Destinations")]
        [Category("Routing")]
        [Description("Possible destinations for load. Comma seperated e.g. xxxxx,yyyyy means the load can be routed to either xxxxx or yyyyy, use // to disable a destination e.g. NP01,//EX01 where EX01 will be ignored")]
        [Browsable(true)]
        [PropertyOrder(4)]
        public string DestinationCode
        {
            get { return _RoutingCode; }
            set
            {
                DestinationsPrimary.Clear();
                string[] destinations = value.Split(',');
                foreach (string destination in destinations)
                {
                    if (!destination.Contains("//"))
                    {
                        if (destination != "")
                        {
                            DestinationsPrimary.Add(destination);
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                _RoutingCode = value;
                UpdateConfig();
            }
        }

        private string _RoutingCodeSecondary = "";
        [DisplayName("Secondary Destinations")]
        [Category("Routing")]
        [Description("Possible destinations for load. Comma seperated e.g. xxxxx,yyyyy means the load can be routed to either xxxxx or yyyyy, use // to disable a destination e.g. NP01,//EX01 where EX01 will be ignored")]
        [Browsable(true)]
        [PropertyOrder(5)]
        public string DestinationCodeSecondary
        {
            get { return _RoutingCodeSecondary; }
            set
            {
                DestinationsSecondary.Clear();
                string[] destinations = value.Split(',');
                foreach (string destination in destinations)
                {
                    if (!destination.Contains("//"))
                    {
                        if (destination != "")
                        {
                            DestinationsSecondary.Add(destination);
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                _RoutingCodeSecondary = value;
                UpdateConfig();
            }
        }

        private float _TelegramDelay;
        [DisplayName("Telegram Delay")]
        [Category("Routing")]
        [Description("Delay the StartTranportTelegram by this many seconds after the routing message is received")]
        [PropertyOrder(6)]
        public float TelegramDelay
        {
            get { return _TelegramDelay; }
            set
            {
                _TelegramDelay = value;
                UpdateConfig();
            }
        }

        private int _RoutePercentage = 100;
        [DisplayName("Route Percentage")]
        [Category("Routing")]
        [Description("Percentage of loads to be routed to primary route, all other loads will be routed to secondary route if destination is selected. NOTE: Only applicable with Random Distribution")]
        [PropertyOrder(7)]
        public int RoutePercentage
        {
            get { return _RoutePercentage; }
            set 
            { 
                if (value > 0 && value < 101)
                {
                    _RoutePercentage = value;
                    UpdateConfig();
                }
            }
        }
        #endregion

        #region Logic
        public override void triggerRouting(string telegramReceive)
        {
            if (DestController == null)
            {
                vfc.LogEventMessage(new LogEventMessageEventArgs(string.Format("Routing {0}: Cannot trigger routing 'Routing Controller' has not been configured", Name), EventSeverity.Warning));
                return;
            }

            barcode = telegramReceive.GetFieldValue(DestController, TelegramFields.Barcode1);
            
            if (Distribution == DistributionDATCOMAUS.None)
            {
                return;
            }
            if (DestinationsPrimary.Count == 0)
            {
                vfc.LogEventMessage(new LogEventMessageEventArgs(string.Format("Routing {0}: No routing sent, destinations not configured", Name), EventSeverity.Warning));
                return;
            }
            if (RouteController == null)
            {
                vfc.LogEventMessage(new LogEventMessageEventArgs(string.Format("Routing {0}: No routing sent, Route Controller not configured", Name), EventSeverity.Warning));
                return;
            }

            string destination = string.Empty;

            if (Distribution == DistributionDATCOMAUS.First)
            {
                destination = DestinationsPrimary[0];
            }
            else if (Distribution == DistributionDATCOMAUS.Random)
            {
                if (RoutePercentage == 100 || vfc.random.Next(1, 101) <= RoutePercentage)
                {
                    //Send to Primary Route
                    destination = DestinationsPrimary[vfc.random.Next(0, DestinationsPrimary.Count)];
                }
                else
                {
                    //Send to Secondary Route
                    if (DestinationsSecondary.Count > 0)
                    {
                        destination = DestinationsSecondary[vfc.random.Next(0, DestinationsSecondary.Count)];
                    }
                    else
                    {
                        //Dont Send Routing
                        return;
                    }
                }
            }
            else if (Distribution == DistributionDATCOMAUS.RoundRobin)
            {
                if (lastDestination == string.Empty || !DestinationsPrimary.Contains(lastDestination))
                {
                    destination = DestinationsPrimary[0];
                }
                else
                {
                    for (int i = 0; i < DestinationsPrimary.Count; i++)
                    {
                        if (DestinationsPrimary[i] == lastDestination)
                        {
                            if (i == DestinationsPrimary.Count - 1)
                            {
                                destination = DestinationsPrimary[0];
                            }
                            else
                            {
                                destination = DestinationsPrimary[i + 1];
                            }
                        }
                    }
                }
            }

            //Standard DATCOMAUS components
            string telegramSend = DestController.Template.CreateTelegram(DestController, TelegramTypes.TransportOrder);
            telegramSend = telegramSend.SetFieldValue(DestController, TelegramFields.Current, telegramReceive.GetFieldValue(DestController, TelegramFields.Current));
            telegramSend = telegramSend.SetFieldValue(DestController, TelegramFields.Destination, destination);
            telegramSend = telegramSend.SetFieldValue(DestController, TelegramFields.ULStatus, "00");
            telegramSend = telegramSend.SetFieldValue(DestController, TelegramFields.Barcode1, telegramReceive.GetFieldValue(DestController, TelegramFields.Barcode1));
            telegramSend = telegramSend.SetFieldValue(DestController, TelegramFields.Barcode2, telegramReceive.GetFieldValue(DestController, TelegramFields.Barcode2));
            telegramSend = telegramSend.SetFieldValue(DestController, TelegramFields.Profile, "@@@@");
            telegramSend = telegramSend.SetFieldValue(DestController, TelegramFields.CarrierSize, telegramReceive.GetFieldValue(DestController, TelegramFields.CarrierSize));
            telegramSend = telegramSend.SetFieldValue(DestController, TelegramFields.SpecialData, telegramReceive.GetFieldValue(DestController, TelegramFields.SpecialData));
            telegramSend = telegramSend.SetFieldValue(DestController, TelegramFields.Weight, telegramReceive.GetFieldValue(DestController, TelegramFields.Weight));
            telegramSend = telegramSend.SetFieldValue(DestController, TelegramFields.Height, telegramReceive.GetFieldValue(DestController, TelegramFields.Height));
            telegramSend = telegramSend.SetFieldValue(DestController, TelegramFields.Length, telegramReceive.GetFieldValue(DestController, TelegramFields.Length));
            telegramSend = telegramSend.SetFieldValue(DestController, TelegramFields.Width, telegramReceive.GetFieldValue(DestController, TelegramFields.Width));

            if (TelegramDelay > 0)
            {
                timerTelegrams.Add(telegramSend);
                System.Timers.Timer timer = new System.Timers.Timer();
                timer.Interval = TelegramDelay * 1000;
                timer.Elapsed += timer_Elapsed;
                timer.Start();
            }
            else
            {
                DestController.Send(telegramSend);
            }
            lastDestination = destination;
        }

        void timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            System.Timers.Timer timer = sender as System.Timers.Timer;
            timer.Elapsed -= timer_Elapsed;
            RouteController.Send(timerTelegrams[0]);
            timerTelegrams.RemoveAt(0);
        }
        #endregion

        public enum RoutingTypesDATCOMAUS
        {
            Arrival,    //02 Arrival
            Left,       //03 Left
        }

        public enum DistributionDATCOMAUS
        {
            None,
            First,
            Random,
            RoundRobin
        }
    }
}
