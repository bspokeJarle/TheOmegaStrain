namespace TheOmegaStrain.Domain
{
    public class TriangleMeshWithColor : TriangleMesh, ITriangleMeshWithColor
    {
        public string? Color { get; set; }
    }
}
