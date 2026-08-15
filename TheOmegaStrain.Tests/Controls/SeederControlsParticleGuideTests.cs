using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Controls.SeederControls;

namespace TheOmegaStrain.Tests.Controls;

[TestClass]
public class SeederControlsParticleGuideTests
{
    [TestInitialize]
    public void Setup()
    {
        GameState.SurfaceState = new SurfaceState
        {
            AiObjects = new List<OmegaObject3D>(),
            SurfaceViewportObject = new OmegaObject3D
            {
                ObjectId = 1,
                ObjectName = "Surface",
                ObjectOffsets = new Vector3 { x = 105f, y = 500f, z = 400f },
                WorldPosition = new Vector3()
            }
        };
    }

    [TestMethod]
    public void ReleaseParticles_UsesSeederWorldPositionWithoutSurfaceXOffset()
    {
        var particles = new CapturingParticles();
        var seeder = new OmegaObject3D
        {
            ObjectId = 2,
            ObjectName = "Seeder",
            WorldPosition = new Vector3 { x = 1000f, y = 0f, z = 2000f },
            ObjectOffsets = new Vector3 { x = 0f, y = -200f, z = 600f },
            Particles = particles
        };
        var controls = new SeederControls
        {
            ParentObject = seeder
        };
        controls.SetParticleGuideCoordinates(PointTriangle(0f, 31.2f, 0f), PointTriangle(0f, 131.2f, 0f));

        controls.ReleaseParticles(seeder);

        Assert.IsNotNull(particles.WorldPosition);
        Assert.AreEqual(1000f, particles.WorldPosition!.x, 0.001f,
            "A centered Seeder guide must not be shifted sideways by the surface viewport X offset.");
        Assert.AreEqual(2000f, particles.WorldPosition.z, 0.001f);
    }

    [TestMethod]
    public void HandleCrash_BulletRequiresThreeHitsToDestroySeeder()
    {
        var seeder = CreateSeederCollisionObject();
        var controls = new SeederControls();

        HitSeeder(controls, seeder, "Bullet");

        Assert.AreEqual(EnemySetup.SeederHealth - WeaponSetup.GetWeaponDamage("Bullet"), seeder.ImpactStatus!.ObjectHealth);
        Assert.IsFalse(seeder.ImpactStatus.HasCrashed, "Non-fatal bullet hit should be consumed after applying damage.");
        Assert.IsTrue(seeder.CrashBoxes.Count > 0);

        HitSeeder(controls, seeder, "Bullet");

        Assert.AreEqual(EnemySetup.SeederHealth - (WeaponSetup.GetWeaponDamage("Bullet") * 2), seeder.ImpactStatus.ObjectHealth);
        Assert.IsFalse(seeder.ImpactStatus.HasCrashed);
        Assert.IsTrue(seeder.CrashBoxes.Count > 0);

        HitSeeder(controls, seeder, "Bullet");

        Assert.IsTrue(seeder.ImpactStatus.ObjectHealth <= 0);
        Assert.AreEqual(0, seeder.CrashBoxes.Count, "Third bullet should destroy the seeder.");
    }

    [TestMethod]
    public void HandleCrash_LazerDestroysSeederInOneHit()
    {
        var seeder = CreateSeederCollisionObject();
        var controls = new SeederControls();

        HitSeeder(controls, seeder, "Lazer");

        Assert.IsTrue(seeder.ImpactStatus!.ObjectHealth <= 0);
        Assert.AreEqual(0, seeder.CrashBoxes.Count, "One lazer hit should destroy the seeder.");
    }

    [TestMethod]
    public void HandleCrash_DroneDecoyDestroysSeederInOneHit()
    {
        var seeder = CreateSeederCollisionObject();
        var controls = new SeederControls();

        HitSeeder(controls, seeder, "DroneDecoy");

        Assert.IsTrue(seeder.ImpactStatus!.ObjectHealth <= 0);
        Assert.AreEqual(0, seeder.CrashBoxes.Count, "A decoy hit should damage seeders through the normal seeder crash flow.");
    }

