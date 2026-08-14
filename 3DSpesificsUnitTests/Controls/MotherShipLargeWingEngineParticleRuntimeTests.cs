using _3dRotations.World.Objects;
using CommonUtilities.OmegaEngineAdapters;
using CommonUtilities.CommonGlobalState;
using Domain;
using GameAiAndControls.Controls.MotherShipMediumControls;
using static Domain._3dSpecificsImplementations;

namespace _3DSpesificsUnitTests.Controls;

[TestClass]
public class MotherShipLargeWingEngineParticleRuntimeTests
{
    private static readonly OmegaMeshRotation Rotate = new();

    private sealed class CapturingParticles : IParticles
    {
        public IObjectMovement ParentShip { get; set; } = null!;
        public List<IParticle> Particles { get; set; } = new();
        public float LifeMultiplier { get; set; } = 1f;
        public int MaxParticlesOverride { get; set; }

        public readonly List<(ITriangleMeshWithColor trajectory, ITriangleMeshWithColor start)> Calls = new();

        public void ReleaseParticles(ITriangleMeshWithColor Trajectory, ITriangleMeshWithColor StartPosition, IVector3 WorldPosition, IObjectMovement ParentShip, int Thrust, bool? explosion, float upwardVelocityBoost = 0f)
        {
            Calls.Add((Trajectory, StartPosition));
        }

        public void MoveParticles() { }
    }

    private static List<ITriangleMeshWithColor> ApplyShipRotation(List<ITriangleMeshWithColor> tris, Vector3 rotation)
    {
        var r = Rotate.RotateZMesh(tris, rotation.z);
        r = Rotate.RotateYMesh(r, rotation.y);
        r = Rotate.RotateXMesh(r, rotation.x);
        return r;
    }

    private static Vector3 Centroid(ITriangleMeshWithColor tri) => new Vector3
    {
        x = (tri.vert1.x + tri.vert2.x + tri.vert3.x) / 3f,
        y = (tri.vert1.y + tri.vert2.y + tri.vert3.y) / 3f,
        z = (tri.vert1.z + tri.vert2.z + tri.vert3.z) / 3f,
    };

