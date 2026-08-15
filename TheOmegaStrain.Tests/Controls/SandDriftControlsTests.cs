using TheOmegaStrain.Game.World.Objects;
using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Controls;
using System.Linq;

namespace TheOmegaStrain.Tests.Controls;

[TestClass]
public class SandDriftControlsTests
{
    [TestInitialize]
    public void Setup()
    {
        GameState.SurfaceState = new SurfaceState();
        GameState.SettingsState = new GameSettingsState();
        GameState.DeltaTime = GameState.GameplayBaselineDeltaTime;
        GameState.ObjectIdCounter = 0;
        SandDriftControls.GlobalSandOpacity = 1f;
    }

    [TestMethod]
    public void CreateSandEmitter_IsNonCollidingSceneOwnedEmitter()
    {
        var emitter = SandEmitter.CreateSandEmitter(null);

        Assert.AreEqual("SandEmitter", emitter.ObjectName);
        Assert.IsNull(emitter.Particles);
        Assert.IsFalse(emitter.HasShadow);
        Assert.AreEqual(0, emitter.CrashBoxes.Count);
        Assert.IsInstanceOfType(emitter.Movement, typeof(SandDriftControls));
    }

    [TestMethod]
    public void MoveObject_DrawsSlowWideSandDust()
    {
        var emitter = SandEmitter.CreateSandEmitter(null);

        emitter.Movement!.MoveObject(emitter, null, null);

        var dustPart = emitter.ObjectParts.Single(p => p.PartName == "SandDust");
        var visibleDust = dustPart.Triangles.Where(t => t.vert1.z != 0f).ToList();

        Assert.AreEqual(SandDriftControls.TargetDustCount, dustPart.Triangles.Count);
        Assert.IsTrue(dustPart.Triangles.All(t => t.noHidden == true));
        Assert.IsTrue(visibleDust.Count > SandDriftControls.VisibleDustTarget / 2, "Sand drift should keep a visible, sparse dust sheet.");
        Assert.IsTrue(visibleDust.Any(t => Math.Abs(t.vert2.x - t.vert1.x) > Math.Abs(t.vert2.y - t.vert1.y)),
            "Sand drift should remain subtly horizontal instead of falling as vertical rain streaks.");
        Assert.IsTrue(visibleDust.Any(t => t.Color != "ffffff" && t.Color != "BDEAFF" && t.Color != "000000"),
            "Sand drift should use warm sand colors, not snow or rain colors.");
    }

    [TestMethod]
    public void MoveObject_DriftsMostlySidewaysInsteadOfFallingFast()
    {
        var emitter = SandEmitter.CreateSandEmitter(null);
        emitter.Movement!.MoveObject(emitter, null, null);

        var dustPart = emitter.ObjectParts.Single(p => p.PartName == "SandDust");
        var initial = dustPart.Triangles
            .Take(SandDriftControls.VisibleDustTarget)
            .Select(t => (x: t.vert1.x, y: t.vert1.y))
            .ToList();

        for (int i = 0; i < 8; i++)
            emitter.Movement.MoveObject(emitter, null, null);

        var moved = dustPart.Triangles
            .Take(SandDriftControls.VisibleDustTarget)
            .Select(t => (x: t.vert1.x, y: t.vert1.y))
            .ToList();

        float avgAbsX = 0f;
        float avgAbsY = 0f;
        int count = 0;
        for (int i = 0; i < initial.Count; i++)
        {
            if (initial[i].x == 0f || moved[i].x == 0f)
                continue;

            avgAbsX += MathF.Abs(moved[i].x - initial[i].x);
            avgAbsY += MathF.Abs(moved[i].y - initial[i].y);
            count++;
        }

        avgAbsX /= count;
        avgAbsY /= count;

        Assert.IsTrue(avgAbsX > avgAbsY,
            $"Sand should drift more sideways than downward. avgX={avgAbsX:0.###}, avgY={avgAbsY:0.###}");
        Assert.IsTrue(avgAbsY < 6f,
            $"Sand should hover instead of dropping quickly like rain. avgY={avgAbsY:0.###}");
    }

    [TestMethod]
    public void MoveObject_HonorsGlobalSandOpacity()
    {
        var emitter = SandEmitter.CreateSandEmitter(null);

        SandDriftControls.GlobalSandOpacity = 0f;
        emitter.Movement!.MoveObject(emitter, null, null);

        var dustPart = emitter.ObjectParts.Single(p => p.PartName == "SandDust");
        Assert.IsTrue(dustPart.Triangles.All(t => t.Color == "000000" && t.vert1.z == 0f));
    }

    [TestMethod]
    public void MoveObject_KeepsDustInWorldSpaceAsShipMoves()
    {
        var emitter = SandEmitter.CreateSandEmitter(null);
        emitter.Movement!.MoveObject(emitter, null, null);

        var dustPart = emitter.ObjectParts.Single(p => p.PartName == "SandDust");
        float initialAverageZ = dustPart.Triangles
            .Take(SandDriftControls.VisibleDustTarget)
            .Average(t => t.vert1.z);

        GameState.SurfaceState.GlobalMapPosition = new Vector3
        {
            x = GameState.SurfaceState.GlobalMapPosition.x,
            y = GameState.SurfaceState.GlobalMapPosition.y,
            z = GameState.SurfaceState.GlobalMapPosition.z + 50f
        };

        emitter.Movement.MoveObject(emitter, null, null);

        float movedAverageZ = dustPart.Triangles
            .Take(SandDriftControls.VisibleDustTarget)
            .Average(t => t.vert1.z);
        float delta = movedAverageZ - initialAverageZ;

        Assert.IsTrue(delta < -45f && delta > -55f, $"Dust depth should shift against ship/world movement instead of staying screen-locked. Delta={delta:0.###}");
    }

    [TestMethod]
    public void MoveObject_SyncsEmitterVerticallyWithGroundAltitude()
    {
        var emitter = SandEmitter.CreateSandEmitter(null);

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
    public void MoveObject_HighParticleDensityCreatesMoreDust()
    {
        GameState.SettingsState = new GameSettingsState
        {
            GraphicsQuality = GraphicsQualityPreset.High,
            ParticleDensityPercent = 130,
            EnhancedWeatherEnabled = true
        };

        var emitter = SandEmitter.CreateSandEmitter(null);

        emitter.Movement!.MoveObject(emitter, null, null);

        var dustPart = emitter.ObjectParts.Single(p => p.PartName == "SandDust");
        Assert.AreEqual(SandDriftControls.CurrentTargetDustCount, dustPart.Triangles.Count);
        Assert.IsTrue(dustPart.Triangles.Count > SandDriftControls.TargetDustCount);
    }
}
