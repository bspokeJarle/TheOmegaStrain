using TheOmegaStrain.Game.World.Objects;
using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Controls;
using System.Linq;
using static TheOmegaStrain.Domain._3dSpecificsImplementations;

namespace TheOmegaStrain.Tests.Controls;

[TestClass]
public class SnowfallControlsTests
{
    [TestInitialize]
    public void Setup()
    {
        GameState.SurfaceState = new SurfaceState();
        GameState.SettingsState = new GameSettingsState();
        GameState.DeltaTime = GameState.GameplayBaselineDeltaTime;
        GameState.ObjectIdCounter = 0;
    }

    [TestMethod]
    public void CreateSnowEmitter_IsNonCollidingSceneOwnedEmitter()
    {
        var emitter = SnowEmitter.CreateSnowEmitter(null);

        Assert.AreEqual("SnowEmitter", emitter.ObjectName);
        Assert.IsNull(emitter.Particles);
        Assert.IsFalse(emitter.HasShadow);
        Assert.AreEqual(0, emitter.CrashBoxes.Count);
        Assert.IsInstanceOfType(emitter.Movement, typeof(SnowfallControls));
    }

    [TestMethod]
    public void MoveObject_MaintainsSmallWhiteSnowflakes()
    {
        var emitter = SnowEmitter.CreateSnowEmitter(null);

        emitter.Movement!.MoveObject(emitter, null, null);

        var snowflakePart = emitter.ObjectParts.Single(p => p.PartName == "Snowflakes");
        Assert.AreEqual(SnowfallControls.TargetFlakeCount, snowflakePart.Triangles.Count);
        Assert.IsTrue(snowflakePart.Triangles.All(t => t.Color == "ffffff"));
        Assert.IsTrue(snowflakePart.Triangles.All(t => t.noHidden == true));
        Assert.IsTrue(snowflakePart.Triangles.Take(SnowfallControls.VisibleFlakeTarget).All(t => t.vert1.z >= 750f));
        Assert.IsTrue(snowflakePart.Triangles.Take(SnowfallControls.VisibleFlakeTarget).All(t => t.vert1.z <= 750f + SnowfallControls.DepthSpread));
    }

    [TestMethod]
    public void MoveObject_KeepsFlakesInWorldSpaceAsShipMoves()
    {
        var emitter = SnowEmitter.CreateSnowEmitter(null);
        emitter.Movement!.MoveObject(emitter, null, null);

        var snowflakePart = emitter.ObjectParts.Single(p => p.PartName == "Snowflakes");
        float initialAverageZ = snowflakePart.Triangles
            .Take(SnowfallControls.VisibleFlakeTarget)
            .Average(t => t.vert1.z);

        GameState.SurfaceState.GlobalMapPosition = new Vector3
        {
            x = GameState.SurfaceState.GlobalMapPosition.x,
            y = GameState.SurfaceState.GlobalMapPosition.y,
            z = GameState.SurfaceState.GlobalMapPosition.z + 50f
        };

        emitter.Movement.MoveObject(emitter, null, null);

        float movedAverageZ = snowflakePart.Triangles
            .Take(SnowfallControls.VisibleFlakeTarget)
            .Average(t => t.vert1.z);
        float delta = movedAverageZ - initialAverageZ;

        Assert.IsTrue(delta < -45f && delta > -55f, $"Snowflake depth should shift against ship/world movement instead of staying screen-locked. Delta={delta:0.###}");
    }

    [TestMethod]
    public void MoveObject_RecyclesMostSnowAheadOfTravelDirection()
    {
        var emitter = SnowEmitter.CreateSnowEmitter(null);
        emitter.Movement!.MoveObject(emitter, null, null);

        GameState.SurfaceState.GlobalMapPosition = new Vector3
        {
            x = GameState.SurfaceState.GlobalMapPosition.x,
            y = GameState.SurfaceState.GlobalMapPosition.y,
            z = GameState.SurfaceState.GlobalMapPosition.z + 20f
        };
        emitter.Movement.MoveObject(emitter, null, null);

        GameState.SurfaceState.GlobalMapPosition = new Vector3
        {
            x = GameState.SurfaceState.GlobalMapPosition.x,
            y = GameState.SurfaceState.GlobalMapPosition.y,
            z = GameState.SurfaceState.GlobalMapPosition.z + 6000f
        };
        emitter.Movement.MoveObject(emitter, null, null);

        var snowflakePart = emitter.ObjectParts.Single(p => p.PartName == "Snowflakes");
        int flakesAhead = snowflakePart.Triangles.Count(t => t.vert1.z > 500f);

        Assert.IsTrue(flakesAhead >= SnowfallControls.TargetFlakeCount * 3 / 4, "Recycled snow should be biased toward the current travel direction.");
    }

    [TestMethod]
    public void MoveObject_SyncsEmitterVerticallyWithGroundAltitude()
    {
        var emitter = SnowEmitter.CreateSnowEmitter(null);

        GameState.SurfaceState.GlobalMapPosition = new Vector3
        {
            x = GameState.SurfaceState.GlobalMapPosition.x,
            y = 40f,
            z = GameState.SurfaceState.GlobalMapPosition.z
        };

        emitter.Movement!.MoveObject(emitter, null, null);

        Assert.AreEqual(
            40f * SurfacePositionSyncHelpers.DefaultEnemySurfaceSyncFactorY,
            emitter.ObjectOffsets.y,
            0.001f);
    }

    [TestMethod]
    public void MoveObject_HighParticleDensityCreatesMoreSnowflakes()
    {
        GameState.SettingsState = new GameSettingsState
        {
            GraphicsQuality = GraphicsQualityPreset.High,
            ParticleDensityPercent = 130,
            EnhancedWeatherEnabled = true
        };

        var emitter = SnowEmitter.CreateSnowEmitter(null);

        emitter.Movement!.MoveObject(emitter, null, null);

        var snowflakePart = emitter.ObjectParts.Single(p => p.PartName == "Snowflakes");
        Assert.AreEqual(SnowfallControls.CurrentTargetFlakeCount, snowflakePart.Triangles.Count);
        Assert.IsTrue(snowflakePart.Triangles.Count > SnowfallControls.TargetFlakeCount);
    }
}
