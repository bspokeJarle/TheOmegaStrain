using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Runtime.Collision;

namespace TheOmegaStrain.Tests.Physics;

[TestClass]
[DoNotParallelize]
public class BomberBombBlastPositionTests
{
    private GamePlayState _previousGameplay = null!;
    private ShipState _previousShip = null!;
    private SurfaceState _previousSurface = null!;

    [TestInitialize]
    public void Setup()
    {
        _previousGameplay = GameState.GamePlayState;
        _previousShip = GameState.ShipState;
        _previousSurface = GameState.SurfaceState;
        GameState.GamePlayState = new GamePlayState { Phase = GamePhase.Playing };
        GameState.ShipState = new ShipState();
        GameState.SurfaceState = new SurfaceState
        {
            SurfaceViewportObject = new OmegaObject3D
            {
                ObjectId = 97000,
                ObjectOffsets = new Vector3(70f, 500f, 400f)
            }
        };
    }

    [TestCleanup]
    public void Cleanup()
    {
        GameState.GamePlayState = _previousGameplay;
        GameState.ShipState = _previousShip;
        GameState.SurfaceState = _previousSurface;
    }

    [TestMethod]
    public void Blast_DamagesShipAtTheSameSurfaceFootprintUsedForCrater()
    {
        var bomb = CreateExplodingBomb(97001);
        var footprint = SurfacePositionSyncHelpers.GetSurfaceFootprintWorldPosition(bomb);
        GameState.ShipState.ShipCrashCenterWorldPosition = footprint;

        CrashDetection.HandleCrashboxes([bomb, CreateShip()], isPaused: false);

        Assert.AreEqual(100f - GameSetup.BomberBombBlastDamage, GameState.GamePlayState.Health, 0.001f);
    }

    [TestMethod]
    public void Blast_DoesNotDamageShipAtBombsRawViewportCornerPosition()
    {
        var bomb = CreateExplodingBomb(97002);
        GameState.ShipState.ShipCrashCenterWorldPosition = bomb.WorldPosition;

        CrashDetection.HandleCrashboxes([bomb, CreateShip()], isPaused: false);

        Assert.AreEqual(100f, GameState.GamePlayState.Health, 0.001f);
    }

    [DataTestMethod]
    [DataRow(0f, 35f)]
    [DataRow(200f, 35f)]
    [DataRow(201f, 20f)]
    [DataRow(400f, 20f)]
    [DataRow(401f, 10f)]
    [DataRow(600f, 10f)]
    [DataRow(601f, 0f)]
    public void Blast_DamageFallsAcrossThreeDistanceBands(float distance, float expectedDamage)
    {
        var bomb = CreateExplodingBomb(98000 + (int)distance);
        var footprint = SurfacePositionSyncHelpers.GetSurfaceFootprintWorldPosition(bomb);
        GameState.ShipState.ShipCrashCenterWorldPosition = new Vector3(
            footprint.x + distance, footprint.y, footprint.z);

        CrashDetection.HandleCrashboxes([bomb, CreateShip()], isPaused: false);

        Assert.AreEqual(100f - expectedDamage, GameState.GamePlayState.Health, 0.001f);
    }

    private static OmegaObject3D CreateExplodingBomb(int id) => new()
    {
        ObjectId = id,
        ObjectName = "BomberBomb",
        WorldPosition = new Vector3(10_000f, 0f, 10_000f),
        ObjectOffsets = new Vector3(0f, 0f, 600f),
        ObjectParts = [new OmegaObjectPart3D { PartName = "BombBody" }],
        CrashBoxes = [],
        ImpactStatus = new ImpactStatus { HasCrashed = true, ObjectName = "Surface" }
    };

    private static OmegaObject3D CreateShip() => new()
    {
        ObjectId = 97003,
        ObjectName = "Ship",
        CrashBoxes = []
    };
}
