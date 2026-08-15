using RetroMesh.Engine;
namespace TheOmegaStrain.Domain
{
    public class TriangleMesh : EngineTriangleMesh
    {
        protected override IVector3 CreateVector() => new Vector3();
    }
}
