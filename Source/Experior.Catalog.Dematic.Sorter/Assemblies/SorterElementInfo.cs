using System;
using Experior.Core.Assemblies;
using Experior.Core.Properties.Collections;
using Experior.Core.Routes;
using Environment = Experior.Core.Environment;
using Experior.Dematic.Base;

namespace Experior.Catalog.Dematic.Sorter.Assemblies
{
    [Serializable]
    public class SorterElementInfo : AssemblyInfo, IControllableInfo
    {
        public SorterElement.SorterTypes SorterType = SorterElement.SorterTypes.TiltTray;
        public bool UseNumberOfCarriers { get; set; }
        public float EndFixLocalRoll { get; set; }
        public float StartFixLocalRoll { get; set; }
        public float EndFixLocalPitch { get; set; }
        public float StartFixLocalPitch { get; set; }
        public float SorterElementLength { get; set; }
        public float CarrierHeightOffset { get; set; }
        public float Sorterspeed { get; set; }
        public ActionPoint.Edges LoadCarrierPosition { get; set; }
        public float SorterWidth { get; set; }
        public int NumberOfCarriers { get; set; }
        public float Carrierwidth { get; set; }
        public float CarrierHeight { get; set; }
        public float CarrierLength { get; set; }
        public float CarrierSpacing { get; set; }
        public bool TiltTrays { get; set; }
        public float TiltTrayAngle { get; set; }
        public float TiltTrayDistance { get; set; }
        public float LoadOffsetOnCarrier { get; set; }
        public bool SorterMasterElement { get; set; }
        public float HeightDifference { get; set; }
        public float CurveRadius { get; set; }
        public float CurveDegrees { get; set; }
        public float Inductionlength { get; set; }
        public Environment.Revolution Revolution { get; set; }
        public bool VisibleCarriers { get; set; }
        public ExpandablePropertyList<SorterElementFixPoint> FixPoints = new ExpandablePropertyList<SorterElementFixPoint>();

        public SorterElementInfo()
        {
            //Default values
            SorterElementLength = 4;
            SorterWidth = 1.1f;
            CarrierHeightOffset = 0.05f;
            Carrierwidth = 0.5f;
            CarrierLength = 0.65f;
            CarrierHeight = 0.02f;
            CarrierSpacing = 0.15f;
            TiltTrayAngle = (float)(15 / 180f * Math.PI); //15 degrees
            TiltTrayDistance = 1;
            Sorterspeed = 2;
            LoadCarrierPosition = ActionPoint.Edges.Center;
            CurveRadius = 3.555f;
            CurveDegrees = 90;
            Inductionlength = 1;
            VisibleCarriers = true;
        }

        #region IControllableInfo

        private string controllerName = "No Controller";
        public string ControllerName
        {
            get { return controllerName; }
            set { controllerName = value; }
        }

        private ProtocolInfo protocolInfo;
        public ProtocolInfo ProtocolInfo
        {
            get { return protocolInfo; }
            set { protocolInfo = value; }
        }

        #endregion
    }
}
