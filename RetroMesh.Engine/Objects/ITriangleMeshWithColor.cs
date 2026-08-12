namespace Domain
{
    public interface ITriangleMeshWithColor : ITriangleMesh
    {
        string? Color { get; set; }
    }
}