    [TestMethod]
    public void MoveObject_ExplodingSeederKeepsHitFrameTransformAfterSurfaceScroll()
    {
        GameState.SurfaceState.GlobalMapPosition = new Vector3 { x = 0f, y = 40f, z = 0f };
        var seeder = CreateSeederCollisionObject();
        seeder.WorldPosition = new Vector3 { x = 1200f, y = 4f, z = 2400f };
        seeder.ObjectOffsets = new Vector3 { x = 25f, y = -180f, z = 640f };
        seeder.Rotation = new Vector3 { x = 90f, y = 0f, z = 42f };
        var controls = new SeederControls();

        seeder.ImpactStatus!.HasCrashed = true;
        seeder.ImpactStatus.ObjectName = "Lazer";
        controls.MoveObject(seeder, audioPlayer: null, soundRegistry: null);

        var anchoredWorld = Copy(seeder.WorldPosition!);
        var anchoredOffsets = Copy(seeder.ObjectOffsets!);
        var anchoredRotation = Copy(seeder.Rotation!);

        GameState.SurfaceState.GlobalMapPosition = new Vector3 { x = 0f, y = 180f, z = 0f };
        controls.MoveObject(seeder, audioPlayer: null, soundRegistry: null);

        Assert.AreEqual(anchoredWorld.x, seeder.WorldPosition!.x, 0.001f);
        Assert.AreEqual(anchoredWorld.y, seeder.WorldPosition.y, 0.001f);
        Assert.AreEqual(anchoredWorld.z, seeder.WorldPosition.z, 0.001f);
        Assert.AreEqual(anchoredOffsets.x, seeder.ObjectOffsets!.x, 0.001f);
        Assert.AreEqual(anchoredOffsets.y, seeder.ObjectOffsets.y, 0.001f,
            "Seeder explosion must not keep surface-syncing after the hit frame.");
        Assert.AreEqual(anchoredOffsets.z, seeder.ObjectOffsets.z, 0.001f);
        Assert.AreEqual(anchoredRotation.z, seeder.Rotation!.z, 0.001f);
    }

    private static void HitSeeder(SeederControls controls, OmegaObject3D seeder, string weaponName)
    {
        seeder.ImpactStatus!.HasCrashed = true;
        seeder.ImpactStatus.ObjectName = weaponName;
        controls.HandleCrash(seeder);
    }

    private static OmegaObject3D CreateSeederCollisionObject()
    {
        return new OmegaObject3D
        {
            ObjectId = 10,
            ObjectName = "Seeder",
            WorldPosition = new Vector3 { x = 1000f, y = 0f, z = 2000f },
            ObjectOffsets = new Vector3 { x = 0f, y = -200f, z = 600f },
            Rotation = new Vector3(),
            CrashBoxes =
            [
                [
                    new Vector3 { x = -10f, y = -10f, z = -10f },
                    new Vector3 { x = 10f, y = 10f, z = 10f }
                ]
            ],
            ImpactStatus = new ImpactStatus { ObjectHealth = EnemySetup.SeederHealth },
            ObjectParts =
            [
                new OmegaObjectPart3D
                {
                    PartName = "Body",
                    IsVisible = true,
                    Triangles =
                    [
                        PointTriangle(0f, 0f, 0f)
                    ]
                }
            ]
        };
    }

    private static TriangleMeshWithColor PointTriangle(float x, float y, float z)
    {
        return new TriangleMeshWithColor
        {
            Color = "FFFFFF",
            vert1 = new Vector3 { x = x, y = y, z = z },
            vert2 = new Vector3 { x = x, y = y, z = z },
            vert3 = new Vector3 { x = x, y = y, z = z },
            noHidden = true
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

    private sealed class CapturingParticles : IParticles
    {
        public IObjectMovement ParentShip { get; set; } = null!;
        public List<IParticle> Particles { get; set; } = new();
        public float LifeMultiplier { get; set; } = 1f;
        public int MaxParticlesOverride { get; set; }
        public Vector3? WorldPosition { get; private set; }

        public void ReleaseParticles(
            ITriangleMeshWithColor Trajectory,
            ITriangleMeshWithColor StartPosition,
            IVector3 WorldPosition,
            IObjectMovement ParentShip,
            int Thrust,
            bool? explosion,
            float upwardVelocityBoost = 0f)
        {
            this.WorldPosition = new Vector3
            {
                x = WorldPosition.x,
                y = WorldPosition.y,
                z = WorldPosition.z
            };
        }

        public void MoveParticles()
        {
        }
    }
}
