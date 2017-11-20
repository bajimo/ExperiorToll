using Experior.Catalog.Dematic.Case.Components;
using Experior.Core.Assemblies;
using Experior.Dematic.Base;
using System;

namespace Experior.Catalog.Dematic.Case
{
    public class Create
    {
        public static Assembly BeltCurve(string title, string subtitle, object properties)
        {
            CurveBeltConveyorInfo curveBeltInfo = new CurveBeltConveyorInfo();
            curveBeltInfo.name = Assembly.GetValidName("Case Belt Curve ");

            curveBeltInfo.thickness = 0.05f;
            curveBeltInfo.spacing = 0.1f;
            curveBeltInfo.speed = 0.7f;
            curveBeltInfo.width = 0.5f;
            curveBeltInfo.height = 0.7f;

            if (subtitle.Contains("Counter"))
            {
                curveBeltInfo.revolution = Core.Environment.Revolution.Counterclockwise;
                curveBeltInfo.yaw = (float)Math.PI;
            }
            else
            {
                curveBeltInfo.revolution = Core.Environment.Revolution.Clockwise;
            }

            curveBeltInfo.radius = 1.015f;
            return new CurveBeltConveyor(curveBeltInfo);
        }

        public static Assembly StraightBelt(string title, string subtitle, object properties)
        {
            StraightBeltConveyorInfo straightBeltinfo = new StraightBeltConveyorInfo();
            //straightBeltinfo.Length = StandardCase.DefaultSpecs.DefaultLength; //For the plug-in
            straightBeltinfo.Length = 2;
            straightBeltinfo.thickness = 0.05f;
            straightBeltinfo.speed = 0.7f;
            straightBeltinfo.width = 0.5f;  //TODO width should use enum...same for all
            straightBeltinfo.height = 0.7f;
            straightBeltinfo.name = Assembly.GetValidName("Belt ");
            return new StraightBeltConveyor(straightBeltinfo);
        }

        public static Assembly StraightAccumulation(string title, string subtitle, object properties)
        {
            StraightAccumulationConveyorInfo straightAccumulationinfo = new StraightAccumulationConveyorInfo();

            straightAccumulationinfo.thickness = 0.05f;
            straightAccumulationinfo.speed = 0.7f;
            straightAccumulationinfo.width = 0.5f;
            straightAccumulationinfo.height = 0.7f;
            straightAccumulationinfo.Positions = 3;
            straightAccumulationinfo.LineFullPosition = 2;
            straightAccumulationinfo.name = Assembly.GetValidName("RSPN ");
            return new StraightAccumulationConveyor(straightAccumulationinfo);
        }

        public static Assembly Roller(string title, string subtitle, object properties)
        {
            StraightConveyorInfo info = new StraightConveyorInfo();
            info.thickness = 0.05f;
            info.spacing = 0.1f;
            info.width = 0.5f;
            info.length = 0.5f;
            info.height = 0.7f;
            info.speed = 0.7f;
            info.name = Assembly.GetValidName("RA ");
            return new StraightConveyor(info);
        }

        public static Assembly GravityConveyor(string title, string subtitle, object properties)
        {
            var info = new GravityConveyorInfo();
            info.name = Assembly.GetValidName("GV ");
            info.FillPercent = 85;
            info.thickness = 0.05f;
            info.spacing = 0.0f;
            info.width = 0.5f;
            info.length = 1.5f;
            info.height = 0.7f;
            info.speed = 0.7f;
            return new GravityConveyor(info);
        }

        public static Assembly RotateLoad(string title, string subtitle, object properties)
        {
            LoadRotateConveyorInfo loadrotateconveyorinfo = new LoadRotateConveyorInfo();
            loadrotateconveyorinfo.thickness = 0.05f;
            return new LoadRotateConveyor(loadrotateconveyorinfo);
        }

