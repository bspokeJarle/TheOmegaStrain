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
public class LeafDriftControlsTests
{
    [TestInitialize]
    public void Setup()
    {
        GameState.SurfaceState = new SurfaceState();
        GameState.SettingsState = new GameSettingsState();
        GameState.DeltaTime = GameState.GameplayBaselineDeltaTime;
        GameState.ObjectIdCounter = 0;
        LeafDriftControls.GlobalLeafOpacity = 1f;
    }

    [TestMethod]
    public void CreateLeafEmitter_IsNonCollidingSceneOwnedEmitter()
    {
        var emitter = LeafEmitter.CreateLeafEmitter(null);

        Assert.AreEqual("LeafEmitter", emitter.ObjectName);
        Assert.IsNull(emitter.Particles);
        Assert.IsFalse(emitter.HasShadow);
        Assert.AreEqual(0, emitter.CrashBoxes.Count);
        Assert.IsInstanceOfType(emitter.Movement, typeof(LeafDriftControls));
    }

    [TestMethod]
    public void MoveObject_DrawsLowPolyLeavesInTreePalette()
    {
        var emitter = LeafEmitter.CreateLeafEmitter(null);

        emitter.Movement!.MoveObject(emitter, null, null);

        var leafPart = emitter.ObjectParts.Single(p => p.PartName == "Leaves");
        var visibleLeaves = leafPart.Triangles.Where(t => t.vert1.z != 0f).ToList();

        Assert.AreEqual(LeafDriftControls.TargetLeafCount, leafPart.Triangles.Count);
        Assert.IsTrue(leafPart.Triangles.All(t => t.noHidden == true));
        Assert.IsTrue(visibleLeaves.Count > LeafDriftControls.VisibleLeafTarget / 2);
        Assert.IsTrue(visibleLeaves.All(t => LeafTree.LeafColors.Contains(t.Color)));
        Assert.IsTrue(visibleLeaves.Any(t => Math.Abs(t.vert3.y - t.vert1.y) > Math.Abs(t.vert2.y - t.vert1.y)),
            "Leaves should read as longer leaf-like triangles, not tiny dust specks.");
    }

    [TestMethod]
    public void MoveObject_HonorsGlobalLeafOpacityForStarFade()
    {
        var emitter = LeafEmitter.CreateLeafEmitter(null);

        LeafDriftControls.GlobalLeafOpacity = 0f;
        emitter.Movement!.MoveObject(emitter, null, null);

        var leafPart = emitter.ObjectParts.Single(p => p.PartName == "Leaves");
        Assert.IsTrue(
            leafPart.Triangles.All(t => t.Color == "000000" && t.vert1.z == 0f),
            "Leaves should fade out completely when the starfield opacity is fully faded in.");

        LeafDriftControls.GlobalLeafOpacity = 1f;
        emitter.Movement.MoveObject(emitter, null, null);

        var visibleLeaves = leafPart.Triangles.Where(t => t.vert1.z != 0f).ToList();
        Assert.IsTrue(visibleLeaves.Count > LeafDriftControls.VisibleLeafTarget / 2);
        Assert.IsTrue(visibleLeaves.All(t => LeafTree.LeafColors.Contains(t.Color)));
    }

    [TestMethod]
    public void MoveObject_SyncsEmitterVerticallyWithGroundAltitude()
    {
        var emitter = LeafEmitter.CreateLeafEmitter(null);

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
    public void MoveObject_HighParticleDensityCreatesMoreLeaves()
    {
        GameState.SettingsState = new GameSettingsState
        {
            GraphicsQuality = GraphicsQualityPreset.High,
            ParticleDensityPercent = 130,
            EnhancedWeatherEnabled = true
        };

        var emitter = LeafEmitter.CreateLeafEmitter(null);

        emitter.Movement!.MoveObject(emitter, null, null);

        var leafPart = emitter.ObjectParts.Single(p => p.PartName == "Leaves");
        Assert.AreEqual(LeafDriftControls.CurrentTargetLeafCount, leafPart.Triangles.Count);
        Assert.IsTrue(leafPart.Triangles.Count > LeafDriftControls.TargetLeafCount);
    }
}
