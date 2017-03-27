
using System.ComponentModel;

namespace Experior.Catalog.Dematic.Case
{
    public enum CaseConveyorWidth
    {
        _400mm = 400,
        M_415mm = 415,
        _500mm = 500,
        M_515mm = 515,
        _600mm = 600,
        M_615mm = 615,
        _750mm = 750,
        M_765mm = 765,
        _900mm = 900,
        M_915mm = 915,
        _Custom,
    }

    public enum CaseOrientation
    {
        Auto,
        WidthLeading,
        LengthLeading
    }

    public enum TransferType
    {
        TwoWay,
        DHDM
    }

    public enum AccumulationPitch
    {
        [Description("375 mm")]
        _375mm = 375,
        [Description("500 mm")]
        _500mm = 500,
        [Description("625 mm")]
        _625mm = 625,
        [Description("750 mm")]
        _750mm = 750,
        [Description("875 mm")]
        _875mm = 875,
        [Description("1000 mm")]
        _1000mm = 1000,
        [Description("1125 mm")]
        _1125mm = 1125,
        [Description("1250 mm")]
        _1250mm = 1250,
        [Description("Custom")]
        _Custom = 0
    }

    public enum OutfeedLength
    {
        _0mm = 0,
        _125mm = 125,
        _250mm = 250,
        _375mm = 375
    }

    public enum AngledDivertLocalControl
    {
        Round_Robin,
        Route_To_Default,
        None
    }

    public enum Arrived
    {
        Straight,
        Merge
    }

    public enum DivertType
    {
        PopUp,
        Angled
    }

    public enum MergeType
    {
        PopUp,
        Angled
    }

    public enum ControlTypesSubSet
    {
        Test,
        Local
    }

    //public enum DivertDirections
    //{
    //    DivertStraight,
    //    DivertLeft,
    //    DivertRight
    //}
}
