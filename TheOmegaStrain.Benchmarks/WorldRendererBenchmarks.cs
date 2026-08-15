using System.Collections.Generic;
using System.Threading;
using System.Windows.Threading;
using BenchmarkDotNet.Attributes;
using Microsoft.VSDiagnostics;
using TheOmegaStrain.Wpf;
using TheOmegaStrain.Wpf.Rendering;
using TheOmegaStrain.Domain;

namespace TheOmegaStrain.Benchmarks;
[CPUUsageDiagnoser]
[InProcess]
public class WorldRendererBenchmarks
{
    private const int TriangleCount = 2048;
    private Thread _uiThread = null !;
    private Dispatcher _dispatcher = null !;
    private DrawingVisualHost _host = null !;
    private WorldRenderer _renderer = null !;
    private List<ProjectedTriangleMesh> _triangles = null !;

    [GlobalSetup]
    public void Setup()
    {
        _triangles = new List<ProjectedTriangleMesh>(TriangleCount);
        for (int i = 0; i < TriangleCount; i++)
        {
            _triangles.Add(new ProjectedTriangleMesh { X1 = i, Y1 = i + 1, X2 = i + 2, Y2 = i + 3, X3 = i + 4, Y3 = i + 5, CalculatedZ = i % 2000, TriangleAngle = 0.5f, Color = "FFFFFF", PartName = i % 2 == 0 ? "CrashBox-Test" : "Ship" });
        }

        using var ready = new ManualResetEventSlim(false);
        _uiThread = new Thread(() =>
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            ready.Set();
            Dispatcher.Run();
        });
        _uiThread.SetApartmentState(ApartmentState.STA);
        _uiThread.IsBackground = true;
        _uiThread.Start();
        ready.Wait();
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _dispatcher.Invoke(() =>
        {
            _host = new DrawingVisualHost();
            _renderer = new WorldRenderer(_host);
        });
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        _dispatcher.Invoke(() =>
        {
            _host = null !;
            _renderer = null !;
        });
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _dispatcher.InvokeShutdown();
        _uiThread.Join();
    }

    [Benchmark]
    public void RenderTriangles()
    {
        _dispatcher.Invoke(() => _renderer.RenderTriangles(_triangles));
    }
}