    [TestMethod]
    public void RuntimePipeline_wing_engine_starts_must_stay_on_opposite_sides_of_ship()
    {
        GameState.SurfaceState.GlobalMapPosition = new Vector3 { x = 0f, y = 0f, z = 0f };
        GameState.SurfaceState.AiObjects.Clear();

        var ship = MotherShipLarge.CreateMotherShipLarge(parentSurface: null!);
        ship.WorldPosition = new Vector3 { x = 0f, y = 0f, z = 0f };
        ship.ObjectOffsets = new Vector3 { x = 0f, y = -150f, z = 400f };

        var ctrl = new MotherShipLargeControls();

        var t = typeof(MotherShipLargeControls);
        t.GetField("_descentInitialized", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(ctrl, true);
        t.GetField("_isDescending", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(ctrl, false);
        t.GetField("_lastMovementTime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(ctrl, DateTime.Now.AddSeconds(-0.25));

        ctrl.MoveObject(ship, null, null);

        var leftStartPart = ship.ObjectParts.Find(p => p.PartName == "LeftWingEngineStart")!;
        var rightStartPart = ship.ObjectParts.Find(p => p.PartName == "RightWingEngineStart")!;

        var leftRot = ApplyShipRotation(new List<ITriangleMeshWithColor>(leftStartPart.Triangles), (Vector3)ship.Rotation);
        var rightRot = ApplyShipRotation(new List<ITriangleMeshWithColor>(rightStartPart.Triangles), (Vector3)ship.Rotation);

        var left = Centroid(leftRot[0]);
        var right = Centroid(rightRot[0]);

        Assert.IsTrue(left.x > 0f,
            $"Left engine start is not on left-side screen half after runtime pipeline. left.x={left.x:F3}");
        Assert.IsTrue(right.x < 0f,
            $"Right engine start is not on right-side screen half after runtime pipeline. right.x={right.x:F3}");
    }

    [TestMethod]
    public void RuntimePipeline_particles_should_spawn_from_current_frame_engine_start_not_previous_frame()
    {
        GameState.SurfaceState.GlobalMapPosition = new Vector3 { x = 0f, y = 0f, z = 0f };
        GameState.SurfaceState.AiObjects.Clear();

        var ship = MotherShipLarge.CreateMotherShipLarge(parentSurface: null!);
        ship.WorldPosition = new Vector3 { x = 0f, y = 0f, z = 0f };
        ship.ObjectOffsets = new Vector3 { x = 0f, y = -150f, z = 400f };
        var particles = new CapturingParticles();
        ship.Particles = particles;

        var ctrl = new MotherShipLargeControls();
        var t = typeof(MotherShipLargeControls);
        t.GetField("_descentInitialized", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(ctrl, true);
        t.GetField("_isDescending", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(ctrl, false);

        // Frame A: animate once, then cache stale guides as previous-frame loop output.
        t.GetField("_lastMovementTime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(ctrl, DateTime.Now.AddSeconds(-0.25));
        ctrl.MoveObject(ship, null, null);

        var leftStartA = ship.ObjectParts.Find(p => p.PartName == "LeftWingEngineStart")!.Triangles[0];
        var leftGuideA = ship.ObjectParts.Find(p => p.PartName == "LeftWingEngineGuide")!.Triangles[0];
        var rotA = (Vector3)ship.Rotation;
        var leftStartARot = ApplyShipRotation(new List<ITriangleMeshWithColor> { leftStartA }, rotA)[0];
        var leftGuideARot = ApplyShipRotation(new List<ITriangleMeshWithColor> { leftGuideA }, rotA)[0];
        ctrl.SetParticleGuideCoordinates(leftStartARot, null!);
        ctrl.SetParticleGuideCoordinates(null!, leftGuideARot);

        var rightStartA = ship.ObjectParts.Find(p => p.PartName == "RightWingEngineStart")!.Triangles[0];
        var rightGuideA = ship.ObjectParts.Find(p => p.PartName == "RightWingEngineGuide")!.Triangles[0];
        var rightStartARot = ApplyShipRotation(new List<ITriangleMeshWithColor> { rightStartA }, rotA)[0];
        var rightGuideARot = ApplyShipRotation(new List<ITriangleMeshWithColor> { rightGuideA }, rotA)[0];
        ctrl.SetRearEngineGuideCoordinates(rightStartARot, null!);
        ctrl.SetRearEngineGuideCoordinates(null!, rightGuideARot);

        // Frame B: emission should use current frame B guides, not stale A guides.
        particles.Calls.Clear();
        t.GetField("_lastMovementTime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(ctrl, DateTime.Now.AddSeconds(-0.25));
        ctrl.MoveObject(ship, null, null);

        Assert.IsTrue(particles.Calls.Count >= 2, "Expected left and right particle emission calls on frame B.");

        var actualStart = particles.Calls[0].start;
        var actualSpawn = Centroid(actualStart);

        var leftStartB = ship.ObjectParts.Find(p => p.PartName == "LeftWingEngineStart")!.Triangles[0];
        var rotB = (Vector3)ship.Rotation;
        var leftStartBRot = ApplyShipRotation(new List<ITriangleMeshWithColor> { leftStartB }, rotB)[0];
        var expectedSpawn = Centroid(leftStartBRot);

        Assert.AreEqual(expectedSpawn.x, actualSpawn.x, 0.01f,
            $"Particle spawn.x should match CURRENT frame engine start. expected={expectedSpawn.x:F3}, actual={actualSpawn.x:F3}");
        Assert.AreEqual(expectedSpawn.y, actualSpawn.y, 0.01f,
            $"Particle spawn.y should match CURRENT frame engine start. expected={expectedSpawn.y:F3}, actual={actualSpawn.y:F3}");
        Assert.AreEqual(expectedSpawn.z, actualSpawn.z, 0.01f,
            $"Particle spawn.z should match CURRENT frame engine start. expected={expectedSpawn.z:F3}, actual={actualSpawn.z:F3}");
    }
}
