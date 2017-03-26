using Experior.Catalog.Dematic.Case.Components;
using Experior.Core.Loads;
using Experior.Core.Properties;
using Experior.Dematic.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Serialization;

namespace Experior.Catalog.Dematic.ATC.Assemblies.CaseConveyor
{
    public class MHEControl_BeltSorterDivert : MHEControl
    {
        private BeltSorterDivertATCInfo divertDatcomInfo;
        private BeltSorterDivert divertConveyor;
        private MHEController_Case casePLC;
        //DivertRoute selectedRoute = DivertRoute.None;
        private List<string> DivertRoutes = null;



        #region Constructors
        public MHEControl_BeltSorterDivert(BeltSorterDivertATCInfo info, BeltSorterDivert sorterDivert)
        {
            divertConveyor = sorterDivert;
            divertDatcomInfo = info;
            Info = info;  // set this to save properties 

            //Subscribe to the diverter events
            divertConveyor.OnDivertPointArrivedControl += divertConveyor_OnDivertPointArrivedControl;
            divertConveyor.OnDivertPointDivertedControl += divertConveyor_OnDivertPointDivertedControl;

            casePLC = divertConveyor.Controller as MHEController_Case;

            //Anything with setup code in the "set" of a property except setting the value will need to be called 
            //explicitly so that the "set" code will execute when loading from a saved configuration
            DivertRoutingCode = info.divertRoutingCode;
            //StraightRoutingCode = info.straightRoutingCode;
        }

        //Load has arrived at divert point
        void divertConveyor_OnDivertPointArrivedControl(object sender, BeltSorterDivertArgs e)
        {
            if (e._load is ATCCaseLoad)
            {
                ATCCaseLoad atcLoad = e._load as ATCCaseLoad;
                if (atcLoad.Destination != null && DivertRoutes != null && DivertRoutes.Contains(atcLoad.Destination))
                {
                    //selectedRoute = DivertRoute.Divert;
                    divertConveyor.RouteLoad(DivertRoute.Divert, e._load);
                    return;
                }
            }

            //selectedRoute = DivertRoute.Straight;
            divertConveyor.RouteLoad(DivertRoute.Straight, e._load);
        }

        void divertConveyor_OnDivertPointDivertedControl(object sender, BeltSorterDivertArgs e)
        {
            //Case_Load caseLoad = e._load as Case_Load;
            //if (e._direction != selectedRoute && !string.IsNullOrEmpty(FailedToDivertLocation))
            //{
            //    //Send failed to divert message
            //    casePLC.SendDivertConfirmation(FailedToDivertLocation, caseLoad.SSCCBarcode);

            //}
            //else if (e._direction == DivertRoute.Divert && !string.IsNullOrEmpty(DivertRoutingLocation))
            //{
            //    //Send Diverted message
            //    casePLC.SendDivertConfirmation(DivertRoutingLocation, caseLoad.SSCCBarcode);
            //}
        }
        #endregion

        bool loadDeleted(Load load) //Not used no event at the moment (Don't think we care!!!)
        {
            return true;
        }


        #region User Interface

        [Category("Configuration")]
        [DisplayName("Controller Point")]
        [Description("Set true if the controller script should handle the routing. The routing will not be handleb by the selected PLC, however the configuration can still be used for routing within the controller script")]
        [PropertyOrder(1)]
        public bool ControllerPoint
        {
            get
            {
                return divertDatcomInfo.controllerPoint;
            }
            set
            {
                divertDatcomInfo.controllerPoint = value;
            }
        }

        [Category("Divert")]
        [DisplayName("Divert Routing Code")]
        [Description("Routing destinations of the load for routing Left: format xxxx,yyyy if the load destination is set to either xxxx or yyyy it will be routed Left")]
        public string DivertRoutingCode
        {
            get { return divertDatcomInfo.divertRoutingCode; }
            set
            {
                if (value == null || value == "")
                {
                    divertDatcomInfo.divertRoutingCode = null;
                    DivertRoutes = null;
                    return;
                }

                string[] splitRoutes = value.Split(',');
                DivertRoutes = new List<string>();
                foreach (string route in splitRoutes)
                {
                    DivertRoutes.Add(route);
                }

                if (DivertRoutes != null)
                {
                    divertDatcomInfo.divertRoutingCode = value;
                }
            }
        }

        [DisplayName("Divert Route Location")]
        [Description("Location name in message to ATC when successfully diverted, if blank then no divert confirmation message will be sent")]
        public string DivertRoutingLocation
        {
            get { return divertDatcomInfo.divertRoutingLocation; }
            set
            {
                if (value == "")
                    divertDatcomInfo.divertRoutingLocation = null;
                else
                {
                    divertDatcomInfo.divertRoutingLocation = value;
                }
            }
        }
        #endregion

        public override void Dispose()
        {
        }
    }

    [Serializable]
    [XmlInclude(typeof(BeltSorterDivertATCInfo))]
    public class BeltSorterDivertATCInfo : ProtocolInfo
    {
        public bool controllerPoint = false;
        public string divertRoutingCode = null;
        public string divertRoutingLocation = null;
    }
}