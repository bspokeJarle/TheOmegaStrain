using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Microsoft.VSDiagnostics;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Common.OmegaEngineAdapters;

namespace TheOmegaStrain.Benchmarks;
[CPUUsageDiagnoser]
public class DeepCopySingleObjectBenchmarks
{
    private OmegaObject3D _source = null !;
    [GlobalSetup]
    public void Setup()
    {
        var triangles = new List<ITriangleMeshWithColor>(32);
        for (int i = 0; i < 32; i++)
        {
            float offset = i * 0.5f;
            triangles.Add(new TriangleMeshWithColor { vert1 = new Vector3(offset, offset + 1f, offset + 2f), vert2 = new Vector3(offset + 3f, offset + 4f, offset + 5f), vert3 = new Vector3(offset + 6f, offset + 7f, offset + 8f), normal1 = new Vector3(0.1f, 0.2f, 0.3f), normal2 = new Vector3(0.2f, 0.3f, 0.4f), normal3 = new Vector3(0.3f, 0.4f, 0.5f), angle = 0.5f, Color = "FFFFFF", noHidden = false, landBasedPosition = i });
        }

        _source = new OmegaObject3D
        {
            ObjectId = 1,
            ObjectName = "BenchmarkObject",
            ObjectOffsets = new Vector3(1f, 2f, 3f),
            Rotation = new Vector3(4f, 5f, 6f),
            WorldPosition = new Vector3(7f, 8f, 9f),
            CrashBoxes = new List<List<IVector3>>
            {
                new List<IVector3>
                {
                    new Vector3(1f, 2f, 3f),
                    new Vector3(4f, 5f, 6f)
                }
            },
            ObjectParts = new List<I3dObjectPart>
            {
                new OmegaObjectPart3D
                {
                    PartName = "Main",
                    IsVisible = true,
                    Triangles = triangles
                }
            }
        };
    }

    [Benchmark]
    public I3dObject DeepCopySingleObject()
    {
        return OmegaObjectHelpers.DeepCopySingleObject(_source);
    }
}
