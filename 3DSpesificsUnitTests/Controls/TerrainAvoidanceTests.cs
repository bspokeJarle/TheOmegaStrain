using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;
using CommonUtilities.CommonSetup;
using Domain;
using GameAiAndControls.Controls.MotherShipSmallControls;
using GameAiAndControls.Helpers;
using _3dRotations.Helpers;
using TheOmegaStrain.Runtime.Collision;
using System.Reflection;
using static Domain._3dSpecificsImplementations;

namespace _3DSpesificsUnitTests.Controls;

[TestClass]
public class TerrainAvoidanceTests
{
    [TestInitialize]
    public void Setup()
    {
        GameState.GamePlayState = new GamePlayState();
        GameState.SurfaceState = new SurfaceState
        {
            GlobalMapPosition = new Vector3 { x = 0, y = 0, z = 0 },
            AiObjects = new List<_3dObject>()
        };
    }

    [TestMethod]
    public void TryStartTerrainRecovery_WhenAiObjectHitsSurface_ClearsCrashAndMovesUp()
    {
        var aiObject = CreateAiObject(1001, "KamikazeDrone", "Surface");
        GameState.SurfaceState.AiObjects.Add(aiObject);

        var started = TerrainAvoidanceHelpers.TryStartTerrainRecovery(aiObject);
        var originalY = aiObject.ObjectOffsets!.y;
        var originalX = aiObject.WorldPosition!.x;

        var applied = TerrainAvoidanceHelpers.ApplyTerrainRecovery(aiObject, 0.1f);

        Assert.IsTrue(started, "Terrain contact should start recovery for AIObjects.");
        Assert.IsTrue(applied, "Started recovery should apply movement.");
        Assert.IsFalse(aiObject.ImpactStatus!.HasCrashed, "Terrain recovery should consume the crash signal.");
        Assert.AreEqual(string.Empty, aiObject.ImpactStatus.ObjectName);
        Assert.IsTrue(aiObject.ObjectOffsets.y < originalY, "Recovery should lift the object away from the surface.");
        Assert.AreNotEqual(originalX, aiObject.WorldPosition.x, "Recovery should also steer horizontally away from terrain.");
    }

    [DataTestMethod]
    [DataRow("Seeder", 2010)]
    [DataRow("KamikazeDrone", 2011)]
    [DataRow("ZeppelinBomber", 2012)]
    [DataRow("MotherShipSmall", 2013)]
    [DataRow("MotherShipMedium", 2014)]
    [DataRow("MotherShipLarge", 2015)]
    [DataRow("SpaceSwan", 2016)]
    public void TryStartTerrainRecovery_WhenAvoidanceCapableAiHitsTower_UsesTowerAsTerrainObstacle(string objectName, int objectId)
    {
        var aiObject = CreateAiObject(objectId, objectName, "Tower");
        GameState.SurfaceState.AiObjects.Add(aiObject);
        var originalY = aiObject.ObjectOffsets!.y;
        var originalX = aiObject.WorldPosition!.x;

        var started = TerrainAvoidanceHelpers.TryStartTerrainRecovery(aiObject);
        var applied = TerrainAvoidanceHelpers.ApplyTerrainRecovery(aiObject, 0.1f);

        Assert.IsTrue(started, $"{objectName} should treat towers as passive terrain obstacles for AI avoidance.");
        Assert.IsTrue(applied, $"{objectName} should apply tower recovery movement.");
        Assert.IsFalse(aiObject.ImpactStatus!.HasCrashed);
        Assert.IsTrue(aiObject.ObjectOffsets.y < originalY, $"{objectName} should lift away from the tower contact.");
        Assert.AreNotEqual(originalX, aiObject.WorldPosition.x, $"{objectName} should steer horizontally away from the tower.");
    }

    [DataTestMethod]
    [DataRow("LargePalm", 2020)]
    [DataRow("SmallPalm", 2021)]
    [DataRow("BambooHut", 2022)]
    [DataRow("LeafTree", 2023)]
    public void TryStartTerrainRecovery_WhenAiHitsLandBasedDecoration_UsesItAsTerrainObstacle(string obstacleObjectName, int objectId)
    {
        var aiObject = CreateAiObject(objectId, "Seeder", obstacleObjectName);
        GameState.SurfaceState.AiObjects.Add(aiObject);
        var originalY = aiObject.ObjectOffsets!.y;

        var started = TerrainAvoidanceHelpers.TryStartTerrainRecovery(aiObject);
        var applied = TerrainAvoidanceHelpers.ApplyTerrainRecovery(aiObject, 0.1f);

        Assert.IsTrue(started, $"Seeder should treat {obstacleObjectName} as a passive terrain obstacle for AI avoidance.");
        Assert.IsTrue(applied, $"Seeder should apply recovery movement away from {obstacleObjectName}.");
        Assert.IsFalse(aiObject.ImpactStatus!.HasCrashed);
        Assert.IsTrue(aiObject.ObjectOffsets.y < originalY, $"Seeder should lift away from {obstacleObjectName} contact.");
    }

