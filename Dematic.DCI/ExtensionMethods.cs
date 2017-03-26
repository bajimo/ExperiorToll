using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Dematic.DCI
{
    /// <summary>
    /// String extension methods
    /// </summary>
    public static class Strings
    {
        /// <summary>
        /// Append field to the end of the message (End of Message will be dealt with)
        /// </summary>
        /// <param name="Value"></param>
        /// <param name="FieldName">Not used but useful to read the code</param>
        /// <param name="FieldValue">Value to be inserted into the telegram</param>
        /// <returns></returns>
        public static string AppendField(this string Value, IDCITelegrams controller, TelegramFields FieldName, string FieldValue)
        {
            return null;
        }

        /// <summary>
        /// Set the value of a field within a message
        /// </summary>
        /// <param name="Value"></param>
        /// <param name="Controller">The controller that is sending the telegram</param>
        /// <param name="FieldName">Name of the field to be set</param>
        /// <param name="FieldValue">Value of the field to be set</param>
        /// <returns></returns>


        public static string SetFieldValue(this string Value, IDCITelegrams Controller, TelegramFields FieldName, string FieldValue)
        {
            TelegramTypes type = Value.GetTelegramType(Controller);
            TelegramTemplate.Telegram data = Controller.Template.GetTelegramData(type);
            int position = data.FieldList.FindIndex(x => x == FieldName);
            string[] telegram = Value.Split(',');
            telegram[position] = FieldValue;
            return string.Join(",", telegram);
        }

        /// <summary>
        /// Returns the string value of a field within a DCI telegram message
        /// </summary>
        /// <param name="Value"></param>
        /// <param name="Controller">The controller that has received the telegram</param>
        /// <param name="FieldName">Name of the field to be returned</param>
        /// <returns></returns>
        public static string GetFieldValue(this string Value, IDCITelegrams Controller, TelegramFields FieldName)
        {
            return Value.GetFieldValue(Controller, FieldName, 0);
        }

        /// <summary>
        /// Returns the string value of a field within a DCI telegram message
        /// </summary>
        /// <param name="Value"></param>
        /// <param name="Controller">The controller that has recived the telegram</param>
        /// <param name="FieldName">Name of the field value to be returned</param>
        /// <param name="BlockPosition">For blocked messages, which block within the telegram (zero based i.e first block = 0)</param>
        /// <returns></returns>
        public static string GetFieldValue(this string Value, IDCITelegrams Controller, TelegramFields FieldName, int BlockPosition)
        {

            if (BlockPosition > Value.GetNumberOfBlocks(Controller)) { return null; }
            TelegramTypes type = Value.GetTelegramType(Controller);
            if (type != TelegramTypes.Unknown)
            {
                try
                {
                    TelegramTemplate.Telegram data = Controller.Template.GetTelegramData(type);
                    int position = (data.FieldList.FindIndex(x => x == FieldName)) + ((data.FieldList.Count - 10) * (BlockPosition));
                    string[] splitValue = Value.Split(',');
                    if (position >= splitValue.Length)
                    {
                        return null;
                    }
                    else
                    {
                        return Value.Split(',')[position];
                    }
                }
                catch //(Exception ex)
                {
                    return null;
                }
            }

            else
            {
                return null;
            }


        }

        /// <summary>
        /// Find the telegram type from the message
        /// </summary>
        /// <param name="Value"></param>
        /// <param name="telegrams">The telegram template being used</param>
        /// <returns></returns>
        public static TelegramTypes GetTelegramType(this string Value, IDCITelegrams Controller)
        {
            return Controller.Template.TelegramType(Value);
        }

        public static int GetNumberOfBlocks(this string Value, IDCITelegrams Controller)
        {
            int result = 0;
            if (int.TryParse(Controller.Template.NumberOfBlocks(Value), out result))
            {
                return result;
            }
            else
            {
                return 1;
            }
        }

        /// <summary>
        /// Return the side of a location string (returns Left as default)
        /// </summary>
        /// <param name="Value"></param>
        /// <returns></returns>
        public static RackSide Side(this string Value)
        {
            if (Value.Length == 10)
            {
                if (Value.GetInteger(0, 1) == 2) return RackSide.Right;
            }

            if (Value.Length == 14)
            {
                LocationTypes type = Value.LocationType();

                if (type == LocationTypes.BinLocation)
                {
                    if (Value.GetInteger(4, 1) == 2) return RackSide.Right;
                }
                else if (type == LocationTypes.RackConvOut || type == LocationTypes.RackConvIn || type == LocationTypes.PickStation || type == LocationTypes.DropStation || type == LocationTypes.Lift)
                {
                    if (Value.Substring(7, 1) == "R") return RackSide.Right;
                }
            }
            return RackSide.Left;
        }

        /// <summary>
        /// Return the X Location of a location string (returns 0 if not valid)
        /// </summary>
        /// <param name="Value"></param>
        /// <returns></returns>
        public static int XLoc(this string Value)
        {
            return Value.GetInteger(1, 3);
        }

        /// <summary>
        /// Return the Y Location of a location string (returns 0 if not valid)
        /// </summary>
        /// <param name="Value"></param>
        /// <returns></returns>
        public static int YLoc(this string Value)
        {
            if (Value.Length == 10)
            {
                return Value.GetInteger(4, 2);
            }
            else //Value is 14
            {
                return Value.GetInteger(8, 2);
            }
        }

        public static int RackLevel(this string Value)
        {
            return YLoc(Value);
        }

        /// <summary>
        /// Return the Depth of a location string (returns 0 if not valid)
        /// </summary>
        /// <param name="Value"></param>
        /// <returns></returns>
        public static int Depth(this string Value)
        {
            return Value.GetInteger(6, 2);
        }

        /// <summary>
        /// Return the Raster within the location string (returns 0 if not valid)
        /// </summary>
        /// <param name="Value"></param>
        /// <returns></returns>
        public static int Raster(this string Value)
        {
            return Value.GetInteger(8, 1);
        }

        /// <summary>
        /// Return the Position within the Raster of a location string (returns 0 if not valid)
        /// </summary>
        /// <param name="Value"></param>
        /// <returns></returns>
        public static int Pos(this string Value)
        {
            return Value.GetInteger(9, 1);
        }

        private static int GetInteger(this string Value, int StartIndex, int Length)
        {
            if (Value.Length >= StartIndex + Length)
            { 
                int result = 0;
                if (int.TryParse(Value.Substring(StartIndex, Length), out result))
                {
                    return result;
                }
            }
            return 0;
        }

        /// <summary>
        /// returns an enum indicating the type of location that the location string represents
        /// </summary>
        public static LocationTypes LocationType(this string value)
        {
            var isMS = false;
            if (value.Length == 14 && value.Substring(0, 2) == "MS" )
            {
                isMS = true;
                value = value.Substring(6, 8);
            }
            if (value.Length == 14 && (value.Substring(0, 2) == "ML" || value.Substring(0, 2) == "UL"))
            {
                value = value.Substring(6, 8);
            }

            if (value.Length != 8)
            {
                return LocationTypes.None;
            }
            char type = value.Substring(4, 1)[0];
            char dir = value.Substring(5, 1)[0];
            if (char.IsLetter(type))
            {
                switch (type)
                {
                    case 'R':
                        {
                            if (dir == 'I') { return LocationTypes.RackConvIn; }
                            else { return LocationTypes.RackConvOut; }
                        }
                    case 'L':
                        if (isMS)
                        {
                            return LocationTypes.Lift;
                        }
                        else
                        {
                            return LocationTypes.LHD;
                        }
                    case 'P': return LocationTypes.PickStation;
                    case 'D': return LocationTypes.DropStation;
                    case 'S': return LocationTypes.Shuttle;
                    case 'I': return LocationTypes.BinLocation; //IA
                    default: return LocationTypes.None;
                }
            }
            return LocationTypes.BinLocation; //Assume that it is a bin loc 
        }
        public static EventCodes GetEventCode(this string Value)
        {
            switch (Value)
            {
                case "OK": return EventCodes.Ok;
                case "BO": return EventCodes.BinOccupied;
                case "BE": return EventCodes.BinEmpty;
                case "DN": return EventCodes.DestNotReachable;
                case "SN": return EventCodes.SourceNotReachable;
                case "TH": return EventCodes.TooHigh;
                case "CE": return EventCodes.CurrentNoExist;
                case "DE": return EventCodes.DestNoExist;
                case "DU": return EventCodes.DestUnreachable;
                case "TU": return EventCodes.TUUnknown;
                default: return EventCodes.TUUnknown;
            }
        }

        public static AvailabilityStatus GetAvailabilityStatus(this string Value)
        {
            switch (Value)
            {
                case "AU": return AvailabilityStatus.Automatic;
                case "MA": return AvailabilityStatus.Manual;
                case "FL": return AvailabilityStatus.Fault;
                case "OF": return AvailabilityStatus.Off;
                default: return AvailabilityStatus.NoOperation;
            }
        }


        public static Brush StatusColor(this AvailabilityStatus Value)
        {
            switch (Value)
            {
                case AvailabilityStatus.Automatic: return Brushes.LimeGreen;
                case AvailabilityStatus.Fault: return Brushes.OrangeRed;
                case AvailabilityStatus.Manual: return Brushes.Orange;
                case AvailabilityStatus.NoOperation: return Brushes.LightGray;
                case AvailabilityStatus.Off: return Brushes.DarkGray;
            }
            return null;
        }

        //public static string GetDeviceName(this string Value)
        //{
        //    if (Value.Length == 14)
        //    {
        //        return Value.Substring(6, 8);
        //    }
        //    return null;
        //}

        //public static string GetLocationName(this string Value)
        //{
        //    if (Value.Length == 14)
        //    {
        //        if (Value.LocationType() == LocationTypes.BinLocation)
        //        {
        //            return Value.Substring(4, 10);
        //        }
        //        else
        //        {
        //            return Value.Substring(6, 8);
        //        }
        //    }
        //    return Value;
        //}




        /// <summary>
        /// String array extension methods
        /// </summary>
        public static class StringArrays
        {


        }
    }
}
