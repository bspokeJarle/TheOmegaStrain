namespace Domain
{
    public partial class _3dSpecificsImplementations
    {
        public class TriangleMeshWithColor : TriangleMesh, ITriangleMeshWithColor
        {
            public string? Color { get; set; }
        }
    }
}
