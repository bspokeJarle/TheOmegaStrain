using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Controls;
using TheOmegaStrain.Gameplay.Controls.KamikazeDroneControls;
using TheOmegaStrain.Game.Helpers;
using TheOmegaStrain.Runtime.Collision;

namespace TheOmegaStrain.Tests.Controls;

[TestClass]
public class ExplosionParticleEffectsTests
{
    [TestInitialize]
    public void Setup()
    {
        GameState.GamePlayState = new GamePlayState();
        GameState.ShipState = new ShipState();
        GameState.SurfaceState = new SurfaceState
        {
            AiObjects = new List<OmegaObject3D>(),
            GlobalMapPosition = new Vector3 { x = 0f, y = 0f, z = 0f }
        };
    }

    [TestMethod]
    public void CrashDetection_DecoyDoesNotCrashWithShip()
    {
        var ship = CreateCollisionObject("Ship", 7201);
        var decoy = CreateCollisionObject("DroneDecoy", 7202);

        CrashDetection.HandleCrashboxes(new List<OmegaObject3D> { ship, decoy }, isPaused: false);

        Assert.IsFalse(ship.ImpactStatus!.HasCrashed, "Decoy should not collide with the player ship.");
        Assert.IsFalse(decoy.ImpactStatus!.HasCrashed, "Decoy should not be destroyed by touching the player ship.");
    }

    [TestMethod]
    public void CrashDetection_DecoyDoesNotCrashWithParticles()
    {
        var decoy = CreateCollisionObject("DroneDecoy", 7203);
        var particle = CreateCollisionObject("Particle", 7204);
        particle.ImpactStatus = new ImpactStatus
        {
            HasCrashed = false,
            HasExploded = false,
            ObjectName = "KamikazeDrone"
        };

        CrashDetection.HandleCrashboxes(new List<OmegaObject3D> { particle, decoy }, isPaused: false);
        CrashDetection.HandleCrashboxes(new List<OmegaObject3D> { particle, decoy }, isPaused: false);

        Assert.IsFalse(particle.ImpactStatus!.HasCrashed, "Particles should ignore decoys instead of treating them as collision targets.");
        Assert.IsFalse(decoy.ImpactStatus!.HasCrashed, "Decoys should not be detonated by explosion particles.");
    }

    [TestMethod]
    public void DecoyExplosion_DirectHitExplodesWithoutWaitingForTimeout()
    {
        var decoy = CreateExplodingDecoy();
        var controls = new DecoyBeaconControls();

        controls.MoveObject(decoy, null, null);

        Assert.IsNotNull(decoy.Particles, "Decoy should keep its particle system during the explosion.");
        Assert.IsTrue(decoy.Particles.Particles.Count > 0, "Direct decoy hits should release explosion particles immediately.");
        Assert.AreEqual(0, decoy.CrashBoxes.Count, "Exploding decoy should clear crashboxes immediately so it cannot keep attracting collisions.");
        Assert.IsFalse(decoy.ImpactStatus!.HasCrashed, "Direct-hit explosion should consume the crash state immediately.");
    }

    [TestMethod]
    public void KamikazeDroneFindsDecoyCompensatedCoordinatesWithinTwoFrames()
    {
        GameState.GamePlayState.PowerUpsCollected = 1;

        var decoy = CreateNavigationDecoy(7102, worldX: -10_000f, worldZ: 0f, offsetX: 10_100f, offsetZ: 400f);
        GameState.SurfaceState.AiObjects.Add(decoy);

        var drone = CreateHuntingDrone();
        drone.ObjectOffsets = new Vector3 { x = 0f, y = 0f, z = 400f };
        var controls = new KamikazeDroneControls
        {
            StartHuntDateTime = DateTime.Now.AddSeconds(-1)
        };

        controls.MoveObject(drone, null, null);

        for (int frame = 0; frame < 2; frame++)
        {
            SetPrivateDateTime(controls, "LastMovementDateTime", DateTime.Now.AddSeconds(-0.1));
            controls.MoveObject(drone, null, null);
        }

        Assert.AreEqual(
            100f,
            drone.WorldPosition!.x,
            2f,
            "Drone should find the decoy's compensated hunt coordinate, not chase the decoy's raw WorldPosition.");
        Assert.AreEqual(
            0f,
            drone.WorldPosition.z,
            2f,
            "Drone should stay on the decoy's compensated hunt depth when the decoy is directly sideways.");
    }

