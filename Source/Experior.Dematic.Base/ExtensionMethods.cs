using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Experior.Dematic.Base;
using Environment = Experior.Core.Environment;
using System.Collections;
using System.Drawing;

namespace Experior.Dematic.Base
{

    public static class ExtensionMethods
    {
        public static int AisleNumber(this string value)
        {
            if (value != null)
            {
                int aisleNumber = 0;
                int.TryParse(value.Substring(0, 2), out aisleNumber);
                return aisleNumber;
            }
            return 0;
        }

        public static RackSide Side(this string value)
        {
          //  return value.Substring(2, 1);
            int sideStringLocation; 
            int result;
            if (int.TryParse(value.Substring(0, 1), out result))
            {
                sideStringLocation = 2;
            }
            else
            {
                sideStringLocation = 0;
            }

            foreach (var eValue in Enum.GetValues(typeof(RackSide)))
            {
                if (value.Substring(sideStringLocation, 1).ToCharArray().First() == (char)(RackSide)eValue)
                {
                    return (RackSide)eValue;
                }
            }

            return RackSide.NA;
        }

        /// <summary>
        /// Finds the level from location
        /// </summary>
        public static int LevelasInt(this string value)
        {
            if (value != null)
            {
                int result;
                if (int.TryParse(value.Substring(0, 1), out result))  // takes the form aasyyxz: a=aisle, s = side, y = level, x = input or output, Z = loc A or B e.g. 01R05OA
                {
                    int level = 0;
                    int.TryParse(value.Substring(3, 2), out level);
                    return level;
                }
                else // takes the form  sxxxyydd: Side, xxx location, yy = level, dd = depth
                {
                    int level = 0;
                    int.TryParse(value.Substring(4, 2), out level);
                    return level;
                }
            }
            return 0;
        }

        /// <summary>
        /// Finds the level from location
        /// </summary>     
        /// <returns>The level padded to to characters</returns>
        public static string Level(this string value)
        {
            if (value != null)
            {

                if (char.IsLetter(value.Substring(0, 1)[0]))  // takes the form  sxxxyydd: Side, xxx location, yy = level, dd = depth 
                {
                    return value.Substring(4, 2);
                }
                return value.Substring(3, 2);                 // takes the form aasyyxz: a=aisle, s = side, y = level, x = input or output, Z = loc A or B e.g. 01R05OA
            }
            return string.Empty;
        }

        public static ConveyorTypes ConvType(this string value)
        {
            if (value != null)
            {
                foreach (var eValue in Enum.GetValues(typeof(ConveyorTypes)))
                {
                    if (value.Substring(5, 1).ToCharArray().First() == (char)(ConveyorTypes)eValue)
                    {
                        return (ConveyorTypes)eValue;
                    }
                }
            }
            return ConveyorTypes.NA;
        }

        /// <summary>
        /// A or B
        /// </summary>
        public static string ConvPosition(this string value)
        {
            return value.Substring(6, 1);
        }

        /// <summary>
        /// returns the destination location for a particular shuttle
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static float RackXLocation(this string value)
        {
            if (value != null)
            {
                int result;

                if (char.IsLetter(value.Substring(0, 1)[0]))  // if takes the form  sxxxyydd: s = Side (L or R), xxx location, yy = level, dd = depth
                {
                    int.TryParse(value.Substring(1, 3), out result);
                    return (float)result;
                }
            }
            return 0;
        }

        /// <summary>
        /// Returns true if location is a rack location
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool IsRackBinLocation(this string value)
        {
            if (value != null)
            {
                return char.IsLetter(value.Substring(0, 1)[0]);
            }
            return false;
        }

        /// <summary>
        /// returns the depth to drop the load in the rack either 1 or 2
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static int LoadDepth(this string value) 
        {            
            int result = 0;
            try
            {
                if (int.TryParse(value.Substring(0, 1), out result))  // takes the form aasyyxz: a=aisle, s = side, y = level, x = input or output, Z = loc A or B e.g. 01R05OA
                {
                    return 0; // The form aasyyxz has no depth information
                }
                else // takes the form  sxxxyydd: Side, xxx location, yy = level, dd = depth
                {
                    if (value.Substring(6, 2).ToUpper() == "IA")
                    {
                        result = -1;
                    }
                    else
                    {
                        int.TryParse(value.Substring(6, 2), out result);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Write("ERROR in extension method LoadDepth " + ex.Message, Color.Red);
            }

 
            return result ;
        }

        /// <summary>
        /// Determines whether two float values are within a set tolerance
        /// </summary>
        /// <param name="value1"></param>
        /// <param name="value2"></param>
        /// <param name="acceptableDifference">The tolerance</param>
        /// <returns></returns>
        public static bool WithinRange(this float value1, float value2, float acceptableDifference)
        {
            return Math.Abs(value1 - value2) <= acceptableDifference;
        }

    }
}
