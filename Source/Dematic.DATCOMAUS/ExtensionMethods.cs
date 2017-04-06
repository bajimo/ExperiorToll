using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Dematic.DATCOMAUS
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
        public static string AppendField(this string Value, IDATCOMAUSTelegrams controller, TelegramFields FieldName, string FieldValue)
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


        public static string SetFieldValue(this string Value, IDATCOMAUSTelegrams Controller, TelegramFields FieldName, string FieldValue)
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
        /// <param name="Controller">The controller that has recived the telegram</param>
        /// <param name="FieldName">Name of the field value to be returned</param>
        /// <returns></returns>
        public static string GetFieldValue(this string Value, IDATCOMAUSTelegrams Controller, TelegramFields FieldName)
        {

            TelegramTypes type = Value.GetTelegramType(Controller);
            if (type != TelegramTypes.Unknown)
            {
                try
                {
                    TelegramTemplate.Telegram data = Controller.Template.GetTelegramData(type);
                    int position = (data.FieldList.FindIndex(x => x == FieldName));
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
        public static TelegramTypes GetTelegramType(this string Value, IDATCOMAUSTelegrams Controller)
        {
            return Controller.Template.TelegramType(Value);
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

        //public static Brush StatusColor(this AvailabilityStatus Value)
        //{
        //    switch (Value)
        //    {
        //        case AvailabilityStatus.Automatic: return Brushes.LimeGreen;
        //        case AvailabilityStatus.Fault: return Brushes.OrangeRed;
        //        case AvailabilityStatus.Manual: return Brushes.Orange;
        //        case AvailabilityStatus.NoOperation: return Brushes.LightGray;
        //        case AvailabilityStatus.Off: return Brushes.DarkGray;
        //    }
        //    return null;
        //}
    }
}
