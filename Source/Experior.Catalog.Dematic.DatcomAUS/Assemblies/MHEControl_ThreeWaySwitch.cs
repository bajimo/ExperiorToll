using Experior.Catalog.Dematic.Case.Components;
using Experior.Catalog.Dematic.Custom.Components;
using Experior.Core.Properties;
using Experior.Dematic.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Serialization;

namespace Experior.Catalog.Dematic.DatcomAUS.Assemblies
{
    public class MHEControl_ThreeWaySwitch : MHEControl
    {
        private ThreeWaySwitchDatcomAusInfo threeWaySwitchDatcomInfo;
        private ThreeWaySwitch threeWaySwitch;
        private MHEControllerAUS_Case casePLCcontroller;

        //These lists are used to store the values routing codes and they are converted to List<int[]> by
        //using MHEControllerAUS_Case.ValidateRoutingCode()
        public List<string> leftRoutes = null;
        public List<string> rightRoutes = null;
        public List<string> centerRoutes = null;

        //Lookup table for helping with routeLoad
        private Dictionary<ThreeWayRoutes, ThreeWaySwitch.StraightThrough> threeWayRoutes = new Dictionary<ThreeWayRoutes, ThreeWaySwitch.StraightThrough>();

        public MHEControl_ThreeWaySwitch(ThreeWaySwitchDatcomAusInfo info, ThreeWaySwitch _threeWaySwitch)
        {
            Info                     = info;  // set this to save properties 
            threeWaySwitchDatcomInfo = info;
            threeWaySwitch           = _threeWaySwitch;
            casePLCcontroller                  = _threeWaySwitch.Controller as MHEControllerAUS_Case;
            RightRoutingCode = info.rightRoutingCode;
            LeftRoutingCode = info.leftRoutingCode;
            CenterRoutingCode = info.centerRoutingCode;

            threeWayRoutes.Add(ThreeWayRoutes.Left, threeWaySwitch.LeftConv);
            threeWayRoutes.Add(ThreeWayRoutes.Right, threeWaySwitch.RightConv);
            threeWayRoutes.Add(ThreeWayRoutes.Center, threeWaySwitch.CenterConv);

            threeWaySwitch.OnDivertCompleteController        += threeWaySwitch_OnDivertCompleteController;
            threeWaySwitch.OnArrivedAtTransferController     += threeWaySwitch_OnArrivedAtTransferController;
            threeWaySwitch.OnTransferStatusChangedController += threeWaySwitch_OnTransferStatusChangedController;
        }

        void threeWaySwitch_OnTransferStatusChangedController(object sender, EventArgs e)
        {
           // throw new NotImplementedException();
        }

        public void threeWaySwitch_OnArrivedAtTransferController(object sender, ThreeWayArrivalArgs e)
        {
            Case_Load caseLoad = e._load as Case_Load;

            if(casePLCcontroller.DivertSet(caseLoad.SSCCBarcode, leftRoutes))
            {
                threeWayRoutes[e._fromSide].RouteLoad(threeWayRoutes[e._fromSide], threeWaySwitch.LeftConv, e._load);
            }
            else if(casePLCcontroller.DivertSet(caseLoad.SSCCBarcode, rightRoutes))
            {
                threeWayRoutes[e._fromSide].RouteLoad(threeWayRoutes[e._fromSide], threeWaySwitch.RightConv, e._load);
            }
            else if (casePLCcontroller.DivertSet(caseLoad.SSCCBarcode, centerRoutes))
            {
                threeWayRoutes[e._fromSide].RouteLoad(threeWayRoutes[e._fromSide], threeWaySwitch.CenterConv, e._load);
            }
            else //No routing found send 02
            {
                casePLCcontroller.SendDivertConfirmation(((StraightConveyor)threeWayRoutes[e._fromSide].PreviousConveyor).Name, e._load.Identification);
            }
        }

        void threeWaySwitch_OnDivertCompleteController(object sender, ThreeWayDivertedArgs e)
        {
         //   throw new NotImplementedException();
        }

        public override void Dispose()
        {
            throw new NotImplementedException();
        }

