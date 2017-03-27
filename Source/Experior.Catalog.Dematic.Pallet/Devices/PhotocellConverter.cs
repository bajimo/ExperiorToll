using Experior.Catalog.Dematic.Pallet.Assemblies;
using Experior.Catalog.Dematic.Pallet.Devices;
using Experior.Core.Assemblies;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;

namespace Experior.Catalog.Devices
{
    public class PhotocellConverter : StringConverter
    {
        public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
        {
            return true; //true means show a combobox
        }

        public override bool GetStandardValuesExclusive(ITypeDescriptorContext context)
        {
            return false;//true will limit to list. false will show the list, but allow free-form entry
        }

        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
        {
            List<string> photocells = new List<string>();

            try
            {
                if (context.Instance is BaseStraight)
                {
                    BaseStraight conveyor = context.Instance as BaseStraight;
                    foreach (Assembly assembly in conveyor.Assemblies)
                    {
                        if (assembly is PalletPhotocell)
                        {
                            photocells.Add(assembly.Name);
                        }
                    }
                }
                else if (context.Instance is PalletStraight)
                {
                    PalletStraight conveyor = context.Instance as PalletStraight;
                    foreach (Assembly assembly in conveyor.Assemblies)
                    {
                        if (assembly is PalletPhotocell)
                        {
                            photocells.Add(assembly.Name);
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                Core.Environment.Log.Write("PhotocellConverter combodropdown error", Color.Red);
                Core.Environment.Log.Write(ex.Message, Color.Red);
            }


            return new StandardValuesCollection(photocells);
        }

    }

    public class AllPhotocellConverter : StringConverter
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
            List<string> photocells = new List<string>();

            try
            {
                foreach (Assembly assembly in Core.Assemblies.Assembly.Items.Values)
                {
                    if (assembly is BaseStraight) // || assembly is BaseStraight)
                    {
                        if (assembly.Assemblies != null)
                        {
                            foreach (Assembly device in assembly.Assemblies)
                            {
                                if (device is PalletPhotocell)
                                {
                                    photocells.Add(string.Format("{0},{1}", assembly.Name, device.Name));
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Core.Environment.Log.Write("PhotocellConverter combodropdown error", Color.Red);
                Core.Environment.Log.Write(ex.Message, Color.Red);
            }
            return new StandardValuesCollection(photocells);
        }


    }
}
