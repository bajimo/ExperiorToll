using Experior.Core.Assemblies;
using Experior.Core.Parts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using Microsoft.DirectX;

namespace Experior.Dematic.Base
{
    public class DematicArrow : Arrow
    {
        public DematicArrow(Assembly theAssembly, float width) : base(width / 2, 0.07f)
        {
            this.Color = Color.Green;
            theAssembly.Add(this);
        }

        public override void Render(bool shadow)
        {
            base.Render(false);
        }
    }
}