        public static Assembly AngledDivert(string title, string subtitle, object properties)
        {
            AngledDivertInfo angledDivertInfo = new AngledDivertInfo()
            {
                thickness = 0.05f,
                width = 0.5f,
                length = 1,
                DivertConveyorLength = 0.7f,
                height = 0.7f,
                speed = 0.7f,
                divertSide = Side.Left,
                DivertConveyorOffset = 0.5f,
                type = subtitle,
                name = Assembly.GetValidName("Angled Divert "),
                divertAngle = (float)(Math.PI / 4),

            };

            return new AngledDivert(angledDivertInfo);
        }

        public static Assembly AngledMerge(string title, string subtitle, object properties)
        {
            AngledMergeInfo angledMergeInfo = new AngledMergeInfo()
            {
                length = 1.5f,
                thickness = 0.05f,
                speed = 0.7f,
                width = 0.5f,
                height = 0.7f,
                mergeConveyorOffset = 0.9f,
                mergeSide = Side.Left,
                mergeConveyorLength = 0.7f,
                name = Assembly.GetValidName("AngledMerge "),
                mergeAngle = (float)(Math.PI / 4),
                MergeDelayTime = 5,
                StraightDelayTime = 6
            };

            return new AngledMerge(angledMergeInfo);
        }

        public static Assembly BeltSorter(string title, string subtitle, object properties)
        {
            BeltSorterMergeInfo beltSorterMergeInfo = new BeltSorterMergeInfo()
            {
                length = 1,
                thickness = 0.05f,
                speed = 0.7f,
                width = 0.5f,
                height = 0.7f,
                mergeConveyorOffset = 0.5f,
                mergeSide = Side.Left,
            };

            if (subtitle == "Angled Merge")
            {
                beltSorterMergeInfo.type = MergeType.Angled;
                beltSorterMergeInfo.mergeConveyorLength = 0.7f;
                beltSorterMergeInfo.name = Assembly.GetValidName("SorterMerge ");
                beltSorterMergeInfo.mergeAngle = (float)(Math.PI / 4);
                return new BeltSorterMerge(beltSorterMergeInfo);
            }
            else if (subtitle == "Pop Up Merge")
            {
                beltSorterMergeInfo.type = MergeType.PopUp;
                beltSorterMergeInfo.mergeConveyorLength = beltSorterMergeInfo.width / 2;
                beltSorterMergeInfo.name = Assembly.GetValidName("PopUpMerge ");
                beltSorterMergeInfo.mergeAngle = (float)(Math.PI / 2);
                return new BeltSorterMerge(beltSorterMergeInfo);
            }
            else if (subtitle == "Induct")
            {
                //straightBeltinfo.Length = StandardCase.DefaultSpecs.DefaultLength; //For the plug-in

                BeltSorterInductInfo straightBeltinfo = new BeltSorterInductInfo()
                {
                    Length = 0.5f,
                    thickness = 0.05f,
                    speed = 0.7f,
                    width = 0.5f,
                    height = 0.7f,
                    windowSize = 1,
                    name = Assembly.GetValidName("SorterInduct ")
                };
                return new BeltSorterInduct(straightBeltinfo);
            }
            else if (subtitle == "Pop Up Divert" || subtitle == "Angled Divert")
            {
                BeltSorterDivertInfo beltSorterDivertInfo = new BeltSorterDivertInfo()
                {
                    thickness = 0.05f,
                    width = 0.5f,
                    length = 1,
                    divertConveyorLength = 0.7f,
                    height = 0.7f,
                    speed = 0.7f,
                    divertSide = Side.Left,
                    divertConveyorOffset = 0.5f
                };

                if (subtitle == "Pop Up Divert")
                {
                    beltSorterDivertInfo.type = DivertType.PopUp;
                    beltSorterDivertInfo.divertConveyorLength = beltSorterDivertInfo.width / 2;
                    beltSorterDivertInfo.name = Assembly.GetValidName("Pop Up Divert ");
                    beltSorterDivertInfo.divertAngle = (float)(Math.PI / 2);
                    return new BeltSorterDivert(beltSorterDivertInfo);
                }
                else if (subtitle == "Angled Divert")
                {
                    beltSorterDivertInfo.type = DivertType.Angled;
                    beltSorterDivertInfo.name = Assembly.GetValidName("Angled Divert ");
                    beltSorterDivertInfo.divertAngle = (float)(Math.PI / 4);
                    return new BeltSorterDivert(beltSorterDivertInfo);
                }
            }
            return null;
        }