    [TestMethod]
    public void KamikazeDroneChoosesClosestDecoyByCompensatedHuntPosition()
    {
        GameState.GamePlayState.PowerUpsCollected = 1;

        var nearDecoy = CreateNavigationDecoy(7301, worldX: -10_000f, worldZ: 0f, offsetX: 10_100f, offsetZ: 400f);
        var farDecoy = CreateNavigationDecoy(7302, worldX: -1000f, worldZ: 0f, offsetX: 0f, offsetZ: 0f);
        GameState.SurfaceState.AiObjects.Add(nearDecoy);
        GameState.SurfaceState.AiObjects.Add(farDecoy);

        var drone = CreateHuntingDrone();
        drone.ObjectOffsets = new Vector3 { x = 0f, y = 0f, z = 400f };
        var controls = new KamikazeDroneControls
        {
            StartHuntDateTime = DateTime.Now.AddSeconds(-1)
        };

        controls.MoveObject(drone, null, null);
        SetPrivateDateTime(controls, "LastMovementDateTime", DateTime.Now.AddSeconds(-0.1));

        controls.MoveObject(drone, null, null);

        Assert.IsTrue(
            drone.WorldPosition!.x > 0f,
            "Drone should chase the nearest decoy by compensated hunt position, not by raw WorldPosition.");
    }

    [TestMethod]
    public void KamikazeDroneHitsDecoyByCompensatedHuntPosition()
    {
        GameState.GamePlayState.PowerUpsCollected = 1;

        var decoy = CreateNavigationDecoy(7304, worldX: -10_000f, worldZ: 0f, offsetX: 10_000f, offsetZ: 400f);
        GameState.SurfaceState.AiObjects.Add(decoy);

        var drone = CreateHuntingDrone();
        drone.ObjectOffsets = new Vector3 { x = 0f, y = 0f, z = 400f };
        var controls = new KamikazeDroneControls
        {
            StartHuntDateTime = DateTime.Now.AddSeconds(-1)
        };

        controls.MoveObject(drone, null, null);

        Assert.IsTrue(drone.ImpactStatus!.HasCrashed, "Navigation overlap with a decoy should trigger the drone impact.");
        Assert.AreEqual("DroneDecoy", drone.ImpactStatus.ObjectName);
        Assert.IsTrue(decoy.ImpactStatus!.HasCrashed, "The decoy should receive the drone impact at its navigation position.");
        Assert.AreEqual("KamikazeDrone", decoy.ImpactStatus.ObjectName);
    }

    [TestMethod]
    public void KamikazeDroneExplosion_ReleasesVisibleExplosionParticles()
    {
        var drone = CreateExplodingDrone();
        var controls = new KamikazeDroneControls
        {
            StartHuntDateTime = DateTime.Now.AddSeconds(-1)
        };

        controls.MoveObject(drone, null, null);

        Assert.IsNotNull(drone.Particles, "Exploding AI objects should keep their particle system during the explosion.");
        Assert.IsTrue(drone.Particles.Particles.Count > 0, "Explosion should release the particle stream.");
        Assert.IsTrue(drone.Particles.Particles.Any(particle => particle.Visible), "Explosion particles should be advanced immediately so they render on the explosion frame.");
    }

    private static void SetPrivateDateTime(object instance, string fieldName, DateTime value)
    {
        var field = instance.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.IsNotNull(field, $"Expected private field {fieldName} to exist.");
        field.SetValue(instance, value);
    }

    private static OmegaObject3D CreateExplodingDecoy()
    {
        return new OmegaObject3D
        {
            ObjectId = 7101,
            ObjectName = "DroneDecoy",
            IsOnScreen = true,
            ParentSurface = null,
            WorldPosition = new Vector3 { x = 100f, y = 0f, z = 100f },
            ObjectOffsets = new Vector3 { x = -200f, y = 0f, z = 800f },
            Rotation = new Vector3 { x = 0f, y = 0f, z = 0f },
            Particles = new ParticlesAI(),
            CrashBoxes = CreateCrashBoxes(),
            ImpactStatus = new ImpactStatus
            {
                HasCrashed = true,
                ObjectName = "KamikazeDrone",
                ObjectHealth = 1
            },
            ObjectParts = CreateSimpleObjectParts()
        };
    }

