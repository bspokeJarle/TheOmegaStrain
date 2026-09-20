using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Controls;
using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Game.Helpers;
using TheOmegaStrain.Game.World.Objects;
using TheOmegaStrain.Runtime.Collision;

namespace TheOmegaStrain.Tests.Controls;

[TestClass]
[DoNotParallelize]
public class PowerUpControlsTests
{
    private ShipState _previousShip = null!;
    private SurfaceState _previousSurface = null!;
    private float _previousDeltaTime;
    [TestInitialize]
    public void Setup()
    {
        _previousShip = GameState.ShipState;
        _previousSurface = GameState.SurfaceState;
        _previousDeltaTime = GameState.DeltaTime;
        GameState.ShipState = new ShipState { ShipObjectOffsets = new Vector3(40f, -280f, 120f) };
        GameState.DeltaTime = 1f / 90f;
        OmegaWorldViewSetup.ConfigurePitch(63f);
        GameState.SurfaceState = new SurfaceState
        {
            GlobalMapPosition = new Vector3(),
            AiObjects = new List<OmegaObject3D>()
        };
    }

    [TestCleanup]
    public void Cleanup()
    {
        GameState.ShipState = _previousShip;
        GameState.SurfaceState = _previousSurface;
        GameState.DeltaTime = _previousDeltaTime;
        OmegaWorldViewSetup.ConfigurePitch(63f);
    }

    [DataTestMethod]
    [DataRow(30, 63f)]
    [DataRow(60, 63f)]
    [DataRow(90, 63f)]
    [DataRow(144, 63f)]
    [DataRow(90, 70f)]
    public void MoveObject_ReachesMovingShipWithinOneAndAHalfSecondsAcrossFrameCopies(int fps, float pitch)
    {
        OmegaWorldViewSetup.ConfigurePitch(pitch);
        GameState.DeltaTime = 1f / fps;
        var original = CreatePowerUp();
        var controls = new PowerUpControls();
        original.Movement = controls;
        GameState.SurfaceState.AiObjects.Add(original);
        OmegaObject3D frame = original;
        for (int i = 0; i < (int)(fps * 1.5f); i++)
        {
            GameState.SurfaceState.GlobalMapPosition = new Vector3(95000f + i * 2, 20f, 96000f - i);
            GameState.ShipState.ShipObjectOffsets = new Vector3(40f + i, -280f - i, 120f + i);
            frame = OmegaObjectHelpers.DeepCopy3dObjects(new List<OmegaObject3D> { original })[0];
            controls.MoveObject(frame, null, null);
        }

        var target = SurfacePositionSyncHelpers.GetShipRamTargetWorldPosition(frame);
        Assert.AreEqual(target.x, frame.WorldPosition.x, 0.5f);
        Assert.AreEqual(target.y, frame.WorldPosition.y, 0.5f);
        Assert.AreEqual(target.z, frame.WorldPosition.z, 0.5f);
        Assert.IsTrue(frame.CrashBoxes.Count > 0, "Normal pickup collision must be enabled on arrival.");
        Assert.IsFalse(frame.ImpactStatus!.HasCrashed, "Movement must not award the pickup itself.");
        Assert.AreEqual(frame.WorldPosition.x, original.WorldPosition.x);
    }

    [TestMethod]
    public void MoveObject_SeparateDropsDoNotShareTravelTimer()
    {
        var first = CreatePowerUp();
        var firstControls = new PowerUpControls();
        for (int i = 0; i < 135; i++) firstControls.MoveObject(first, null, null);
        var second = CreatePowerUp();
        var secondControls = new PowerUpControls();
        var start = Copy(second.WorldPosition);
        secondControls.MoveObject(second, null, null);
        Assert.AreEqual(start.x, second.WorldPosition.x);
        Assert.AreEqual(start.y, second.WorldPosition.y);
        Assert.AreEqual(start.z, second.WorldPosition.z);
    }

