using Experior.Core.Assemblies;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Experior.Dematic.Base
{
    /// <summary>
    /// Used to show a list of avialable case conveyor controllers in a drop down box
    /// </summary>
    public class CaseControllerConverter : StringConverter
    {
        public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
        {
            return true;//true means show a combobox

        }

        public override bool GetStandardValuesExclusive(ITypeDescriptorContext context)
        {
            return false;//true will limit to list. false will show the list, but allow free-form entry
        }

        public override System.ComponentModel.TypeConverter.StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
        {
            List<string> controllers = Assembly.Items.Values.OfType<IController>().Select(x => x.Name).ToList();
            controllers.Add("No Controller");
            return new StandardValuesCollection(controllers);
        }
    }
}
