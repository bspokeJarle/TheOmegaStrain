using TheOmegaStrain.Game.World.Objects;
using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Controls.MotherShipMediumControls;
using static TheOmegaStrain.Domain._3dSpecificsImplementations;

namespace TheOmegaStrain.Tests.Controls;

// Tests that pin down the wing-engine particle direction for MotherShipMedium.
//
// Particle velocity formula (from Particles.cs):
//   velocity = (startPos - guidePos) / life
//   position -= velocity  (each frame)
//
// So particles travel in the direction:  guidePos - startPos
//
// For nozzle emission the guide must be straight out from the nozzle in local -Z:
//   start.z = -11, guide.z = -111  => particles travel in -Z.
//
// In-game frame sequence (LiveGameLoop):
//   1. MoveObject  → AnimateEngines calls ApplyPivotedRotation (Y-axis tilt) on start/guide parts
//                  → ReleaseWingParticles reads _leftEngineStart/_leftEngineGuide (set previous frame)
//   2. RotateMesh  → applies global ship rotation (x=WorldViewSetup.CameraPitchDegrees, z=90) to all parts
//   3. SetMovementGuides → stores the globally-rotated triangles into _leftEngineStart/_leftEngineGuide for next frame
//
// So the guide coords sent to ReleaseParticles have been through BOTH rotations.
// The tests below replicate this exact pipeline to catch direction bugs without needing to run the game.
[TestClass]
public class MotherShipMediumWingEngineParticleTests
{
    private const float Life = 60f; // arbitrary non-zero life value
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

    // Simulates one frame of particle movement for one axis and returns the new position.
    private static float SimulateAxisPosition(float start, float guide, float startingPosition = 0f)
    {
        float velocity = (start - guide) / Life;
        return startingPosition - velocity;
    }

    // Replicates ApplyPivotedRotation: translate to origin, RotateY by angle, translate back.
    private static List<ITriangleMeshWithColor> ApplyPivotedRotation(
        List<ITriangleMeshWithColor> tris, Vector3 pivot, float angle)
    {
        var atOrigin = Translate(tris, new Vector3 { x = -pivot.x, y = -pivot.y, z = -pivot.z });
        var rotated  = Rotate.RotateYMesh(atOrigin, angle);
        return Translate(rotated, pivot);
    }

    // Replicates LiveGameLoop.RotateMesh for a single axis sequence (z then x — ship default x=WorldViewSetup.CameraPitchDegrees, y=0, z=90).
    private static List<ITriangleMeshWithColor> ApplyShipRotation(
        List<ITriangleMeshWithColor> tris, float rotX = WorldViewSetup.CameraPitchDegrees, float rotY = 0f, float rotZ = 90f)
    {
        var r = Rotate.RotateZMesh(tris, rotZ);
        r = Rotate.RotateYMesh(r, rotY);
        r = Rotate.RotateXMesh(r, rotX);
        return r;
    }

    private static List<ITriangleMeshWithColor> ApplyShipRotation(
        List<ITriangleMeshWithColor> tris, Vector3 rotation)
        => ApplyShipRotation(tris, rotation.x, rotation.y, rotation.z);

    private static List<ITriangleMeshWithColor> Translate(List<ITriangleMeshWithColor> tris, Vector3 offset)
    {
        var result = new List<ITriangleMeshWithColor>(tris.Count);
        foreach (var tri in tris)
            result.Add(new TriangleMeshWithColor
            {
                Color = tri.Color, noHidden = tri.noHidden,
                vert1 = new Vector3 { x = tri.vert1.x + offset.x, y = tri.vert1.y + offset.y, z = tri.vert1.z + offset.z },
                vert2 = new Vector3 { x = tri.vert2.x + offset.x, y = tri.vert2.y + offset.y, z = tri.vert2.z + offset.z },
                vert3 = new Vector3 { x = tri.vert3.x + offset.x, y = tri.vert3.y + offset.y, z = tri.vert3.z + offset.z },
            });
        return result;
    }

