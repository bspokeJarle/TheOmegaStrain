namespace Domain
{
    public partial class _3dSpecificsImplementations
    {
        public class TriangleMesh : EngineTriangleMesh
        {
            protected override IVector3 CreateVector() => new Vector3();
        }
    }
}
