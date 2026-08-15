using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;
using Microsoft.VSDiagnostics;
using TheOmegaStrain.Game.World.Objects;

namespace TheOmegaStrain.Benchmarks;
[CPUUsageDiagnoser]
public class SurfaceViewportBenchmarks
{
    private const int BenchmarkMapSize = 128;
    private Surface _surface = null!;
    [GlobalSetup]
    public void Setup()
    {
        MapSetup.maxHeight = 400;
        GameState.ObjectIdCounter = 1;
        GameState.ShipState = new ShipState
        {
            ShipObjectOffsets = new Vector3
            {
                x = 0f,
                y = 0f,
                z = 0f
            },
            ShipHasShadow = true
        };
        GameState.SurfaceState = new SurfaceState
        {
            Global2DMap = CreateMap(),
            GlobalMapPosition = new Vector3
            {
                x = SurfaceSetup.DefaultMapPosition.x,
                y = 25f,
                z = SurfaceSetup.DefaultMapPosition.z
            },
            SurfaceViewportObject = new OmegaObject3D
            {
                ObjectId = -1,
                ObjectName = "SurfaceViewport",
                ObjectOffsets = new Vector3
                {
                    x = 0f,
                    y = 0f,
                    z = 0f
                },
                Rotation = new Vector3
                {
                    x = 0f,
                    y = 0f,
                    z = 0f
                },
                WorldPosition = new Vector3
                {
                    x = 0f,
                    y = 0f,
                    z = 0f
                },
                CrashBoxes = new List<List<IVector3>>()
            },
            AiObjects = CreateAiObjects(),
            ScreenEcoMetas = new ScreenEcoMeta[MapSetup.screensPrMap, MapSetup.screensPrMap]
        };
        _surface = new Surface();
    }

    [Benchmark]
    public I3dObject GetSurfaceViewPort()
    {
        return _surface.GetSurfaceViewPort();
    }

    private static SurfaceData[, ] CreateMap()
    {
        var map = new SurfaceData[BenchmarkMapSize, BenchmarkMapSize];
        for (int y = 0; y < BenchmarkMapSize; y++)
        {
            for (int x = 0; x < BenchmarkMapSize; x++)
            {
                int depth = ((x * 17) + (y * 31)) % MapSetup.maxHeight;
                bool hasCrashBox = ((x + y) % 11) == 0;
                map[y, x] = new SurfaceData
                {
                    mapDepth = depth,
                    mapId = (y * BenchmarkMapSize) + x,
                    hasLandbasedObject = ((x * y) % 9) == 0,
                    isInfected = ((x + y) % 13) == 0,
                    crashBox = hasCrashBox ? new SurfaceData.CrashBoxData
                    {
                        width = 1 + (x % 2),
                        height = 1 + (y % 2),
                        boxDepth = 20 + (depth % 30)
                    }

                    : null
                };
            }
        }

        return map;
    }

    private static List<OmegaObject3D> CreateAiObjects()
    {
        var globalMapPosition = GameState.SurfaceState.GlobalMapPosition;
        var aiObjects = new List<OmegaObject3D>(4);
        for (int i = 0; i < 4; i++)
        {
            aiObjects.Add(new OmegaObject3D { ObjectId = 100 + i, ObjectName = $"ShadowCaster{i}", WorldPosition = new Vector3 { x = globalMapPosition.x + 120f + (i * 60f), y = 0f, z = globalMapPosition.z + 80f + (i * 45f) }, ObjectOffsets = new Vector3 { x = 0f, y = 0f, z = 0f }, Rotation = new Vector3 { x = 0f, y = 0f, z = 0f }, CrashBoxes = new List<List<IVector3>>(), HasShadow = true });
        }

        return aiObjects;
    }
}