    private static Vector3 Centroid(ITriangleMeshWithColor tri) => new Vector3
    {
        x = (tri.vert1.x + tri.vert2.x + tri.vert3.x) / 3f,
        y = (tri.vert1.y + tri.vert2.y + tri.vert3.y) / 3f,
        z = (tri.vert1.z + tri.vert2.z + tri.vert3.z) / 3f,
    };

    private static Vector3 GetPartCenter(List<ITriangleMeshWithColor> tris)
    {
        float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
        foreach (var t in tris)
            foreach (var v in new[] { (Vector3)t.vert1, (Vector3)t.vert2, (Vector3)t.vert3 })
            {
                if (v.x < minX) minX = v.x; if (v.x > maxX) maxX = v.x;
                if (v.y < minY) minY = v.y; if (v.y > maxY) maxY = v.y;
                if (v.z < minZ) minZ = v.z; if (v.z > maxZ) maxZ = v.z;
            }
        return new Vector3 { x = (minX + maxX) * 0.5f, y = (minY + maxY) * 0.5f, z = (minZ + maxZ) * 0.5f };
    }

    // -----------------------------------------------------------------------
    //  Raw geometry (unscaled) — WingEngineStart / WingEngineGuide static methods
    // -----------------------------------------------------------------------

    [TestMethod]
    public void RightEngine_start_y_is_positive_and_guide_stays_on_same_engine_side()
    {
        var start = MotherShipMedium.WingEngineStart(isRight: true)![0];
        var guide = MotherShipMedium.WingEngineGuide(isRight: true)![0];

        Assert.IsTrue(start.vert1.y > 0,
            $"Right engine start.y should be positive (outward); got {start.vert1.y}");
        Assert.AreEqual(start.vert1.y, guide.vert1.y, 0.01f,
            $"Right engine guide.y ({guide.vert1.y}) should stay aligned with start.y ({start.vert1.y})");
    }

    [TestMethod]
    public void LeftEngine_start_y_is_negative_and_guide_stays_on_same_engine_side()
    {
        var start = MotherShipMedium.WingEngineStart(isRight: false)![0];
        var guide = MotherShipMedium.WingEngineGuide(isRight: false)![0];

        Assert.IsTrue(start.vert1.y < 0,
            $"Left engine start.y should be negative (outward); got {start.vert1.y}");
        Assert.AreEqual(start.vert1.y, guide.vert1.y, 0.01f,
            $"Left engine guide.y ({guide.vert1.y}) should stay aligned with start.y ({start.vert1.y})");
    }

    [TestMethod]
    public void RightEngine_particle_travels_outward_in_negative_Z()
    {
        var start = MotherShipMedium.WingEngineStart(isRight: true)![0];
        var guide = MotherShipMedium.WingEngineGuide(isRight: true)![0];

        float newZ = SimulateAxisPosition(start.vert1.z, guide.vert1.z, startingPosition: start.vert1.z);

        Assert.IsTrue(newZ < start.vert1.z,
            $"Right engine particle should move in -Z from {start.vert1.z}, but ended up at {newZ}");
    }

    [TestMethod]
    public void LeftEngine_particle_travels_outward_in_negative_Z()
    {
        var start = MotherShipMedium.WingEngineStart(isRight: false)![0];
        var guide = MotherShipMedium.WingEngineGuide(isRight: false)![0];

        float newZ = SimulateAxisPosition(start.vert1.z, guide.vert1.z, startingPosition: start.vert1.z);

        Assert.IsTrue(newZ < start.vert1.z,
            $"Left engine particle should move in -Z from {start.vert1.z}, but ended up at {newZ}");
    }

    // -----------------------------------------------------------------------
    //  Guide offset — the guide must be displaced from the start by exactly
    //  WingEngineGuideOffsetZ in the outward -Z direction (both sides).
    // -----------------------------------------------------------------------

