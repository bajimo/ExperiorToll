using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Experior.Catalog.Dematic.DatcomUK.Assemblies
{
    public static class DatcomExtensionMethods
    {
        #region Extracts fields from the Origin, Current Location, and Destination Fields as defined in Dematic Multi-Shuttle Control Principles 3.7.1.2

        public static string Datcom_Location(this string value)
        {
            return value.Substring(0, 1);
        }

        public static string Datcom_Aisle(this string value)
        {
            return value.Substring(1, 2);
        }

        public static string Datcom_GroupOrDepth(this string value)
        {
            return value.Substring(3, 1);
        }

        public static string Datcom_Side(this string value)
        {
            return value.Substring(4,1);
        }

        public static string Datcom_X_horizontal(this string value)
        {
            return value.Substring(5,3);
        }

        public static string Datcom_Y_Vertical(this string value)
        {
            return value.Substring(8, 2);
        }

        #endregion
    }
}