    [DataTestMethod]
    [DataRow("SnowTower", 2031)]
    [DataRow("SmallIgloo", 2032)]
    [DataRow("LargeIgloo", 2033)]
    public void TryStartTerrainRecovery_WhenAiHitsWinterLandmark_UsesItAsTerrainObstacle(string obstacleObjectName, int objectId)
    {
        var aiObject = CreateAiObject(objectId, "MotherShipMedium", obstacleObjectName);
        GameState.SurfaceState.AiObjects.Add(aiObject);

        var started = TerrainAvoidanceHelpers.TryStartTerrainRecovery(aiObject);

        Assert.IsTrue(started, $"MotherShipMedium should treat {obstacleObjectName} as terrain avoidance, not a combat crash.");
        Assert.IsFalse(aiObject.ImpactStatus!.HasCrashed);
    }

    [TestMethod]
    public void CrashDetection_WhenMotherShipApproachesTower_StartsRecoveryBeforeCrashBoxOverlap()
    {
        var motherShip = CreateProximityObject(3001, "MotherShipSmall", centerX: 0f);
        var tower = CreateProximityObject(3002, "Tower", centerX: 300f);
        GameState.SurfaceState.AiObjects.Add(motherShip);
        float originalWorldX = motherShip.WorldPosition!.x;

        CrashDetection.HandleCrashboxes(new List<_3dObject> { motherShip, tower }, isPaused: false);
        var applied = TerrainAvoidanceHelpers.ApplyTerrainRecovery(motherShip, 0.1f);

        Assert.IsFalse(motherShip.ImpactStatus!.HasCrashed, "Proximity avoidance should not create a combat crash.");
        Assert.IsTrue(applied, "A nearby tower should start mothership terrain recovery before crashboxes overlap.");
        Assert.IsTrue(motherShip.WorldPosition!.x < originalWorldX, "The mothership should steer away from the tower before passing through it.");
    }

    [TestMethod]
    public void CrashDetection_WhenMotherShipSmallTowerIsOutsideSmallProactiveRange_DoesNotStartRecovery()
    {
        var motherShip = CreateProximityObject(3003, "MotherShipSmall", centerX: 0f);
        var tower = CreateProximityObject(3004, "Tower", centerX: 450f);
        GameState.SurfaceState.AiObjects.Add(motherShip);
        float originalWorldX = motherShip.WorldPosition!.x;

        CrashDetection.HandleCrashboxes(new List<_3dObject> { motherShip, tower }, isPaused: false);
        var applied = TerrainAvoidanceHelpers.ApplyTerrainRecovery(motherShip, 0.1f);

        Assert.IsFalse(motherShip.ImpactStatus!.HasCrashed, "Distant proximity should not create a combat crash.");
        Assert.IsFalse(applied, "MotherShipSmall should not retreat from distant terrain objects that are outside its smaller proactive range.");
        Assert.AreEqual(originalWorldX, motherShip.WorldPosition!.x);
    }

    [TestMethod]
    public void TryStartTerrainRecovery_WhenObjectIsNotInAiObjects_DoesNotReact()
    {
        var nonAiObject = CreateAiObject(1003, "KamikazeDrone", "Surface");

        var started = TerrainAvoidanceHelpers.TryStartTerrainRecovery(nonAiObject);

        Assert.IsFalse(started, "Only objects registered in SurfaceState.AiObjects should react.");
        Assert.IsTrue(nonAiObject.ImpactStatus!.HasCrashed);
        Assert.AreEqual("Surface", nonAiObject.ImpactStatus.ObjectName);
    }

    [TestMethod]
    public void TryStartTerrainRecovery_WhenBomberBombHitsSurface_DoesNotReact()
    {
        var bomb = CreateAiObject(1004, "BomberBomb", "Surface");
        GameState.SurfaceState.AiObjects.Add(bomb);

        var started = TerrainAvoidanceHelpers.TryStartTerrainRecovery(bomb);

        Assert.IsFalse(started, "BomberBomb should keep its surface crash behavior.");
        Assert.IsTrue(bomb.ImpactStatus!.HasCrashed);
        Assert.AreEqual("Surface", bomb.ImpactStatus.ObjectName);
    }

    [TestMethod]
    public void MotherShipSmall_MoveObject_WhenSurfaceContactOccurs_DoesNotTakeDamage()
    {
        var motherShip = CreateAiObject(1005, "MotherShipSmall", "Surface");
        motherShip.ImpactStatus!.ObjectHealth = EnemySetup.MotherShipSmallHealth;
        motherShip.ObjectOffsets = new Vector3 { x = 0, y = 75f, z = 400f };
        GameState.SurfaceState.AiObjects.Add(motherShip);

        var controls = new MotherShipSmallControls();

        controls.MoveObject(motherShip, null, null);

        Assert.IsFalse(motherShip.ImpactStatus.HasCrashed, "MotherShip should consume terrain contact as avoidance, not damage.");
        Assert.AreEqual(EnemySetup.MotherShipSmallHealth, motherShip.ImpactStatus.ObjectHealth);
        Assert.IsTrue(motherShip.ObjectOffsets!.y < 75f, "MotherShip recovery should lift it away from the terrain.");
    }

