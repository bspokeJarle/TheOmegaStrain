using BenchmarkDotNet.Attributes;
using Microsoft.VSDiagnostics;
using System.Windows.Media;
using _3dTesting.Rendering;

namespace TheOmegaStrain.Benchmarks;
[CPUUsageDiagnoser]
public class CrashBoxBrushBenchmarks
{
    private Color _color;
    private SolidColorBrush? _lastBrush;
    [GlobalSetup]
    public void Setup()
    {
        _color = Color.FromArgb(255, 120, 200, 255);
    }

    [IterationSetup]
    public void IterationSetup()
    {
        WorldRenderer.ClearCrashBoxBrushCache();
    }

    [Benchmark]
    public SolidColorBrush CreateCrashBoxBrush()
    {
        _lastBrush = WorldRenderer.CreateCrashBoxBrush(_color);
        return _lastBrush;
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        _lastBrush = null;
    }
}