    [TestMethod]
    public void RightEngine_guide_is_offset_from_start_by_WingEngineGuideOffsetZ()
    {
        const float expectedOffset = 100f;

        var start = MotherShipMedium.WingEngineStart(isRight: true)![0];
        var guide = MotherShipMedium.WingEngineGuide(isRight: true)![0];

        float actualOffset = start.vert1.z - guide.vert1.z;

        Assert.AreEqual(expectedOffset, actualOffset, 0.01f,
            $"Right engine guide should be {expectedOffset} further out in -Z than start; actual delta = {actualOffset}");
    }

    [TestMethod]
    public void LeftEngine_guide_is_offset_from_start_by_WingEngineGuideOffsetZ()
    {
        const float expectedOffset = 100f;

        var start = MotherShipMedium.WingEngineStart(isRight: false)![0];
        var guide = MotherShipMedium.WingEngineGuide(isRight: false)![0];

        float actualOffset = start.vert1.z - guide.vert1.z;

        Assert.AreEqual(expectedOffset, actualOffset, 0.01f,
            $"Left engine guide should be {expectedOffset} further out in -Z than start; actual delta = {actualOffset}");
    }

    // -----------------------------------------------------------------------
    //  Scaled geometry — as built by CreateMotherShipMedium (ZoomRatio=1.38)
    //  Direction must still be outward after scaling.
    // -----------------------------------------------------------------------

    [TestMethod]
    public void AfterScaling_RightEngine_particle_still_travels_outward()
    {
        var ship = MotherShipMedium.CreateMotherShipMedium(parentSurface: null!);

        var startPart = ship.ObjectParts.Find(p => p.PartName == "RightWingEngineStart");
        var guidePart = ship.ObjectParts.Find(p => p.PartName == "RightWingEngineGuide");

        Assert.IsNotNull(startPart, "RightWingEngineStart part missing from ship");
        Assert.IsNotNull(guidePart, "RightWingEngineGuide part missing from ship");

        float startZ = startPart.Triangles[0].vert1.z;
        float guideZ = guidePart.Triangles[0].vert1.z;

        float newZ = SimulateAxisPosition(startZ, guideZ, startingPosition: startZ);

        Assert.IsTrue(newZ < startZ,
            $"After scaling: right engine particle should move in -Z from {startZ}, but ended up at {newZ}. guideZ={guideZ}");
    }

    [TestMethod]
    public void AfterScaling_LeftEngine_particle_still_travels_outward()
    {
        var ship = MotherShipMedium.CreateMotherShipMedium(parentSurface: null!);

        var startPart = ship.ObjectParts.Find(p => p.PartName == "LeftWingEngineStart");
        var guidePart = ship.ObjectParts.Find(p => p.PartName == "LeftWingEngineGuide");

        Assert.IsNotNull(startPart, "LeftWingEngineStart part missing from ship");
        Assert.IsNotNull(guidePart, "LeftWingEngineGuide part missing from ship");

        float startZ = startPart.Triangles[0].vert1.z;
        float guideZ = guidePart.Triangles[0].vert1.z;

        float newZ = SimulateAxisPosition(startZ, guideZ, startingPosition: startZ);

        Assert.IsTrue(newZ < startZ,
            $"After scaling: left engine particle should move in -Z from {startZ}, but ended up at {newZ}. guideZ={guideZ}");
    }

    // -----------------------------------------------------------------------
    //  Symmetry — both engines should be mirror images (equal |y| values).
    // -----------------------------------------------------------------------

    [TestMethod]
    public void BothEngines_are_symmetric_around_centre()
    {
        var rightStart = MotherShipMedium.WingEngineStart(isRight: true)![0];
        var leftStart  = MotherShipMedium.WingEngineStart(isRight: false)![0];
        var rightGuide = MotherShipMedium.WingEngineGuide(isRight: true)![0];
        var leftGuide  = MotherShipMedium.WingEngineGuide(isRight: false)![0];

        Assert.AreEqual(rightStart.vert1.y, -leftStart.vert1.y, 0.01f,
            $"Start positions should be symmetric: right={rightStart.vert1.y}, left={leftStart.vert1.y}");
        Assert.AreEqual(rightGuide.vert1.y, -leftGuide.vert1.y, 0.01f,
            $"Guide positions should be symmetric: right={rightGuide.vert1.y}, left={leftGuide.vert1.y}");
    }

