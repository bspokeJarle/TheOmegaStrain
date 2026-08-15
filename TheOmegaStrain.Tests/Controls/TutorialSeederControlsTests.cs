using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Controls;
using System.Reflection;

namespace TheOmegaStrain.Tests.Controls;

[TestClass]
public class TutorialSeederControlsTests
{
    [TestInitialize]
    public void Setup()
    {
        GameState.GamePlayState = new GamePlayState();
        GameState.SurfaceState = new SurfaceState();
        GameState.ScreenOverlayState = new ScreenOverlayState();
        GameState.TutorialState = new TutorialRuntimeState();
        GameState.DeltaTime = 1f / 60f;
    }

    [TestMethod]
    public void MoveObject_BulletHitDoesNotSwitchTutorialSeederToAiMovement()
    {
        var controls = new TutorialSeederControls();
        var seeder = CreateSeeder();

        seeder.ImpactStatus!.HasCrashed = true;
        seeder.ImpactStatus.ObjectName = "Bullet";

        controls.MoveObject(seeder, audioPlayer: null, soundRegistry: null);

        Assert.IsFalse(IsUsingCombatControls(controls));
        Assert.IsFalse(seeder.ImpactStatus.HasCrashed);
        Assert.AreEqual(34, seeder.ImpactStatus.ObjectHealth);
    }

    [TestMethod]
    public void MoveObject_LethalHitSwitchesToExistingExplosionFlow()
    {
        var controls = new TutorialSeederControls();
        var seeder = CreateSeeder();

        seeder.ImpactStatus!.HasCrashed = true;
        seeder.ImpactStatus.ObjectName = "Lazer";

        controls.MoveObject(seeder, audioPlayer: null, soundRegistry: null);

        Assert.IsTrue(IsUsingCombatControls(controls));
        Assert.IsTrue(seeder.ImpactStatus.ObjectHealth <= 0);
    }

    [TestMethod]
    public void MoveObject_LethalHitKeepsExplosionRotationSnapshot()
    {
        var controls = new TutorialSeederControls();
        var seeder = CreateSeeder();
        seeder.Rotation = new Vector3 { x = 90f, y = 0f, z = 37f };

        seeder.ImpactStatus!.HasCrashed = true;
        seeder.ImpactStatus.ObjectName = "Lazer";

        controls.MoveObject(seeder, audioPlayer: null, soundRegistry: null);

        Assert.AreEqual(37f, seeder.Rotation!.z, 0.001f);

        seeder.Rotation = new Vector3();
        controls.MoveObject(seeder, audioPlayer: null, soundRegistry: null);

        Assert.AreEqual(37f, seeder.Rotation!.z, 0.001f,
            "Tutorial seeder explosion should keep the rotation from the hit frame instead of snapping to the normal seeder spin.");
    }

    [TestMethod]
    public void MoveObject_LethalHitDoesNotDoubleApplySurfaceSyncToExplosionY()
    {
        GameState.SurfaceState.GlobalMapPosition = new Vector3 { x = 1000f, y = 80f, z = 1000f };
        var controls = new TutorialSeederControls();
        var seeder = CreateSeeder();
        float unsyncedY = seeder.ObjectOffsets!.y;
        float expectedHitFrameY = GameState.SurfaceState.GlobalMapPosition.y
            * SurfacePositionSyncHelpers.DefaultEnemySurfaceSyncFactorY
            + unsyncedY;

        seeder.ImpactStatus!.HasCrashed = true;
        seeder.ImpactStatus.ObjectName = "Lazer";

        controls.MoveObject(seeder, audioPlayer: null, soundRegistry: null);

        Assert.AreEqual(expectedHitFrameY, seeder.ObjectOffsets!.y, 0.001f,
            "Explosion Y should stay at the hit-frame offset. A late SeederControls sync init must not add GlobalMapPosition.y again.");
    }

    [TestMethod]
    public void MoveObject_VisibleFrameSyncsTutorialOffsetsBackToAiObjectBeforeDeepCopy()
    {
        GameState.SurfaceState.GlobalMapPosition = new Vector3 { x = 1000f, y = 80f, z = 1000f };
        var controls = new TutorialSeederControls();
        var original = CreateSeeder();
        original.Movement = controls;
        GameState.SurfaceState.AiObjects.Add(original);

        var visibleFrameCopy = (OmegaObject3D)OmegaObjectHelpers.DeepCopySingleObject(original);
        controls.MoveObject(visibleFrameCopy, audioPlayer: null, soundRegistry: null);

        Assert.AreEqual(visibleFrameCopy.ObjectOffsets!.y, original.ObjectOffsets!.y, 0.001f,
            "Tutorial seeders are rendered from deep copies, so their synced offsets must be pushed back to the AiObjects original before the next frame.");

        visibleFrameCopy.ImpactStatus!.HasCrashed = true;
        visibleFrameCopy.ImpactStatus.ObjectName = "Lazer";

        var nextFrameCopy = (OmegaObject3D)OmegaObjectHelpers.DeepCopySingleObject(original);
        controls.MoveObject(nextFrameCopy, audioPlayer: null, soundRegistry: null);

        Assert.IsTrue(IsUsingCombatControls(controls));
        Assert.AreEqual(visibleFrameCopy.ObjectOffsets.y, nextFrameCopy.ObjectOffsets!.y, 0.001f,
            "The explosion should anchor to the synced tutorial offset from the previous visible frame, not the stale construction offset.");
        Assert.AreEqual(nextFrameCopy.ObjectOffsets.y, original.ObjectOffsets!.y, 0.001f);
    }

    private static bool IsUsingCombatControls(TutorialSeederControls controls)
    {
        var field = typeof(TutorialSeederControls).GetField("_useCombatControls", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        return (bool)field.GetValue(controls)!;
    }

    private static OmegaObject3D CreateSeeder() =>
        new()
        {
            ObjectId = 42,
            ObjectName = "Seeder",
            WorldPosition = new Vector3 { x = 1000f, y = 0f, z = 1000f },
            ObjectOffsets = new Vector3 { x = 0f, y = -200f, z = 600f },
            Rotation = new Vector3(),
            ImpactStatus = new ImpactStatus { ObjectHealth = 55 },
            ObjectParts = new List<I3dObjectPart>
            {
                new OmegaObjectPart3D
                {
                    PartName = "Body",
                    IsVisible = true,
                    Triangles = new List<ITriangleMeshWithColor>
                    {
                        new TriangleMeshWithColor
                        {
                            Color = "00ff00",
                            noHidden = true,
                            vert1 = new Vector3 { x = -10f, y = 0f, z = 0f },
                            vert2 = new Vector3 { x = 10f, y = 0f, z = 0f },
                            vert3 = new Vector3 { x = 0f, y = 12f, z = 0f }
                        }
                    }
                }
            },
            CrashBoxes = new List<List<IVector3>>()
        };
}
