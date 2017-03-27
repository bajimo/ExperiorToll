using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Experior.Core;
using Experior.Core.Assemblies;
using System.ComponentModel;
using Experior.Core.Properties.TypeConverter;
using Experior.Core.Properties;
using Experior.Core.Routes;

namespace Experior.Catalog
{
    [TypeConverter(typeof(ObjectConverter))]
    [Description("Script for building High Level Controller logic")]

    public class ControllerExtended : Controller
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        public delegate void AnyEventEvent(string data, object theObj);

        [EditorBrowsable(EditorBrowsableState.Never)]
        public static event AnyEventEvent __ProfileChangeEvent, __DataArrivalEvent, __ControllerOpenEvent;

        public ControllerExtended(string text)
            : base(text)
        {
            __ProfileChangeEvent += new AnyEventEvent(_ProfileChangeEvent);
            __DataArrivalEvent += new AnyEventEvent(_DataArrivalEvent);
            __ControllerOpenEvent += new AnyEventEvent(_ControllerOpenEvent);
        }


        private void _DataArrivalEvent(string data, object theObj)
        {
            // event method
            if (!Enable) { return; }
            ConvDataArrival(data, theObj);
        }

        private void _ProfileChangeEvent(string data, object theObj)
        {
            // event method
            if (!Enable) { return; }
            ProfileChange(data, theObj);
        }

        private void _ControllerOpenEvent(string data, object theObj)
        {
            if (!Enable) { return; }
            ControllerOpen(data, theObj);
        }

        public static void ProfileChangeEvent(string data, object theObj)
        {
            //Called from Assembly code
            try
            {
                if (__ProfileChangeEvent != null)
                {
                    __ProfileChangeEvent(data, theObj);
                }
            }
            catch { }
        }

        public static void ConvDataArrivalEvent(string data, object theObj)
        {
            //Called from Assembly code
            try
            {
                if (__DataArrivalEvent != null)
                {
                    __DataArrivalEvent(data, theObj);
                }
            }
            catch { }
        }

        public static void ControllerOpenEvent(string data, object theObj)
        {
            //Called from Assembly code
            try
            {
                if (__ControllerOpenEvent != null)
                {
                    __ControllerOpenEvent(data, theObj);
                }
            }
            catch
            {
            }
        }

        //Overridden in script
        public virtual void ProfileChange(string data, object theObj) { }
        public virtual void ConvDataArrival(string data, object theObj) { }
        public virtual void ControllerOpen(string data, object theObj)
        {
        }



        public override void Dispose()
        {
            base.Dispose();
            __ProfileChangeEvent -= new AnyEventEvent(_ProfileChangeEvent);
            __DataArrivalEvent -= new AnyEventEvent(_DataArrivalEvent);
        }
    }
}
