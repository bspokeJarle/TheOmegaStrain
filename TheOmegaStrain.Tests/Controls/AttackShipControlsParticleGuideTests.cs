using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Game.World.Objects;
using TheOmegaStrain.Gameplay.Controls;

namespace TheOmegaStrain.Tests.Controls;

[TestClass]
public class AttackShipControlsParticleGuideTests
{
    private static readonly OmegaMeshRotation Rotate = new();

    [TestMethod]
    public void LethalCrash_StartsExplosionAndClearsCollisionBoxesWithoutPursuit()
    {
        var ship = AttackShip.CreateAttackShip(null!);
        ship.WorldPosition = new Vector3(50000f, 0f, 50000f);
        ship.ObjectOffsets = new Vector3(0f, -150f, 100f);
        ship.ImpactStatus = new ImpactStatus
        {
            ObjectHealth = 155, HasCrashed = true, ObjectName = "Ship"
        };
        var controls = new AttackShipControls();
        controls.MoveObject(ship, null, null);
        Assert.IsTrue(ship.ImpactStatus.ObjectHealth <= 0);
        Assert.IsFalse(ship.ImpactStatus.HasCrashed == true);
        Assert.AreEqual(0, ship.CrashBoxes.Count);
        Assert.AreEqual(50000f, ship.WorldPosition.x);
        Assert.AreEqual(50000f, ship.WorldPosition.z);
        Assert.IsNull(controls.ShipTargetWorldPosition);
    }

    [TestMethod]
    public void MoveObject_PublishesTransformSoOriginalAndRenderCopyHaveSameMapMarker()
    {
        var oldSurface = TheOmegaStrain.Common.CommonGlobalState.GameState.SurfaceState;
        var oldShip = TheOmegaStrain.Common.CommonGlobalState.GameState.ShipState;
        try
        {
            TheOmegaStrain.Common.CommonGlobalState.GameState.SurfaceState = new SurfaceState
            {
                GlobalMapPosition = new Vector3(50000f, 0f, 50000f)
            };
            TheOmegaStrain.Common.CommonGlobalState.GameState.ShipState =
                new TheOmegaStrain.Common.CommonGlobalState.States.ShipState
                {
                    ShipObjectOffsets = new Vector3()
                };
            var original = AttackShip.CreateAttackShip(null!);
            var renderCopy = AttackShip.CreateAttackShip(null!);
            renderCopy.ObjectId = original.ObjectId;
            renderCopy.WorldPosition = new Vector3(50500f, 0f, 50400f);
            renderCopy.ObjectOffsets = new Vector3(35f, -150f, 100f);
            renderCopy.Rotation = new Vector3(63f, 40f, 90f);
            TheOmegaStrain.Common.CommonGlobalState.GameState.SurfaceState.AiObjects.Add(original);

            new AttackShipControls().MoveObject(renderCopy, null, null);

            var expected = SurfacePositionSyncHelpers.GetMinimapMarkerWorldPosition(renderCopy)!;
            var actual = SurfacePositionSyncHelpers.GetMinimapMarkerWorldPosition(original)!;
            Assert.AreEqual(expected.x, actual.x, 0.001f);
            Assert.AreEqual(expected.z, actual.z, 0.001f);
            Assert.AreEqual(renderCopy.Rotation.y, original.Rotation.y);
            Assert.AreEqual(renderCopy.ObjectOffsets.y, original.ObjectOffsets.y);
            Assert.AreNotSame(renderCopy.Rotation, original.Rotation);
            Assert.AreNotSame(renderCopy.ObjectOffsets, original.ObjectOffsets);
        }
        finally
        {
            TheOmegaStrain.Common.CommonGlobalState.GameState.SurfaceState = oldSurface;
            TheOmegaStrain.Common.CommonGlobalState.GameState.ShipState = oldShip;
        }
    }

