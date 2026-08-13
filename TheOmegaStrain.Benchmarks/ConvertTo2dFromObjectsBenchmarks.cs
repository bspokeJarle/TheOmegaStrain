using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Microsoft.VSDiagnostics;
using _3dTesting._3dRotation;
using _3dTesting._Coordinates;
using Domain;
using CommonUtilities.CommonGlobalState;
using static Domain._3dSpecificsImplementations;

namespace TheOmegaStrain.Benchmarks;
[CPUUsageDiagnoser]
public class ConvertTo2dFromObjectsBenchmarks
{
    private _3dTo2d _converter = null !;
    private List<_3dObject> _objects = null !;
    [GlobalSetup]
    public void Setup()
    {
        _converter = new _3dTo2d();
        _objects = new List<_3dObject>(128);
        GameState.SurfaceState.GlobalMapPosition = new Vector3
        {
            x = 0,
            y = 0,
            z = 0
        };
        for (int i = 0; i < 128; i++)
        {
            var triangle = new TriangleMeshWithColor
            {
                vert1 = new Vector3
                {
                    x = -25,
                    y = 0,
                    z = -25
                },
                vert2 = new Vector3
                {
                    x = 25,
                    y = 0,
                    z = -25
                },
                vert3 = new Vector3
                {
                    x = 0,
                    y = 50,
                    z = 25
                },
                normal1 = new Vector3
                {
                    x = 0,
                    y = 0,
                    z = 1
                },
                angle = 0.5f,
                Color = "FFFFFF",
                noHidden = true
            };
            _objects.Add(new _3dObject { ObjectId = i, ObjectName = "BenchmarkObject", WorldPosition = new Vector3 { x = 0, y = 0, z = 0 }, ObjectOffsets = new Vector3 { x = 0, y = 0, z = 0 }, Rotation = new Vector3 { x = 0, y = 0, z = 0 }, ObjectParts = new List<I3dObjectPart> { new _3dObjectPart { PartName = "Main", IsVisible = true, Triangles = new List<ITriangleMeshWithColor> { triangle } } }, CrashBoxes = new List<List<IVector3>>() });
        }
    }

    [Benchmark]
    public List<_2dTriangleMesh> ConvertTo2dFromObjects()
    {
        return _converter.ConvertTo2dFromObjects(_objects, 1);
    }
}
