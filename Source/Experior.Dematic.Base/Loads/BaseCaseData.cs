using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;

namespace Experior.Dematic.Base
{
    public class BaseCaseData
    {
        //public Case_Load Parent { get; internal set; }
        public float Length = 0.6f, Width = 0.3f , Height = 0.3f, Weight = 1;
        public uint TrayStacks = 6;
        public Color colour = Color.Peru;
    }

    //This should be controller specific information but that would mean each assembly would need to know it's controller and that is not a great idea.
    //Somehow the assemblies need to be able to do this but they can't at the moment




}
