using Experior.Catalog.Dematic.Case.Components;
using Experior.Core.Assemblies;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;

namespace Experior.Catalog.Dematic.Case.Devices
{
    public class InductConveyorConverter : StringConverter
    {
        public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
        {
            return true; //true means show a combobox
        }

        public override bool GetStandardValuesExclusive(ITypeDescriptorContext context)
        {
            return false;//true will limit to list. false will show the list, but allow free-form entry
        }

        public override System.ComponentModel.TypeConverter.StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
        {
            List<string> inducts = new List<string>();

            try
            {
                foreach (Assembly assembly in Core.Assemblies.Assembly.Items.Values)
                {
                    if (assembly is BeltSorterInduct)
                    {
                        inducts.Add(assembly.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                Core.Environment.Log.Write("PhotocellConverter combodropdown error", Color.Red);
                Core.Environment.Log.Write(ex.Message, Color.Red);
            }
            return new StandardValuesCollection(inducts);
        }
    }
}
