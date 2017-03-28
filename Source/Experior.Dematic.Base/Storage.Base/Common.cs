using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;

namespace Experior.Dematic.Storage.Base
{


    /// <summary>
    /// Used to show a list of avialable BK10 PLCs  
    /// </summary>
    public class BK10PLCConverter : StringConverter
    {
        public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
        {
            //true means show a combobox
            return true;
        }

        public override bool GetStandardValuesExclusive(ITypeDescriptorContext context)
        {
            //true will limit to list. false will show the list, but allow free-form entry
            return false;
        }

        public override
            System.ComponentModel.TypeConverter.StandardValuesCollection
            GetStandardValues(ITypeDescriptorContext context)
        {
            return new StandardValuesCollection((from plc in Common.BK10PLCs select plc.Name).ToList<string>());
        }

    }

    public class Common
    {
        public delegate void Nofication(IBK10PLCCommon sender);
        public delegate void PickStationStatus(IBK10PLCCommon sender, string crane, bool[] pickStationStation);
        public delegate void FailedToDivert(IBK10PLCCommon sender, string location, string barcode);

        public static List<IBK10PLCCommon> BK10PLCs = new List<IBK10PLCCommon>();

        //public static string GetPLCStateString(MultiShuttlePLC_State theState)
        //{
        //    if (theState == MultiShuttlePLC_State.Unknown_00) return "00";
        //    // else if(theState == PLCStates.Program_Started_01) return "01";                
        //    else if (theState == MultiShuttlePLC_State.Ready_02) return "02";
        //    else if (theState == MultiShuttlePLC_State.Auto_No_Move_03) return "03";
        //    else /*if(theState == PLCState.Auto_04)*/return "04";
        //}

    }

}