    // -----------------------------------------------------------------------
    //  FULL IN-GAME PIPELINE simulation
    //
    //  Replicates exactly what LiveGameLoop does each frame:
    //    Step 1: AnimateEngines → ApplyPivotedRotation (Y-axis tilt around pod pivot)
    //    Step 2: LiveGameLoop.RotateMesh → global ship rotation (Z=90 then X=70)
    //    Step 3: SetMovementGuides → centroids of resulting triangles become start/guide
    //    Step 4: ReleaseParticles → velocity = (startPos - guidePos) / life
    //
    //  We test a range of tilt angles to ensure direction is outward regardless of
    //  where the engine animation is in its cycle.
    // -----------------------------------------------------------------------

    private void AssertOutwardAfterFullPipeline(bool isRight, float tiltAngle, string label)
    {
        var ship = MotherShipMedium.CreateMotherShipMedium(parentSurface: null!);

        var startPartName = isRight ? "RightWingEngineStart" : "LeftWingEngineStart";
        var guidPartName  = isRight ? "RightWingEngineGuide" : "LeftWingEngineGuide";
        var podPartName   = isRight ? "RightPod"             : "LeftPod";

        var podTris   = ship.ObjectParts.Find(p => p.PartName == podPartName)!.Triangles;
        var pivot     = GetPartCenter(podTris);

        var startTris = new List<ITriangleMeshWithColor>(ship.ObjectParts.Find(p => p.PartName == startPartName)!.Triangles);
        var guideTris = new List<ITriangleMeshWithColor>(ship.ObjectParts.Find(p => p.PartName == guidPartName)!.Triangles);

        // Step 1: ApplyPivotedRotation
        startTris = ApplyPivotedRotation(startTris, pivot, tiltAngle);
        guideTris = ApplyPivotedRotation(guideTris, pivot, tiltAngle);

        // Step 2: global ship rotation (x=WorldViewSetup.CameraPitchDegrees, y=0, z=90)
        startTris = ApplyShipRotation(startTris);
        guideTris = ApplyShipRotation(guideTris);

        // Step 3: centroids  (what SetMovementGuides feeds into SetParticleGuideCoordinates)
        var startPos  = Centroid(startTris[0]);
        var guidePos  = Centroid(guideTris[0]);

        // Step 4: verify guide direction is outward relative to transformed local +Z axis.
        var delta = new Vector3 { x = guidePos.x - startPos.x, y = guidePos.y - startPos.y, z = guidePos.z - startPos.z };
        var forward = RotateVectorThroughPipeline(new Vector3 { x = 0f, y = 0f, z = 1f }, tiltAngle);
        float dot = delta.x * forward.x + delta.y * forward.y + delta.z * forward.z;

        Assert.IsTrue(dot < 0f,
            $"[{label}] {(isRight ? "Right" : "Left")} engine after full pipeline should emit out along -Z (dot<0). dot={dot:F3}");
    }

    private static Vector3 RotateVectorThroughPipeline(Vector3 vector, float tiltAngle)
    {
        var tri = new List<ITriangleMeshWithColor>
        {
            new TriangleMeshWithColor
            {
                vert1 = new Vector3 { x = 0f, y = 0f, z = 0f },
                vert2 = new Vector3 { x = vector.x, y = vector.y, z = vector.z },
                vert3 = new Vector3 { x = 0f, y = 0f, z = 0f },
            }
        };

        tri = Rotate.RotateYMesh(tri, tiltAngle);
        tri = ApplyShipRotation(tri);

        var v = tri[0].vert2;
        return new Vector3 { x = v.x, y = v.y, z = v.z };
    }

    [TestMethod]
    public void FullPipeline_RightEngine_particle_travels_outward_at_tilt_0()
        => AssertOutwardAfterFullPipeline(isRight: true,  tiltAngle: 0f,   "tilt=0");

