namespace RetroMesh.Engine
{
    public interface ITriangleMeshWithColor : ITriangleMesh
    {
        string? Color { get; set; }
    }
}