        public static Assembly Transfer(string title, string subtitle, object properties)
        {
            TransferInfo transferInfo = new TransferInfo();
            transferInfo.height = 0.7f;
            //transferInfo.speed = 0.7f;

            if (subtitle == "DHDM")
            {
                transferInfo.width = 1.5f;
                transferInfo.length = 1.5f;
                transferInfo.type = TransferType.DHDM;
                transferInfo.name = Assembly.GetValidName("DHDM ");
                return new Transfer(transferInfo);
            }
            else if (subtitle == "2 Way")
            {
                transferInfo.length = 0.5625f;
                transferInfo.width = 1.15f;
                transferInfo.internalConvWidth = CaseConveyorWidth._500mm;

                transferInfo.type = TransferType.TwoWay;
                transferInfo.name = Assembly.GetValidName("2Way ");
                return new Transfer(transferInfo);
            }
            //else if (subtitle == "3 Way")
            //{
            //    ThreeWaySwitchInfo threeWayInfo = new ThreeWaySwitchInfo();
            //    threeWayInfo.height = 0.7f;
            //    threeWayInfo.length = 3;
            //    threeWayInfo.width = 6;
            //    //transferInfo.internalConvWidth = CaseConveyorWidth._500mm;

            //    transferInfo.name = Experior.Core.Assemblies.Assembly.GetValidName("3Way ");
            //    return new ThreeWaySwitch(threeWayInfo);
            //}
            else if (subtitle == "Diverter/Merger")
            {
                MergeDivertConveyorInfo mergerdiverterinfo = new MergeDivertConveyorInfo();
                mergerdiverterinfo.thickness = 0.05f;
                mergerdiverterinfo.spacing = 0.1f;
                mergerdiverterinfo.width = 0.5f;
                mergerdiverterinfo.length = 0.75f;
                mergerdiverterinfo.height = 0.7f;
                mergerdiverterinfo.speed = 0.7f;
                mergerdiverterinfo.name = Assembly.GetValidName("Merge Divert ");
                return new MergeDivertConveyor(mergerdiverterinfo);
            }
            else if (subtitle == "Transfer Plate")
            {
                StraightTransferPlateInfo transferPlateInfo = new StraightTransferPlateInfo();
                transferPlateInfo.thickness = 0.05f;
                transferPlateInfo.speed = 0.7f;
                transferPlateInfo.width = 0.75f;
                transferPlateInfo.height = 0.7f;
                transferPlateInfo.length = 0.11f;
                transferPlateInfo.name = Assembly.GetValidName("Transfer Plate ");
                return new StraightTransferPlate(transferPlateInfo);
            }
            else if (subtitle == "Divert Transfer Plate")
            {
                DivertTransferPlateInfo transferPlateInfo = new DivertTransferPlateInfo();
                transferPlateInfo.thickness = 0.05f;
                transferPlateInfo.speed = 0.7f;
                transferPlateInfo.width = 0.5f;
                transferPlateInfo.height = 0.7f;
                transferPlateInfo.length = 0.75f;
                transferPlateInfo.speed = 0.7f;
                transferPlateInfo.divertConveyorLength = transferPlateInfo.width / 2;
                transferPlateInfo.name = Assembly.GetValidName("Divert Transfer Plate ");
                return new DivertTransferPlate(transferPlateInfo);
            }
            else if (subtitle == "Two-To-One Merge")
            {
                TwoToOneMergeInfo _221Info = new TwoToOneMergeInfo();
                _221Info.height = 0.7f;
                _221Info.width = 1;
                _221Info.internalConvWidth = CaseConveyorWidth._500mm;
                _221Info.name = Assembly.GetValidName("Two2One ");

                return new TwoToOneMerge(_221Info);
            }
            return null;
        }

