using Microsoft.DirectX;

namespace Experior.Catalog.Dematic.Sorter.Assemblies
{
    public class TrackTransformation
    {
        public Vector3 Position { get; set; }
        public Matrix Orientation { get; set; }
        public Matrix Transformation { get; set; }
        public Vector3 Direction { get; set; }
    }
}