    [TestMethod]
    public void FullPipeline_LeftEngine_particle_travels_outward_at_tilt_0()
        => AssertOutwardAfterFullPipeline(isRight: false, tiltAngle: 0f,   "tilt=0");

    [TestMethod]
    public void FullPipeline_RightEngine_particle_travels_outward_at_tilt_35()
        => AssertOutwardAfterFullPipeline(isRight: true,  tiltAngle: 35f,  "tilt=35");

    [TestMethod]
    public void FullPipeline_LeftEngine_particle_travels_outward_at_tilt_35()
        => AssertOutwardAfterFullPipeline(isRight: false, tiltAngle: 35f,  "tilt=35");

    [TestMethod]
    public void FullPipeline_RightEngine_particle_travels_outward_at_tilt_minus35()
        => AssertOutwardAfterFullPipeline(isRight: true,  tiltAngle: -35f, "tilt=-35");

    [TestMethod]
    public void FullPipeline_LeftEngine_particle_travels_outward_at_tilt_minus35()
        => AssertOutwardAfterFullPipeline(isRight: false, tiltAngle: -35f, "tilt=-35");

    [TestMethod]
    public void FullPipeline_RightEngine_particle_travels_outward_at_tilt_90()
        => AssertOutwardAfterFullPipeline(isRight: true,  tiltAngle: 90f,  "tilt=90");

    [TestMethod]
    public void FullPipeline_LeftEngine_particle_travels_outward_at_tilt_90()
        => AssertOutwardAfterFullPipeline(isRight: false, tiltAngle: 90f,  "tilt=90");

    [TestMethod]
    public void RuntimePipeline_wing_engine_starts_must_stay_on_opposite_sides_of_ship()
    {
        // This test executes MotherShipMediumControls.MoveObject (real runtime path),
        // then applies the same global rotation pass as LiveGameLoop and verifies
        // that left/right wing engine start guides are still on opposite lateral sides.
        // If both end up on the same side, particles will look shifted to one side in-game.
        GameState.SurfaceState.GlobalMapPosition = new Vector3 { x = 0f, y = 0f, z = 0f };
        GameState.SurfaceState.AiObjects.Clear();

        var ship = MotherShipMedium.CreateMotherShipMedium(parentSurface: null!);
        ship.WorldPosition = new Vector3 { x = 0f, y = 0f, z = 0f };
        ship.ObjectOffsets = new Vector3 { x = 0f, y = -150f, z = 400f };

        var ctrl = new MotherShipMediumControls();

        // Force post-descent branch so engine tilt animation runs immediately.
        var t = typeof(MotherShipMediumControls);
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

        // Facing camera baseline (Z=90): left engine should end up on +X side, right on -X side.
        Assert.IsTrue(left.x > 0f,
            $"Left engine start is not on left-side screen half after runtime pipeline. left.x={left.x:F3}");
        Assert.IsTrue(right.x < 0f,
            $"Right engine start is not on right-side screen half after runtime pipeline. right.x={right.x:F3}");
    }