        public static Assembly Miniload(string title, string subtitle, object properties)
        {
            if (subtitle == "Pick Station")
            {
                MiniloadPickStationInfo miniloadPickStationInfo = new MiniloadPickStationInfo()
                {
                    length = 1,
                    thickness = 0.05f,
                    speed = 0.7f,
                    width = 0.5f,
                    height = 0.7f,
                    //mergeConveyorOffset = 0.5f,
                    feedSide = Side.Left,
                };

                //beltSorterMergeInfo.type = MiniloadPickStation.MergeType.PopUp;
                //beltSorterMergeInfo.mergeConveyorLength = beltSorterMergeInfo.width / 2;
                miniloadPickStationInfo.name = Assembly.GetValidName("Miniload PS ");
                //beltSorterMergeInfo.mergeAngle          = (float)(Math.PI / 2);
                return new MiniloadPickStation(miniloadPickStationInfo);
            }
            else if (subtitle == "Drop Station")
            {
                MiniloadDropStationInfo miniloadDropStationInfo = new MiniloadDropStationInfo()
                {
                    length = 1,
                    thickness = 0.05f,
                    speed = 0.7f,
                    width = 0.5f,
                    height = 0.7f,
                    //mergeConveyorOffset = 0.5f,
                    receiveSide = Side.Left,
                };

                //beltSorterMergeInfo.type = MiniloadPickStation.MergeType.PopUp;
                //beltSorterMergeInfo.mergeConveyorLength = beltSorterMergeInfo.width / 2;
                miniloadDropStationInfo.name = Assembly.GetValidName("Miniload DS ");
                //beltSorterMergeInfo.mergeAngle          = (float)(Math.PI / 2);
                return new MiniloadDropStation(miniloadDropStationInfo);
            }
            return null;
        }

        public static Assembly TrayStacker(string title, string subtitle, object properties)
        {
            TrayStackerInfo info = new TrayStackerInfo
            {
                StackLimit = 8,
                TrayHeight = 0.058f,
                TrayLength = 0.65f,
                spacing = 0.1f,
                length = 1,
                thickness = 0.05f,
                speed = 0.5f,
                width = 0.5f,
                height = 0.7f,
            };
            return new TrayStacker(info);
        }

        public static Assembly TrayDestacker(string title, string subtitle, object properties)
        {
            TrayDestackerInfo info = new TrayDestackerInfo
            {
                StackLimit = 8,
                TrayHeight = 0.058f,
                TrayLength = 0.65f,
                spacing = 0.1f,
                length = 1,
                thickness = 0.05f,
                speed = 0.5f,
                width = 0.5f,
                height = 0.7f,
            };
            return new TrayDestacker(info);
        }

        public static Assembly PickDoubleLift(string title, string subtitle, object properties)
        {
            PickDoubleLiftInfo info = new PickDoubleLiftInfo
            {
                thickness = 0.05f,
                spacing = 0.1f,
                width = 0.5f,
                length = 2.25f,
                height = 0.7f,
                speed = 0.7f,
                name = Assembly.GetValidName("GTPL ")

            };
            return new PickDoubleLift(info);
        }

        public static Assembly PickPutStation(string title, string subtitle, object properties)
        {
            var info = new PickPutStationInfo
            {
                //thickness = 0.05f,
                //spacing = 0.1f,
                width = 0.5f,
                length = 2.25f,
                height = 0.7f,
                //speed = 0.7f,
                name = Assembly.GetValidName("GTP ")
            };
            return new PickPutStation(info);
        }

        //public static Assembly ConveyorUnits(string title, string subtitle, object properties)
        //{
        //    StraightAccumulationConveyorUnitsInfo straightAccumulationUnitsinfo = new StraightAccumulationConveyorUnitsInfo();
        //    straightAccumulationUnitsinfo.thickness = 0.05f;
        //    straightAccumulationUnitsinfo.speed = 0.7f;
        //    straightAccumulationUnitsinfo.width = 0.5f;
        //    straightAccumulationUnitsinfo.height = 0.7f;
        //    straightAccumulationUnitsinfo.name = Experior.Core.Assemblies.Assembly.GetValidName("CU ");
        //    return new StraightAccumulationConveyorUnits(straightAccumulationUnitsinfo);
        //}
    }
}