    [TestMethod]
    public void CreateAttackShip_HidesParticleGuideParts()
    {
        var ship = AttackShip.CreateAttackShip(parentSurface: null!);

        AssertGuidePartIsHidden(ship, "AttackShipLeftEngineStartGuide");
        AssertGuidePartIsHidden(ship, "AttackShipLeftEngineDirectionGuide");
        AssertGuidePartIsHidden(ship, "AttackShipRightEngineStartGuide");
        AssertGuidePartIsHidden(ship, "AttackShipRightEngineDirectionGuide");
    }

    [TestMethod]
    public void CreateAttackShip_EngineParticleGuidesStartBehindNozzleWithClearance()
    {
        var ship = AttackShip.CreateAttackShip(parentSurface: null!);

        AssertEngineGuideClearance(
            ship,
            "AttackShipLeftEngineStartGuide",
            "AttackShipLeftEngineDirectionGuide",
            expectedLateralY: 28.5f * 1.5f);

        AssertEngineGuideClearance(
            ship,
            "AttackShipRightEngineStartGuide",
            "AttackShipRightEngineDirectionGuide",
            expectedLateralY: -28.5f * 1.5f);
    }

    [TestMethod]
    public void MoveObject_ParticlesSpawnFromCurrentFrameEngineStartNotPreviousFrame()
    {
        var ship = AttackShip.CreateAttackShip(parentSurface: null!);
        ship.WorldPosition = new Vector3 { x = 25f, y = 0f, z = 800f };
        ship.ObjectOffsets = new Vector3 { x = 10f, y = 20f, z = 430f };

        var particles = new CapturingParticles();
        ship.Particles = particles;

        var controls = new AttackShipControls();

        var staleRotation = new Vector3 { x = 55f, y = 75f, z = 20f };
        controls.SetParticleGuideCoordinates(
            RotateGuide(ship, "AttackShipLeftEngineStartGuide", staleRotation),
            null!);
        controls.SetParticleGuideCoordinates(
            null!,
            RotateGuide(ship, "AttackShipLeftEngineDirectionGuide", staleRotation));

        controls.MoveObject(ship, null, null);
        particles.Calls.Clear();

        controls.MoveObject(ship, null, null);

        Assert.IsTrue(particles.Calls.Count >= 2, "Expected both AttackShip engines to emit particles.");

        var actualSpawn = Centroid(particles.Calls[0].start);
        var expectedSpawn = Centroid(RotateGuide(ship, "AttackShipLeftEngineStartGuide", ship.Rotation!));

        Assert.AreEqual(expectedSpawn.x, actualSpawn.x, 0.01f,
            $"Particle spawn.x should match CURRENT frame engine start. expected={expectedSpawn.x:F3}, actual={actualSpawn.x:F3}");
        Assert.AreEqual(expectedSpawn.y, actualSpawn.y, 0.01f,
            $"Particle spawn.y should match CURRENT frame engine start. expected={expectedSpawn.y:F3}, actual={actualSpawn.y:F3}");
        Assert.AreEqual(expectedSpawn.z, actualSpawn.z, 0.01f,
            $"Particle spawn.z should match CURRENT frame engine start. expected={expectedSpawn.z:F3}, actual={actualSpawn.z:F3}");
    }

    [TestMethod]
    public void ReleaseParticles_ActualParticlePositionStartsClearOfRotatedNozzle()
    {
        var ship = AttackShip.CreateAttackShip(parentSurface: null!);
        ship.Rotation = new Vector3 { x = 0f, y = 0f, z = 90f };
        ship.WorldPosition = new Vector3();

        var controls = new AttackShipControls();

        controls.ReleaseParticles(ship);
        controls.ReleaseParticles(ship);

        Assert.IsNotNull(ship.Particles);
        Assert.IsTrue(ship.Particles!.Particles.Count > 0, "Expected AttackShip to emit real particles.");

        var spawn = ship.Particles.Particles[0].Position;
        Assert.IsNotNull(spawn, "Particle should store its birth position.");

        var rotatedNozzle = Rotate.RotatePoint(
            90f,
            new Vector3 { x = -54.2f * 1.5f, y = 18.5f * 1.5f, z = 0f },
            'Z');

        Assert.AreEqual(rotatedNozzle.x - 10f * 1.5f, spawn!.x, 0.01f,
            "Rotated particle birth point should move slightly wider than the engine pod.");
        Assert.IsTrue(spawn.y <= rotatedNozzle.y - 42f * 1.5f,
            $"Particle birth y={spawn.y:F1}; expected clear behind rotated nozzle y={rotatedNozzle.y:F1}.");
    }

