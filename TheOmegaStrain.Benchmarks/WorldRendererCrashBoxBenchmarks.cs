using BenchmarkDotNet.Attributes;
using Microsoft.VSDiagnostics;
using TheOmegaStrain.Wpf.Rendering;

namespace TheOmegaStrain.Benchmarks;
[CPUUsageDiagnoser]
public class WorldRendererCrashBoxBenchmarks
{
    private string[] _partNames = null !;
    [GlobalSetup]
    public void Setup()
    {
        _partNames = new string[2048];
        for (int i = 0; i < _partNames.Length; i++)
        {
            _partNames[i] = i % 2 == 0 ? "CrashBox-Test" : "Ship";
        }
    }

    [Benchmark]
    public int CountCrashBoxes()
    {
        return WorldRenderer.CountCrashBoxParts(_partNames);
    }
}
