using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Microsoft.VSDiagnostics;
using _3dTesting.MainWindowClasses;
using _3dTesting._Coordinates;
using _3dTesting._3dWorld;
using _3dTesting.Helpers;
using Domain;
using static Domain._3dSpecificsImplementations;

namespace TheOmegaStrain.Benchmarks;
[CPUUsageDiagnoser]
public class UpdateWorldBenchmarks
{
    private GameWorldManager _manager = null !;
    private _3dWorld _world = null !;
    private List<_2dTriangleMesh> _screen = null !;
    private List<_2dTriangleMesh> _crash = null !;
    [GlobalSetup]
    public void Setup()
    {
        _manager = new GameWorldManager();
        _world = new _3dWorld();
        _world.WorldInhabitants.Clear();
        _world.SceneHandler = new StubSceneHandler();
        var dynamicObj = TestObjectFactory.CreateDynamicTestObject();
        dynamicObj.WorldPosition = new Vector3(0, 0, 0);
        dynamicObj.Rotation = new Vector3();
        dynamicObj.CrashBoxesFollowRotation = false;
        var surfaceObj = TestObjectFactory.CreateSurfaceBasedTestObject();
        surfaceObj.WorldPosition = new Vector3(0, 0, 0);
        surfaceObj.Rotation = new Vector3();
        surfaceObj.CrashBoxesFollowRotation = false;
        _world.WorldInhabitants.Add(dynamicObj);
        _world.WorldInhabitants.Add(surfaceObj);
        _screen = new List<_2dTriangleMesh>();
        _crash = new List<_2dTriangleMesh>();
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _screen = new List<_2dTriangleMesh>();
        _crash = new List<_2dTriangleMesh>();
    }

    [Benchmark]
    public List<_2dTriangleMesh> UpdateWorld()
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
