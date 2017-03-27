using System;
using System.ComponentModel;
using System.Xml.Serialization;
using Experior.Core.Properties;
using Experior.Catalog.Dematic.Case.Components;
using Experior.Catalog.Dematic.Case;

namespace Experior.Catalog.Dematic.Custom.Components
{
    /// <summary>
    /// Created for the conveyors from conveyor-Units
    /// </summary>
    public class StraightAccumulationConveyorUnits : StraightAccumulationConveyor
    {
        private StraightAccumulationConveyorUnitsInfo conveyorUnitsInfo;

        public StraightAccumulationConveyorUnits(StraightAccumulationConveyorUnitsInfo info): base(info)
        {
            conveyorUnitsInfo    = info;
            OutfeedSectionLength = info.outFeedSectionLength;
            AccumulationPitch    = info.accumulationPitch;
        }

        [Category("Configuration")]
        [DisplayName("Accumulation Pitch (mm)")]
        [Description("Pitch of the accumulation conveyor places (mm).")]
        [PropertyOrder(0)]
        public virtual int AccumulationPitch
        {
            get { return conveyorUnitsInfo.accumulationPitch; }
            set
            {
                conveyorUnitsInfo.accumulationPitch = value;
                pitch = value;
                SetAccumulationLength(false);
            }
        }

        [Category("Configuration")]
        [DisplayName("Outfeed Section Length (mm)")]
        [Description("Length of the outfeed roller section in front of the first accumulation position (mm).")]
        [PropertyOrder(2)]
        public virtual int OutfeedSectionLength
        {
            get { return conveyorUnitsInfo.outFeedSectionLength; }
            set
            {
                conveyorUnitsInfo.outFeedSectionLength = value;
                outfeedSection = value;
                SetAccumulationLength(false);
            }
        }

        [Category("Configuration")]
        [DisplayName("Infeed Section Length (mm)")]
        [Description("Length of the infeed roller secrion before the last accumulation position (mm).")]
        [PropertyOrder(3)]
        public int InfeedSectionMM
        {
            get { return conveyorUnitsInfo.infeedSection; }
            set
            {
                conveyorUnitsInfo.infeedSection = value;
                straightAccumulationinfo.InfeedSection = (float)value/1000;
                SetAccumulationLength(false);
            }
        }


        [Browsable(false)]
        public override float InfeedSection
        {
            get{ return base.InfeedSection; }
            set{ base.InfeedSection = value;}
        }

        [Browsable(false)]
        public override AccumulationPitch AccPitch
        {
            get { return base.AccPitch; }
            set { base.AccPitch = value; }
        }

        [Browsable(false)]
        public override OutfeedLength OutfeedSection
        {
            get{return base.OutfeedSection;}
            set{base.OutfeedSection = value;}
        }

        public override string Category
        {
            get { return "Conveyor Units"; }
        }

    }

    [Serializable]
    [XmlInclude(typeof(StraightAccumulationConveyorUnitsInfo))]
    public class StraightAccumulationConveyorUnitsInfo : StraightAccumulationConveyorInfo
    {
        public int accumulationPitch = 1000;
        public int outFeedSectionLength = 0;
        public int infeedSection = 0;
    }
}