    private static void AssertGuidePartIsHidden(OmegaObject3D ship, string partName)
    {
        var part = ship.ObjectParts.Find(p => p.PartName == partName);

        Assert.IsNotNull(part, $"{partName} should exist.");
        Assert.IsFalse(part!.IsVisible, $"{partName} should be hidden like other particle guide parts.");
    }

    private static void AssertEngineGuideClearance(
        OmegaObject3D ship,
        string startPartName,
        string directionPartName,
        float expectedLateralY)
    {
        const float nozzleCapX = -54.2f * 1.5f;
        const float minimumClearanceBehindNozzle = 42f * 1.5f;

        var start = Centroid(GetGuideTriangle(ship, startPartName));
        var guide = Centroid(GetGuideTriangle(ship, directionPartName));

        Assert.IsTrue(start.x <= nozzleCapX - minimumClearanceBehindNozzle,
            $"{startPartName} starts at x={start.x:F1}; expected at least {minimumClearanceBehindNozzle:F1} units behind nozzle cap x={nozzleCapX:F1}.");
        Assert.AreEqual(expectedLateralY, start.y, 0.01f,
            $"{startPartName} should stay centered on its engine pod.");
        Assert.IsTrue(guide.x < start.x,
            $"{directionPartName} must sit behind {startPartName} so exhaust velocity points away from the hull.");
        Assert.AreEqual(14f * 1.5f, start.x - guide.x, 0.01f,
            $"{directionPartName} should preserve the tuned exhaust guide distance.");
    }

    private static ITriangleMeshWithColorAndTexture GetGuideTriangle(OmegaObject3D ship, string partName)
    {
        var part = ship.ObjectParts.Find(p => p.PartName == partName);

        Assert.IsNotNull(part, $"{partName} should exist.");
        Assert.IsTrue(part!.Triangles.Count > 0, $"{partName} should have a guide triangle.");

        return part.Triangles[0];
    }

    private static ITriangleMeshWithColorAndTexture RotateGuide(
        OmegaObject3D ship,
        string partName,
        IVector3 rotation)
    {
        var mesh = new List<ITriangleMeshWithColorAndTexture>
        {
            OmegaObjectHelpers.CopyTriangle(GetGuideTriangle(ship, partName))
        };

        mesh = Rotate.RotateZMesh(mesh, rotation.z);
        mesh = Rotate.RotateYMesh(mesh, rotation.y);
        mesh = Rotate.RotateXMesh(mesh, rotation.x);

        return mesh[0];
    }

    private static Vector3 Centroid(ITriangleMeshWithColorAndTexture tri) => new()
    {
        x = (tri.vert1.x + tri.vert2.x + tri.vert3.x) / 3f,
        y = (tri.vert1.y + tri.vert2.y + tri.vert3.y) / 3f,
        z = (tri.vert1.z + tri.vert2.z + tri.vert3.z) / 3f,
    };

    private sealed class CapturingParticles : IParticles
    {
        public IObjectMovement ParentShip { get; set; } = null!;
        public List<IParticle> Particles { get; set; } = new();
        public float LifeMultiplier { get; set; } = 1f;
        public int MaxParticlesOverride { get; set; }

        public readonly List<(ITriangleMeshWithColorAndTexture trajectory, ITriangleMeshWithColorAndTexture start)> Calls = new();

        public void ReleaseParticles(
            ITriangleMeshWithColorAndTexture Trajectory,
            ITriangleMeshWithColorAndTexture StartPosition,
            IVector3 WorldPosition,
            IObjectMovement ParentShip,
            int Thrust,
            bool? explosion,
            float upwardVelocityBoost = 0f)
        {
            Calls.Add((Trajectory, StartPosition));
        }

        public void MoveParticles()
        {
        }
    }
}
