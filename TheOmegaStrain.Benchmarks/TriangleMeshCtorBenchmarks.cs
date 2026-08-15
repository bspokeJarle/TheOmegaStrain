using BenchmarkDotNet.Attributes;
using TheOmegaStrain.Domain;
using Microsoft.VSDiagnostics;

namespace TheOmegaStrain.Benchmarks;
[CPUUsageDiagnoser]
public class TriangleMeshCtorBenchmarks
{
    private Vector3 _vert1 = new();
    private Vector3 _vert2 = new();
    private Vector3 _vert3 = new();
    private Vector3 _normal1 = new();
    private Vector3 _normal2 = new();
    private Vector3 _normal3 = new();
    [GlobalSetup]
    public void Setup()
    {
        _vert1 = new Vector3(1f, 2f, 3f);
        _vert2 = new Vector3(4f, 5f, 6f);
        _vert3 = new Vector3(7f, 8f, 9f);
        _normal1 = new Vector3(0.1f, 0.2f, 0.3f);
        _normal2 = new Vector3(0.4f, 0.5f, 0.6f);
        _normal3 = new Vector3(0.7f, 0.8f, 0.9f);
    }

    [Benchmark]
    public TriangleMesh CreateTriangleMesh()
    {
        return new TriangleMesh
        {
            vert1 = _vert1,
            vert2 = _vert2,
            vert3 = _vert3,
            normal1 = _normal1,
            normal2 = _normal2,
            normal3 = _normal3,
            landBasedPosition = 1,
            angle = 0.5f
        };
    }
}
