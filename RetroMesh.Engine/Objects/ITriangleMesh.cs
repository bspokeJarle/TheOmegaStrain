namespace Domain
{
    public interface ITriangleMesh
    {
        IVector3 normal1 { get; set; }
        IVector3 normal2 { get; set; }
        IVector3 normal3 { get; set; }
        IVector3 vert1 { get; set; }
        IVector3 vert2 { get; set; }
        IVector3 vert3 { get; set; }
        long? landBasedPosition { get; set; }
        float angle { get; set; }
        bool? noHidden { get; set; }
    }
}
