using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Microsoft.VSDiagnostics;
using TheOmegaStrain.Wpf.Rendering;
using TheOmegaStrain.Domain;
using System.Windows.Media;

namespace TheOmegaStrain.Benchmarks;
[CPUUsageDiagnoser]
public class WorldRendererProcessTrianglesBenchmarks
{
    private List<ProjectedTriangleMesh> _triangles = null !;
    private Dictionary<(float, string), Color> _colorCache = null !;
    private Dictionary<Color, SolidColorBrush> _brushCache = null !;
    private Dictionary<Color, Pen> _penCache = null !;
    [GlobalSetup]
    public void Setup()
    {
        _triangles = new List<ProjectedTriangleMesh>(2048);
        for (int i = 0; i < 2048; i++)
        {
            _triangles.Add(new ProjectedTriangleMesh { X1 = i, Y1 = i + 1, X2 = i + 2, Y2 = i + 3, X3 = i + 4, Y3 = i + 5, CalculatedZ = i % 2000, TriangleAngle = 0.5f, Color = "FFFFFF", PartName = i % 2 == 0 ? "CrashBox-Test" : "Ship" });
        }

        _colorCache = new Dictionary<(float, string), Color>();
        _brushCache = new Dictionary<Color, SolidColorBrush>();
        _penCache = new Dictionary<Color, Pen>();
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _colorCache.Clear();
        _brushCache.Clear();
        _penCache.Clear();
    }

    [Benchmark]
    public int ProcessTrianglesForRender()
    {
        return WorldRenderer.ProcessTrianglesForRender(_triangles, _colorCache, _brushCache, _penCache);
    }
}