    [TestMethod]
    public void MotherShipSmall_MoveObject_WhenChargingIntoTower_CancelsRamAndSteersAway()
    {
        var motherShip = CreateAiObject(1006, "MotherShipSmall", "Tower");
        motherShip.ImpactStatus!.ObjectHealth = EnemySetup.MotherShipSmallHealth;
        motherShip.WorldPosition = new Vector3 { x = 1000f, y = 0f, z = 0f };
        motherShip.ObjectOffsets = new Vector3 { x = 0f, y = 75f, z = 400f };
        GameState.SurfaceState.AiObjects.Add(motherShip);

        var controls = new MotherShipSmallControls();
        SetPrivateField(controls, "_syncInitialized", true);
        SetPrivateField(controls, "_syncY", 75f);
        SetPrivateField(controls, "_lastMovementTime", DateTime.Now.AddSeconds(-0.1));

        var ramState = GetPrivateField(controls, "_ramState");
        SetRamStateField(ramState, "RamCycleStart", DateTime.Now.AddSeconds(-7));
        SetRamStateField(ramState, "RamTargetLocked", true);
        SetRamStateField(ramState, "RamTargetWorldPosition", new Vector3 { x = 0f, y = 0f, z = 0f });
        SetRamStateField(ramState, "IsCharging", true);

        controls.MoveObject(motherShip, null, null);

        Assert.IsFalse(motherShip.ImpactStatus.HasCrashed, "Tower contact should be consumed as avoidance, not damage.");
        Assert.AreEqual(EnemySetup.MotherShipSmallHealth, motherShip.ImpactStatus.ObjectHealth);
        Assert.IsTrue(motherShip.WorldPosition!.x > 1000f, "An active ram should be cancelled so recovery can steer the MotherShip away from the tower.");
        Assert.IsTrue(motherShip.ObjectOffsets!.y < 75f, "Recovery should still lift the MotherShip away from terrain.");
    }

    private static _3dObject CreateAiObject(int objectId, string objectName, string contactName)
    {
        return new _3dObject
        {
            ObjectId = objectId,
            ObjectName = objectName,
            IsActive = true,
            WorldPosition = new Vector3 { x = 1000f + objectId, y = 0f, z = 2000f },
            ObjectOffsets = new Vector3 { x = 0f, y = 100f, z = 400f },
            Rotation = new Vector3(),
            CrashBoxes = new List<List<IVector3>>
            {
                new()
                {
                    new Vector3 { x = -10f, y = -10f, z = -10f },
                    new Vector3 { x = 10f, y = 10f, z = 10f }
                }
            },
            ImpactStatus = new ImpactStatus
            {
                HasCrashed = true,
                ObjectName = contactName,
                ObjectHealth = 100
            },
            ObjectParts = new List<I3dObjectPart>
            {
                new _3dObjectPart
                {
                    PartName = "Body",
                    IsVisible = true,
                    Triangles = new List<ITriangleMeshWithColor>
                    {
                        new TriangleMeshWithColor
                        {
                            Color = "FFFFFF",
                            vert1 = new Vector3 { x = 0f, y = 0f, z = 0f },
                            vert2 = new Vector3 { x = 1f, y = 0f, z = 0f },
                            vert3 = new Vector3 { x = 0f, y = 1f, z = 0f }
                        }
                    }
                }
            }
        };
    }

    private static _3dObject CreateProximityObject(int objectId, string objectName, float centerX)
    {
        return new _3dObject
        {
            ObjectId = objectId,
            ObjectName = objectName,
            IsActive = true,
            IsOnScreen = true,
            WorldPosition = new Vector3 { x = 1000f, y = 0f, z = 2000f },
            ObjectOffsets = new Vector3 { x = centerX, y = 0f, z = 0f },
            Rotation = new Vector3(),
            CrashBoxes = new List<List<IVector3>>
            {
                _3dObjectHelpers.GenerateCrashBoxCorners(
                    new Vector3 { x = -10f, y = -10f, z = -10f },
                    new Vector3 { x = 10f, y = 10f, z = 10f })
            },
            ImpactStatus = new ImpactStatus
            {
                HasCrashed = false,
                ObjectHealth = 100
            },
            ObjectParts = new List<I3dObjectPart>
            {
                new _3dObjectPart
                {
                    PartName = "Body",
                    IsVisible = true,
                    Triangles = new List<ITriangleMeshWithColor>
                    {
                        new TriangleMeshWithColor
                        {
                            Color = "FFFFFF",
                            vert1 = new Vector3 { x = 0f, y = 0f, z = 0f },
                            vert2 = new Vector3 { x = 1f, y = 0f, z = 0f },
                            vert3 = new Vector3 { x = 0f, y = 1f, z = 0f }
                        }
                    }
                }
            }
        };
    }

    private static object GetPrivateField(object target, string fieldName)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Expected private field '{fieldName}' to exist.");
        return field.GetValue(target)!;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Expected private field '{fieldName}' to exist.");
        field.SetValue(target, value);
    }

    private static void SetRamStateField(object state, string fieldName, object value)
    {
        var field = state.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Expected ram state field '{fieldName}' to exist.");
        field.SetValue(state, value);
    }
}