    [TestMethod]
    public void RuntimePipeline_medium_mothership_syncs_thirty_units_higher_above_surface()
    {
        GameState.GamePlayState = new GamePlayState();
        GameState.SurfaceState = new SurfaceState
        {
            GlobalMapPosition = new Vector3 { x = 0f, y = 40f, z = 0f },
            AiObjects = new List<_3dObject>()
        };

        var ship = MotherShipMedium.CreateMotherShipMedium(parentSurface: null!);
        ship.WorldPosition = new Vector3 { x = 0f, y = 0f, z = 0f };
        ship.ObjectOffsets = new Vector3 { x = 12f, y = -1500f, z = 400f };
        ship.Rotation = new Vector3();
        ship.ImpactStatus = new ImpactStatus { ObjectHealth = EnemySetup.MotherShipMediumHealth };

        var ctrl = new MotherShipMediumControls();
        var t = typeof(MotherShipMediumControls);
        t.GetField("_descentInitialized", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(ctrl, true);
        t.GetField("_isDescending", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(ctrl, false);

        ctrl.MoveObject(ship, null, null);

        float expectedY = GameState.SurfaceState.GlobalMapPosition.y *
            SurfacePositionSyncHelpers.DefaultEnemySurfaceSyncFactorY - 105f;
        Assert.AreEqual(expectedY, ship.ObjectOffsets!.y, 0.001f);
    }

    [TestMethod]
    public void RuntimePipeline_particles_should_spawn_from_current_frame_engine_start_not_previous_frame()
    {
        // Runtime contract for animated guides:
        // particle emission must use CURRENT frame start/guide geometry, not stale cached guides.
        GameState.SurfaceState.GlobalMapPosition = new Vector3 { x = 0f, y = 0f, z = 0f };
        GameState.SurfaceState.AiObjects.Clear();

        var ship = MotherShipMedium.CreateMotherShipMedium(parentSurface: null!);
        ship.WorldPosition = new Vector3 { x = 0f, y = 0f, z = 0f };
        ship.ObjectOffsets = new Vector3 { x = 0f, y = -150f, z = 400f };
        var particles = new CapturingParticles();
        ship.Particles = particles;

        var ctrl = new MotherShipMediumControls();
        var t = typeof(MotherShipMediumControls);
        t.GetField("_descentInitialized", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(ctrl, true);
        t.GetField("_isDescending", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(ctrl, false);

        // Frame A: animate once, then intentionally cache stale guides as if from previous frame.
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

        // Also wire right side so MoveObject does normal dual-engine release.
        var rightStartA = ship.ObjectParts.Find(p => p.PartName == "RightWingEngineStart")!.Triangles[0];
        var rightGuideA = ship.ObjectParts.Find(p => p.PartName == "RightWingEngineGuide")!.Triangles[0];
        var rightStartARot = ApplyShipRotation(new List<ITriangleMeshWithColor> { rightStartA }, rotA)[0];
        var rightGuideARot = ApplyShipRotation(new List<ITriangleMeshWithColor> { rightGuideA }, rotA)[0];
        ctrl.SetRearEngineGuideCoordinates(rightStartARot, null!);
        ctrl.SetRearEngineGuideCoordinates(null!, rightGuideARot);

        // Frame B: should emit from CURRENT frame B guides even though stale A guides are cached.
        particles.Calls.Clear();
        t.GetField("_lastMovementTime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(ctrl, DateTime.Now.AddSeconds(-0.25));
        ctrl.MoveObject(ship, null, null);

        Assert.IsTrue(particles.Calls.Count >= 2, "Expected left+right particle emission calls on frame B.");
        var actualStart = particles.Calls[0].start; // first call = left stream in MoveObject
        var actualSpawn = Centroid(actualStart);

        // Expected if using CURRENT frame B start guide.
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

    // -----------------------------------------------------------------------
    //  Tilt animation actually moves the guides
    //
    //  ApplyPivotedRotation rotates around the pod's Y-axis pivot, which changes
    //  the X and Z position of the start and guide triangles.
    //  This confirms the animation is not a no-op and the guides move with it.
    // -----------------------------------------------------------------------

    [TestMethod]
    public void AnimationTilt_moves_start_position_in_XZ_plane()
    {
        var ship0  = MotherShipMedium.CreateMotherShipMedium(parentSurface: null!);
        var ship35 = MotherShipMedium.CreateMotherShipMedium(parentSurface: null!);

        foreach (var (ship, tilt) in new[] { (ship0, 0f), (ship35, 35f) })
        {
            var podTris   = ship.ObjectParts.Find(p => p.PartName == "RightPod")!.Triangles;
            var pivot     = GetPartCenter(podTris);
            var startTris = new List<ITriangleMeshWithColor>(ship.ObjectParts.Find(p => p.PartName == "RightWingEngineStart")!.Triangles);
            var rotated   = ApplyPivotedRotation(startTris, pivot, tilt);
            ship.ObjectParts.Find(p => p.PartName == "RightWingEngineStart")!.Triangles = rotated;
        }

        var pos0  = Centroid(ship0.ObjectParts.Find(p => p.PartName == "RightWingEngineStart")!.Triangles[0]);
        var pos35 = Centroid(ship35.ObjectParts.Find(p => p.PartName == "RightWingEngineStart")!.Triangles[0]);

        Assert.AreNotEqual(pos0.x, pos35.x, 0.1f,
            $"Tilt animation should move start X: tilt=0 gives x={pos0.x:F2}, tilt=35 gives x={pos35.x:F2}");
    }

    // -----------------------------------------------------------------------
    //  Start and guide move as a rigid unit under tilt rotation
    //
    //  The outward offset from start to guide must stay the same regardless of
    //  tilt angle — because both are rotated by the same pivot/angle.
    //  If the distance changes significantly, the emission direction is unstable.
    // -----------------------------------------------------------------------

    private static float Distance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x, dy = a.y - b.y, dz = a.z - b.z;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private float GuideStartDistance(bool isRight, float tiltAngle)
    {
        var ship    = MotherShipMedium.CreateMotherShipMedium(parentSurface: null!);
        var podName = isRight ? "RightPod" : "LeftPod";
        var startName = isRight ? "RightWingEngineStart" : "LeftWingEngineStart";
        var guideName = isRight ? "RightWingEngineGuide" : "LeftWingEngineGuide";

        var pivot     = GetPartCenter(ship.ObjectParts.Find(p => p.PartName == podName)!.Triangles);
        var startTris = ApplyPivotedRotation(new List<ITriangleMeshWithColor>(ship.ObjectParts.Find(p => p.PartName == startName)!.Triangles), pivot, tiltAngle);
        var guideTris = ApplyPivotedRotation(new List<ITriangleMeshWithColor>(ship.ObjectParts.Find(p => p.PartName == guideName)!.Triangles), pivot, tiltAngle);

        return Distance(Centroid(startTris[0]), Centroid(guideTris[0]));
    }

    [TestMethod]
    public void RigidUnit_RightEngine_guide_to_start_distance_is_constant_across_tilt_angles()
    {
        float d0   = GuideStartDistance(isRight: true, 0f);
        float d35  = GuideStartDistance(isRight: true, 35f);
        float d90  = GuideStartDistance(isRight: true, 90f);
        float dm35 = GuideStartDistance(isRight: true, -35f);

        Assert.AreEqual(d0, d35,  1.0f, $"Right engine: distance at tilt=0 ({d0:F2}) vs tilt=35 ({d35:F2}) should be constant");
        Assert.AreEqual(d0, d90,  1.0f, $"Right engine: distance at tilt=0 ({d0:F2}) vs tilt=90 ({d90:F2}) should be constant");
        Assert.AreEqual(d0, dm35, 1.0f, $"Right engine: distance at tilt=0 ({d0:F2}) vs tilt=-35 ({dm35:F2}) should be constant");
    }

    [TestMethod]
    public void RigidUnit_LeftEngine_guide_to_start_distance_is_constant_across_tilt_angles()
    {
        float d0   = GuideStartDistance(isRight: false, 0f);
        float d35  = GuideStartDistance(isRight: false, 35f);
        float d90  = GuideStartDistance(isRight: false, 90f);
        float dm35 = GuideStartDistance(isRight: false, -35f);

        Assert.AreEqual(d0, d35,  1.0f, $"Left engine: distance at tilt=0 ({d0:F2}) vs tilt=35 ({d35:F2}) should be constant");
        Assert.AreEqual(d0, d90,  1.0f, $"Left engine: distance at tilt=0 ({d0:F2}) vs tilt=90 ({d90:F2}) should be constant");
        Assert.AreEqual(d0, dm35, 1.0f, $"Left engine: distance at tilt=0 ({d0:F2}) vs tilt=-35 ({dm35:F2}) should be constant");
    }
}