    [DataTestMethod]
    [DataRow(63f)]
    [DataRow(70f)]
    public void MoveObject_ArrivalTriggersExistingPickupCollision(float pitch)
    {
        OmegaWorldViewSetup.ConfigurePitch(pitch);
        GameState.SurfaceState.GlobalMapPosition = new Vector3(95000f, 10f, 96000f);
        var ship = Ship.CreateShip(null!);
        ship.ObjectName = "Ship";
        ship.WorldPosition = new Vector3();
        ship.ObjectOffsets = new Vector3(40f, -280f, 120f);
        ship.Rotation = new Vector3(pitch, 0f, 35f);
        ship.ImpactStatus = new ImpactStatus { ObjectHealth = 100 };
        GameState.ShipState.ShipObjectOffsets = ship.ObjectOffsets;
        GameState.ShipState.ShipWorldPosition = new Vector3(95000f, 0f, 96000f);
        GameState.ShipState.ShipCrashCenterWorldPosition = VectorMath.Add(
            GameState.ShipState.ShipWorldPosition, ObjectCollisionGeometry.GetRotatedLocalCrashCenter(ship));
        var powerup = PowerUp.CreatePowerup(null!);
        powerup.WorldPosition = new Vector3(96000f, 0f, 96500f);
        powerup.ObjectOffsets = new Vector3(0f, -150f, 450f);
        var controls = new PowerUpControls();
        powerup.Movement = controls;
        GameState.SurfaceState.AiObjects.Add(powerup);
        OmegaObject3D frame = powerup;
        for (int i = 0; i < 135; i++)
        {
            frame = OmegaObjectHelpers.DeepCopy3dObjects(new() { powerup })[0];
            controls.MoveObject(frame, null, null);
        }

        new ObjectFrameTransformer().RotateObjectGeometry(frame);
        new ObjectFrameTransformer().RotateObjectGeometry(ship);
        Assert.IsTrue(ObjectPlacementHelpers.TryGetRenderPosition(frame, 800, 450, out _, out _, out _));
        Assert.IsTrue(ObjectPlacementHelpers.TryGetRenderPosition(ship, 800, 450, out _, out _, out _));
        CrashDetection.HandleCrashboxes(new() { frame, ship }, isPaused: false);
        Assert.IsTrue(frame.ImpactStatus!.HasCrashed);
        Assert.AreEqual("Ship", frame.ImpactStatus.ObjectName);
        Assert.IsTrue(ship.ImpactStatus.HasCrashed);
        Assert.AreEqual("PowerUp", ship.ImpactStatus.ObjectName);
    }

    [DataTestMethod]
    [DataRow(56f)]
    [DataRow(63f)]
    [DataRow(70f)]
    public void MoveObject_UsesConfiguredWorldPitch(float pitchDegrees)
    {
        OmegaWorldViewSetup.ConfigurePitch(pitchDegrees);
        var powerup = CreatePowerUp();

        new PowerUpControls().MoveObject(powerup, audioPlayer: null, soundRegistry: null);

        Assert.AreEqual(pitchDegrees, powerup.Rotation!.x, 0.001f);
    }

    [TestMethod]
    public void MoveObject_ExplodingPowerUpKeepsHitFrameTransformAfterExternalMutation()
    {
        var controls = new PowerUpControls();
        var powerup = CreatePowerUp();
        GameState.SurfaceState.AiObjects.Add(powerup);

        powerup.ImpactStatus!.HasCrashed = true;
        controls.MoveObject(powerup, audioPlayer: null, soundRegistry: null);

        var anchoredWorld = Copy(powerup.WorldPosition!);
        var anchoredOffsets = Copy(powerup.ObjectOffsets!);

        powerup.WorldPosition!.x += 500f;
        powerup.WorldPosition.y += 25f;
        powerup.WorldPosition.z -= 300f;
        powerup.ObjectOffsets!.x -= 100f;
        powerup.ObjectOffsets.y += 250f;
        powerup.ObjectOffsets.z += 150f;

        controls.MoveObject(powerup, audioPlayer: null, soundRegistry: null);

        Assert.AreEqual(anchoredWorld.x, powerup.WorldPosition!.x, 0.001f);
        Assert.AreEqual(anchoredWorld.y, powerup.WorldPosition.y, 0.001f);
        Assert.AreEqual(anchoredWorld.z, powerup.WorldPosition.z, 0.001f);
        Assert.AreEqual(anchoredOffsets.x, powerup.ObjectOffsets!.x, 0.001f);
        Assert.AreEqual(anchoredOffsets.y, powerup.ObjectOffsets.y, 0.001f);
        Assert.AreEqual(anchoredOffsets.z, powerup.ObjectOffsets.z, 0.001f);
    }

    private static OmegaObject3D CreatePowerUp()
    {
        return new OmegaObject3D
        {
            ObjectId = 30,
            ObjectName = "PowerUp",
            WorldPosition = new Vector3 { x = 1100f, y = 2f, z = 2100f },
            ObjectOffsets = new Vector3 { x = 20f, y = -150f, z = 450f },
            Rotation = new Vector3(),
            CrashBoxes = new List<List<IVector3>>
            {
                new()
                {
                    new Vector3 { x = -10f, y = -10f, z = -10f },
                    new Vector3 { x = 10f, y = 10f, z = 10f }
                }
            },
            ImpactStatus = new ImpactStatus { HasCrashed = false, ObjectName = "Ship", ObjectHealth = 1 },
            ObjectParts = new List<I3dObjectPart>
            {
                new OmegaObjectPart3D
                {
                    PartName = "PowerUpBody",
                    IsVisible = true,
                    Triangles = new List<ITriangleMeshWithColorAndTexture>
                    {
                        new TriangleMeshWithColor
                        {
                            Color = "4488FF",
                            noHidden = true,
                            vert1 = new Vector3 { x = -10f, y = 0f, z = 0f },
                            vert2 = new Vector3 { x = 10f, y = 0f, z = 0f },
                            vert3 = new Vector3 { x = 0f, y = 12f, z = 0f }
                        }
                    }
                }
            }
        };
    }

    private static Vector3 Copy(IVector3 source)
    {
        return new Vector3
        {
            x = source.x,
            y = source.y,
            z = source.z
        };
    }
}
