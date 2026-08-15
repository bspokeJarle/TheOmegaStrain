using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Domain;
using Microsoft.VSDiagnostics;

namespace TheOmegaStrain.Benchmarks;
[CPUUsageDiagnoser]
public class RotateMeshBenchmarks
{
    private const int TriangleCount = 2048;
    private readonly OmegaMeshRotation _rotation = new();
    private List<ITriangleMeshWithColor> _mesh = new();
    [GlobalSetup]
    public void Setup()
    {
        _mesh = new List<ITriangleMeshWithColor>(TriangleCount);
        for (int i = 0; i < TriangleCount; i++)
        {
            float offset = i * 0.1f;
            _mesh.Add(new TriangleMeshWithColor { vert1 = new Vector3 { x = offset, y = offset + 1f, z = offset + 2f }, vert2 = new Vector3 { x = offset + 3f, y = offset + 4f, z = offset + 5f }, vert3 = new Vector3 { x = offset + 6f, y = offset + 7f, z = offset + 8f }, Color = "FFFFFF" });
        }
    }

    [Benchmark]
    public List<ITriangleMeshWithColor> RotateMesh()
    {
        return _rotation.RotateMesh(_mesh, 30.0, 'Y');
    }
}
