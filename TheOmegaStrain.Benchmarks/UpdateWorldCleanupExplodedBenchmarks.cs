using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;
using CommonUtilities.CommonSetup;
using Domain;
using Microsoft.VSDiagnostics;
using _3dTesting.MainWindowClasses;
using _3dTesting._3dWorld;
using _3dTesting.Helpers;
using static Domain._3dSpecificsImplementations;

namespace TheOmegaStrain.Benchmarks;
[CPUUsageDiagnoser]
public class UpdateWorldCleanupExplodedBenchmarks
{
    private const int ExplodedObjectCount = 128;
    private GameWorldManager _manager = null!;
    private _3dWorld _world = null!;
    private List<ProjectedTriangleMesh> _screen = null!;
    private List<ProjectedTriangleMesh> _crash = null!;
    [GlobalSetup]
    public void Setup()
    {
        _manager = new GameWorldManager();
        _world = new _3dWorld
        {
            SceneHandler = new StubSceneHandler()
        };
        GameState.PendingWorldObjects.Clear();
        GameState.SurfaceState = new SurfaceState
        {
            AiObjects = new List<_3dObject>(ExplodedObjectCount),
            DirtyTiles = new List<IVector3>(),
            ScreenEcoMetas = new ScreenEcoMeta[MapSetup.screensPrMap, MapSetup.screensPrMap]
        };
        GameState.ShipState = new ShipState
        {
            BestCandidateStates = new List<BestCandidateState>(ExplodedObjectCount)
        };
        _screen = new List<ProjectedTriangleMesh>();
        _crash = new List<ProjectedTriangleMesh>();
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _world.WorldInhabitants.Clear();
        GameState.PendingWorldObjects.Clear();
        GameState.SurfaceState.AiObjects.Clear();
        GameState.SurfaceState.DirtyTiles.Clear();
        GameState.ShipState.BestCandidateStates.Clear();
        var ship = TestObjectFactory.CreateDynamicTestObject();
        ship.ObjectId = 1;
        ship.ObjectName = "Ship";
        ship.WorldPosition = new Vector3(0, 0, 0);
        ship.Rotation = new Vector3();
        ship.ObjectOffsets = new Vector3
        {
            x = 1000,
            y = 0,
            z = 1000
        };
        ship.CrashBoxesFollowRotation = false;
        ship.ImpactStatus = new ImpactStatus
        {
            ObjectName = ship.ObjectName,
            HasExploded = false
        };
        _world.WorldInhabitants.Add(ship);
        var surface = TestObjectFactory.CreateSurfaceBasedTestObject();
        surface.ObjectId = 2;
        surface.ObjectName = "Surface";
        surface.WorldPosition = new Vector3(0, 0, 0);
        surface.Rotation = new Vector3();
        surface.ObjectOffsets = new Vector3
        {
            x = 1000,
            y = 0,
            z = 1000
        };
        surface.CrashBoxesFollowRotation = false;
        surface.ImpactStatus = new ImpactStatus
        {
            ObjectName = surface.ObjectName,
            HasExploded = false
        };
        _world.WorldInhabitants.Add(surface);
        for (int i = 0; i < ExplodedObjectCount; i++)
        {
            var exploded = TestObjectFactory.CreateDynamicTestObject();
            exploded.ObjectId = 1000 + i;
            exploded.ObjectName = $"Exploded{i}";
            exploded.WorldPosition = new Vector3(0, 0, 0);
            exploded.Rotation = new Vector3();
            exploded.ObjectOffsets = new Vector3
            {
                x = 1000 + i,
                y = 0,
                z = 1000 + i
            };
            exploded.CrashBoxesFollowRotation = false;
            exploded.ImpactStatus = new ImpactStatus
            {
                ObjectName = exploded.ObjectName,
                HasExploded = true
            };
            _world.WorldInhabitants.Add(exploded);
            GameState.SurfaceState.AiObjects.Add(exploded);
            GameState.ShipState.BestCandidateStates.Add(new BestCandidateState { BestEnemyCandidate = new EnemyCandidateInfo { EnemyObject = exploded, EnemyCenterPosition = new Vector3 { x = 0, y = 0, z = 0 }, DistanceToShip = 100f + i } });
        }

        _screen = new List<ProjectedTriangleMesh>();
        _crash = new List<ProjectedTriangleMesh>();
    }

    [Benchmark]
    public List<ProjectedTriangleMesh> UpdateWorldCleanupExplodedObjects()
    {
        return _manager.UpdateWorld(_world, ref _screen, ref _crash);
    }

    private sealed class StubSceneHandler : ISceneHandler
    {
        private readonly IScene _scene = new StubScene();
        public IScene GetActiveScene() => _scene;
        public void SetupActiveScene(I3dWorld world)
        {
        }

        public void ResetActiveScene(I3dWorld world)
        {
        }

        public void ResetActiveSceneToPlanetStart(I3dWorld world)
        {
        }

        public void NextScene(I3dWorld world)
        {
        }

        public void HandleKeyPress(GameInputKey key, I3dWorld world)
        {
        }

        public void HandleOverlayActivation(I3dWorld world)
        {
        }

        public void UpdateFrame(I3dWorld world)
        {
        }

        private sealed class StubScene : IScene
        {
            public SceneTypes SceneType => SceneTypes.Game;
            public SceneBiomeTypes SceneBiome => SceneBiomeTypes.HillsWoods;
            public GameModes GameMode => GameModes.Live;
            public string SceneMusic => "music_flight";

            public void SetupScene(I3dWorld world)
            {
            }

            public void SetupSceneOverlay()
            {
            }

            public void SetupGameOverlay()
            {
            }

            public void SetupVideoOverlay(string fileName)
            {
                throw new System.NotImplementedException();
            }
        }
    }
}
