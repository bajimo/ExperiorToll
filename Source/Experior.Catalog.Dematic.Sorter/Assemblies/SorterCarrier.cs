using System.ComponentModel;
using System.Drawing;
using Experior.Core.Loads;
using Microsoft.DirectX;
using Mesh = Experior.Core.Parts.Mesh;

namespace Experior.Catalog.Dematic.Sorter.Assemblies
{
    public class SorterCarrier
    {
        public SorterCarrier()
        {
            Enabled = true;
        }

        internal Mesh CarrierMesh;
        internal float CurrentTiltAngle { get; set; }
        internal float StopTiltingDistance { get; set; }
        internal float CurrentLoadOffset { get; set; }
        internal float RenderingOffsetDistance { get; set; }
        internal int RenderingDistance { get; set; }

        /// <summary>
        /// Current distance in mm.
        /// </summary>
        public int CurrentDistance { get; internal set; }
        public object ReservationKey { get; internal set; }
        //Offset distance in mm.
        public float OffsetDistance { get; internal set; }
        public Load CurrentLoad { get; internal set; }
        public float CurrentLoadYaw { get; internal set; }
        public SorterElementFixPoint CurrentDestination { get; internal set; }
        public Vector3 CurrentPosition { get { return Master.Track[CurrentDistance].Position; } }
        public Matrix CurrentOrientation { get { return Master.Track[CurrentDistance].Orientation; } }
        public SorterCarrier Previous { get; internal set; }
        public SorterCarrier Next { get; internal set; }
        public int Index { get; internal set; }
        public string Name { get; set; }
        public SorterElement Master { get; internal set; }
        public object UserData { get; set; }
        public Color Color
        {
            get { return CarrierMesh.Color; }
            set { CarrierMesh.Color = value; }
        }
        [DefaultValue(true)]
        public bool Enabled { get; set; }

        public override string ToString()
        {
            if (Name != string.Empty)
                return Name;

            return "Carrier " + Index;
        }
    }
}