    private static OmegaObject3D CreateCollisionObject(string objectName, int objectId)
    {
        return new OmegaObject3D
        {
            ObjectId = objectId,
            ObjectName = objectName,
            IsOnScreen = true,
            WorldPosition = new Vector3 { x = 0f, y = 0f, z = 0f },
            ObjectOffsets = new Vector3 { x = 0f, y = 0f, z = 0f },
            Rotation = new Vector3 { x = 0f, y = 0f, z = 0f },
            CrashBoxes = CreateCrashBoxes(),
            ImpactStatus = new ImpactStatus { ObjectName = objectName, ObjectHealth = 100 },
            ObjectParts = CreateSimpleObjectParts()
        };
    }

    private static OmegaObject3D CreateNavigationDecoy()
    {
        return CreateNavigationDecoy(7102, worldX: 300f, worldZ: 300f, offsetX: 0f, offsetZ: 1000f);
    }

    private static OmegaObject3D CreateNavigationDecoy(int objectId, float worldX, float worldZ, float offsetX, float offsetZ)
    {
        return new OmegaObject3D
        {
            ObjectId = objectId,
            ObjectName = "DroneDecoy",
            IsOnScreen = true,
            ParentSurface = null,
            WorldPosition = new Vector3 { x = worldX, y = 0f, z = worldZ },
            ObjectOffsets = new Vector3 { x = offsetX, y = 0f, z = offsetZ },
            Rotation = new Vector3 { x = 0f, y = 0f, z = 0f },
            Particles = new ParticlesAI(),
            CrashBoxes = CreateCrashBoxes(),
            ImpactStatus = new ImpactStatus(),
            ObjectParts = CreateSimpleObjectParts()
        };
    }

    private static OmegaObject3D CreateHuntingDrone()
    {
        return new OmegaObject3D
        {
            ObjectId = 7103,
            ObjectName = "KamikazeDrone",
            IsOnScreen = true,
            ParentSurface = null,
            WorldPosition = new Vector3 { x = 0f, y = 0f, z = 0f },
            ObjectOffsets = new Vector3 { x = 0f, y = 0f, z = 0f },
            Rotation = new Vector3 { x = 0f, y = 0f, z = 0f },
            Particles = new ParticlesAI(),
            CrashBoxes = CreateCrashBoxes(),
            ImpactStatus = new ImpactStatus { ObjectHealth = EnemySetup.KamikazeDroneHealth },
            ObjectParts = CreateSimpleObjectParts()
        };
    }

    private static OmegaObject3D CreateExplodingDrone()
    {
        return new OmegaObject3D
        {
            ObjectId = 7001,
            ObjectName = "KamikazeDrone",
            IsOnScreen = true,
            ParentSurface = null,
            WorldPosition = new Vector3 { x = 100f, y = 0f, z = 100f },
            ObjectOffsets = new Vector3 { x = 0f, y = 0f, z = 0f },
            Rotation = new Vector3 { x = 0f, y = 0f, z = 0f },
            Particles = new ParticlesAI(),
            CrashBoxes = new List<List<IVector3>>(),
            ImpactStatus = new ImpactStatus
            {
                HasCrashed = true,
                ObjectName = "Ship",
                ObjectHealth = 1
            },
            ObjectParts = CreateSimpleObjectParts()
        };
    }

    private static List<List<IVector3>> CreateCrashBoxes()
    {
        return
        [
            [
                new Vector3 { x = -5f, y = -5f, z = -5f },
                new Vector3 { x = 5f, y = 5f, z = 5f }
            ]
        ];
    }

    private static List<I3dObjectPart> CreateSimpleObjectParts()
    {
        return
        [
            new OmegaObjectPart3D
            {
                PartName = "Body",
                IsVisible = true,
                Triangles =
                [
                    new TriangleMeshWithColor
                    {
                        Color = "ffffff",
                        vert1 = new Vector3 { x = -10f, y = 0f, z = 0f },
                        vert2 = new Vector3 { x = 10f, y = 0f, z = 0f },
                        vert3 = new Vector3 { x = 0f, y = 20f, z = 0f }
                    }
                ]
            }
        ];
    }
}
