using Experior.Catalog.Dematic.ATC.Assemblies;
using Experior.Catalog.Dematic.ATC.Assemblies.CaseConveyor;
using Experior.Catalog.Dematic.ATC.Assemblies.PalletConveyor;
using Experior.Catalog.Dematic.ATC.Assemblies.Sorters;
using Experior.Catalog.Dematic.ATC.Assemblies.Storage;
using Experior.Core.Assemblies;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Experior.Catalog.Dematic.ATC
{
    public class Create
    {
        public static Assembly ATCCasePLC(string title, string subtitle, object properties)
        {
            MHEController_CaseATCInfo plcinfo = new MHEController_CaseATCInfo();
            plcinfo.name = Experior.Core.Assemblies.Assembly.GetValidName("PLC ");
            plcinfo.position.Y = 0;
            return new MHEController_Case(plcinfo);
        }

        public static Assembly ATCEmulation(string title, string subtitle, object properties)
        {
            EmulationATCInfo plcinfo = new EmulationATCInfo();
            plcinfo.name = Experior.Core.Assemblies.Assembly.GetValidName("ATC ");
            plcinfo.position.Y = 0;
            return new EmulationATC(plcinfo);
        }

        public static Assembly ATCMultiShuttlePLC(string title, string subtitle, object properties)
        {
            MHEController_MultishuttleATCInfo plcinfo = new MHEController_MultishuttleATCInfo();
            plcinfo.name = Experior.Core.Assemblies.Assembly.GetValidName("MSC ");
            return new MHEController_Multishuttle(plcinfo);
        }

        public static Assembly ATCMiniloadPLC(string title, string subtitle, object properties)
        {
            MHEController_MiniloadATCInfo plcinfo = new MHEController_MiniloadATCInfo();
            plcinfo.name = Experior.Core.Assemblies.Assembly.GetValidName("MLC ");
            return new MHEController_Miniload(plcinfo);
        }

        public static Assembly ATCPalletCranePLC(string title, string subtitle, object properties)
        {
            MHEController_PalletCraneATCInfo plcinfo = new MHEController_PalletCraneATCInfo();
            plcinfo.name = Experior.Core.Assemblies.Assembly.GetValidName("PCC ");
            return new MHEController_PalletCrane(plcinfo);
        }

        public static Assembly ATCSorterPLC(string title, string subtitle, object properties)
        {
            MHEController_SorterATCInfo plcinfo = new MHEController_SorterATCInfo();
            plcinfo.name = Experior.Core.Assemblies.Assembly.GetValidName("SC ");
            return new MHEController_Sorter(plcinfo);
        }

        public static Assembly ATCPalletPLC(string title, string subtitle, object properties)
        {
            MHEController_PalletATCInfo plcinfo = new MHEController_PalletATCInfo();
            plcinfo.name = Experior.Core.Assemblies.Assembly.GetValidName("PLC ");
            plcinfo.position.Y = 0;
            return new MHEController_Pallet(plcinfo);
        }

    }
}
