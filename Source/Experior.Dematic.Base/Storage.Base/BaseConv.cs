using System;
using Microsoft.DirectX;
using System.Drawing;
using System.ComponentModel;
using Experior.Core.Loads;
using Experior.Core.Parts;
using Experior.Core.Routes;
using Experior.Core.Properties;
using System.Text.RegularExpressions;
using Experior.Core.TransportSections;
using System.Collections.Generic;
using Experior.Catalog.Logistic.Track;

namespace Experior.Dematic.Storage.Base {
                                     

    public enum Course {Right,Left, Straight};

    public class HandshakeMessage {

        public enum MessageTypes {
            Pallet_Received_From_Crane,
            Pallet_Removed_From_Conv,
            Tote_Arrived,
            Tote_Ready_To_Miniload,
            Tote_Removed_From_Conv,
            Tote_Arrived_On_Dropstation
        }

        public MessageTypes MessageType {
            get;
            set;
        }
        public Load Load {
            get;
            set;
        }
    }
     
    public class BaseConv : BaseTrack {

       // public static Dictionary<PLCStates, Color> StatusColour = new Dictionary<PLCStates, Color>();
        protected ActionPoint ap, apTrail;

        public ActionPoint AP { get { return ap; } }

        static BaseConv() {
            //BaseConv.StatusColour.Add(PLCStates.Unknown_00, Color.Gainsboro);
            //BaseConv.StatusColour.Add(PLCStates.Ready_02, Color.Yellow);
            //BaseConv.StatusColour.Add(PLCStates.Auto_No_Move_03, Color.Orange);
            //BaseConv.StatusColour.Add(PLCStates.Auto_04, Color.Green);
        }

        public BaseConv(BaseInfo info) : base(info) {}

        [Serializable]
      //  public class BaseInfo : BaseTrackInfo
       // {
        public class BaseInfo : Experior.Core.Assemblies.Logistic.RouteInfo {
                 
            public float theLength, theWidth, theHeight, theDepth, occ_Position, convSpeed;
           // public FixPoint startFixPoint;
            public BaseInfo(): base() {}                           
        }

        protected static Match ValidNameMatches(string name) {
            Regex theRegex;
            Match MatchGroups;
            theRegex = new Regex(@"(?<plcnum>^[0-9]+)(?<AreaFunc>[A-Z]{2})(?<convnum>[0-9]+$)");
            MatchGroups = theRegex.Match(name);
            return MatchGroups;
        }

        public static void Print(string txt, Color colour) {
            Experior.Core.Environment.Log.Write(txt, colour);
        }

        public static void Print(string txt) {
            Experior.Core.Environment.Log.Write(txt, Color.Black);
        }

        public static bool IsValidName(string name) {
            //checks the match group for 4 matches
            // ValidNameMatches should return PLC number AND Area and Function Groups AND conveyor Number AND zeroth element
            //from this we evalulate true or false...
            Match MatchGroups;
            MatchGroups = ValidNameMatches(name);

            if(MatchGroups.Groups.Count == 4) {
                return true;
            }
            return false;
        }

        public new FixPoint StartFixPoint
        {
            get { return base.StartFixPoint; }
        }

        public new FixPoint EndFixPoint
        {
            get { return base.EndFixPoint; }
        }

        //public FixPoint LeftFixPoint {
        //    get {
        //        return base.LeftFixPoint;
        //    }
        //}

        //public FixPoint RightFixPoint {
        //    get {
        //        return base.RightFixPoint;
        //    }
        //}

        [Category("Configuration")]
        [DisplayName("Depth")]
        public virtual float Depth {
            get {
                return ((BaseInfo)Info).theDepth;
            }
            set {
                ((BaseInfo)Info).theDepth = value;
                if(TransportSection is CurveTransportSection) {
                    ((Experior.Core.TransportSections.CurveTransportSection)TransportSection).Height = value;
                }
                else {
                    ((Experior.Core.TransportSections.StraightTransportSection)TransportSection).Height = value;
                }
            }
        }

        [Category("Configuration")]
        [DisplayName("Length")]
        public virtual float Length {
            get {
                return ((BaseInfo)Info).theLength;
            }
            set {
                ((BaseInfo)Info).theLength = value;
                if(TransportSection is Experior.Core.TransportSections.StraightTransportSection) {
                    float delta = (value - ((Experior.Core.TransportSections.StraightTransportSection)TransportSection).Length) / 2;
                    ((Experior.Core.TransportSections.StraightTransportSection)TransportSection).Length = value;
                    StartFixPoint.LocalPosition = new Vector3(StartFixPoint.LocalPosition.X + delta, StartFixPoint.LocalPosition.Y, StartFixPoint.LocalPosition.Z);
                    EndFixPoint.LocalPosition = new Vector3(EndFixPoint.LocalPosition.X - delta, EndFixPoint.LocalPosition.Y, EndFixPoint.LocalPosition.Z);
                    OCC_Position = OCC_Position;
                }
            }
        }

        [Category("Configuration")]
        [DisplayName("Width")]
        [PropertyOrder(2)]
        public virtual float TheWidth {
            get {
                return ((BaseInfo)Info).theWidth;
            }
            set {
                ((BaseInfo)Info).theWidth = value;
                if (TransportSection is Experior.Core.TransportSections.CurveTransportSection)
                {
                    ((Experior.Core.TransportSections.CurveTransportSection)TransportSection).Width = value;
                }
                else if (TransportSection is Experior.Core.TransportSections.StraightTransportSection) {
                    ((Experior.Core.TransportSections.StraightTransportSection)TransportSection).Width = value;                       
                }
            }
        }


        [Category("Configuration")]
        [DisplayName("Occupied Position")]
        [PropertyOrder(6)]
        public virtual float OCC_Position {
            get {
                return ((BaseInfo)Info).occ_Position;
            }
            set {
                ((BaseInfo)Info).occ_Position = value;
                if(ap != null) {
                    ap.Distance = Length - value;
                }
                if(apTrail != null) {                   
                    apTrail.Distance = Length - value;
                }                
                Experior.Core.Environment.Properties.Refresh();
            }
        }
       
        [Category("Configuration")]
        [DisplayName("Height")]
        [PropertyOrder(3)]
        public float TheHeight {
            get {
                ((BaseInfo)Info).theHeight = this.Position.Y;
                return ((BaseInfo)Info).theHeight;
            }
            set {
                ((BaseInfo)Info).theHeight = value;
                this.Position = new Vector3(this.Position.X, value, this.Position.Z);
                //((Experior.Core.TransportSections.StraightTransportSection)TransportSection).Position = new Vector3(this.Position.X, value, this.Position.Z);
                Experior.Core.Environment.Properties.Refresh();
            }
        }

        [Category("Configuration")]
        [DisplayName("Speed")]
        [PropertyOrder(3)]
        public virtual float ConvSpeed {
            get {
                return ((BaseInfo)Info).convSpeed;
            }
            set {
                ((BaseInfo)Info).convSpeed = value;
                TransportSection.Route.Motor.Speed = value;
            }
        }



        public override string Name {
            get {
                return base.Name;
            }
            set {
           //     if(IsValidName(value.ToUpper())) {
                    base.Name = value.ToUpper();
                //}
                //else {
                //    Experior.Core.Environment.Log.Write("Incorect Naming convention used", "Location Naming Error", Color.Red);
                //}
            }
        }

        public override string Category {
            get {return "Base";}                            
        }

        public override Image Image {
            get { return null;}                           
        }
    }

}