        [DisplayName("Right Routing Code")]
        [Description("Routing code for rhs routing: format destination1,destination2,...,destionation n")]
        [PropertyOrder(7)]
        public string RightRoutingCode
        {
            get { return threeWaySwitchDatcomInfo.rightRoutingCode; }
            set
            {
                if (value == "")
                {
                    threeWaySwitchDatcomInfo.rightRoutingCode = null;
                    return;
                }

                List<string> routes = casePLCcontroller.ValidateRoutingCode(value);
                if (routes != null)
                {
                    rightRoutes = routes;
                    threeWaySwitchDatcomInfo.rightRoutingCode = value;
                }
            }
        }

        [DisplayName("Left Routing Code")]
        [Description("Routing code for lhs routing: format destination1,destination2,...,destionation n")]
        [PropertyOrder(7)]
        public string LeftRoutingCode
        {
            get { return threeWaySwitchDatcomInfo.leftRoutingCode; }
            set
            {
                if (value == "")
                {
                    threeWaySwitchDatcomInfo.leftRoutingCode = null;
                    return;
                }

                List<string> routes = casePLCcontroller.ValidateRoutingCode(value);
                if (routes != null)
                {
                    leftRoutes = routes;
                    threeWaySwitchDatcomInfo.leftRoutingCode = value;
                }
            }
        }

        [DisplayName("Center Routing Code")]
        [Description("Routing code for lhs routing: format destination1,destination2,...,destionation n")]
        [PropertyOrder(7)]
        public string CenterRoutingCode
        {
            get { return threeWaySwitchDatcomInfo.centerRoutingCode; }
            set
            {
                if (value == "")
                {
                    threeWaySwitchDatcomInfo.centerRoutingCode = null;
                    return;
                }

                List<string> routes = casePLCcontroller.ValidateRoutingCode(value);
                if (routes != null)
                {
                    centerRoutes = routes;
                    threeWaySwitchDatcomInfo.centerRoutingCode = value;
                }
            }
        }

        [DisplayName("Left Route Location")]
        [Description("Location name in divert confirmation message when successfully diverted, if blank then no divert confirmation message will be sent")]
        [PropertyOrder(10)]
        public string LeftRoutingLocation
        {
            get { return threeWaySwitchDatcomInfo.leftRoutingLocation; }
            set
            {
                if (value == "")
                {
                    threeWaySwitchDatcomInfo.leftRoutingLocation = null;
                }
                else
                {
                    threeWaySwitchDatcomInfo.leftRoutingLocation = value;
                }
            }
        }

        [DisplayName("Right Route Location")]
        [Description("Location name in divert confirmation message when successfully diverted, if blank then no divert confirmation message will be sent")]
        [PropertyOrder(10)]
        public string RightRoutingLocation
        {
            get { return threeWaySwitchDatcomInfo.rightRoutingLocation; }
            set
            {
                if (value == "")
                    threeWaySwitchDatcomInfo.rightRoutingLocation = null;
                else
                {
                    threeWaySwitchDatcomInfo.rightRoutingLocation = value;
                }
            }
        }

        [DisplayName("Center Route Location")]
        [Description("Location name in divert confirmation message when successfully diverted, if blank then no divert confirmation message will be sent")]
        [PropertyOrder(10)]
        public string CenterRoutingLocation
        {
            get { return threeWaySwitchDatcomInfo.centerRoutingLocation; }
            set
            {
                if (value == "")
                    threeWaySwitchDatcomInfo.centerRoutingLocation = null;
                else
                {
                    threeWaySwitchDatcomInfo.centerRoutingLocation = value;
                }
            }
        }
    }

    [Serializable]
    [XmlInclude(typeof(ThreeWaySwitchDatcomAusInfo))]
    public class ThreeWaySwitchDatcomAusInfo : ProtocolInfo
    {
        public string leftRoutingCode;
        public string rightRoutingCode;
        public string centerRoutingCode;
        public string leftRoutingLocation;
        public string rightRoutingLocation;
        public string centerRoutingLocation;
    }
}