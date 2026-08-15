using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;
using CommonUtilities.CommonSetup;
using Domain;
using GameAiAndControls.Controls.JumpingFishControls;
using _3dRotations.World.Objects;
using static Domain._3dSpecificsImplementations;

namespace _3DSpesificsUnitTests.Controls;

[TestClass]
public class JumpingFishControlsTests
{
    private const float InitialX = 0f;
    private const float InitialY = -220f;
    private const float InitialZ = 460f;

    [TestInitialize]
    public void Setup()
    {
        GameState.GamePlayState = new GamePlayState();
        GameState.SurfaceState = new SurfaceState();
        GameState.DeltaTime = 0f;
    }

    [TestCleanup]
    public void Cleanup()
    {
        GameState.DeltaTime = 0f;
    }

    [TestMethod]
    public void MoveObject_StartsVerticalAndMovesThroughLeftJumpArc()
    {
        var fish = CreateFish(isOnScreen: true);
        var controls = new JumpingFishControls();

        MoveOneFrame(controls, fish);

        Assert.AreEqual(InitialX + 130f, fish.ObjectOffsets!.x, 0.001f);
        Assert.AreEqual(InitialY, fish.ObjectOffsets.y, 0.001f);
        Assert.AreEqual(InitialZ, fish.ObjectOffsets.z, 0.001f);
        Assert.AreEqual(WorldViewSetup.SurfaceFacingObjectPitchDegrees, fish.Rotation!.x, 0.001f);
        Assert.AreEqual(0f, fish.Rotation.y, 0.001f);
        Assert.AreEqual(-90f, fish.Rotation.z, 0.001f);

        AdvanceFrames(controls, fish, 10);

        Assert.AreEqual(InitialX, fish.ObjectOffsets.x, 1f);
        Assert.IsTrue(fish.ObjectOffsets.y < InitialY - 150f, "The fish should reach the top of the jump near mid-cycle.");
        Assert.IsTrue(fish.ObjectOffsets.z > InitialZ, "The jump should have a small depth pulse at the apex.");
        Assert.AreEqual(WorldViewSetup.SurfaceFacingObjectPitchDegrees + 8f, fish.Rotation.x, 0.5f);
        Assert.AreEqual(24f, fish.Rotation.y, 0.5f);
        Assert.AreEqual(-180f, fish.Rotation.z, 1f);

        AdvanceFrames(controls, fish, 9);

        Assert.IsTrue(fish.ObjectOffsets.x < InitialX - 120f, "The fish should finish the jump on the left side.");
        Assert.IsTrue(fish.ObjectOffsets.y > InitialY - 50f, "The fish should dive back down near the end of the jump.");
        Assert.IsTrue(fish.Rotation.z < -260f, "The fish should be nose-down on the far side of the jump.");
    }

    [TestMethod]
    public void MoveObject_DoesNothingUntilFishIsOnScreen()
    {
        var fish = CreateFish(isOnScreen: false);
        var controls = new JumpingFishControls();
        var rightFin = fish.ObjectParts.Find(p => p.PartName == "RightPectoralFin")!;
        float originalFinZ = rightFin.Triangles[0].vert1.z;

        MoveOneFrame(controls, fish);
        AdvanceFrames(controls, fish, 4);

        Assert.AreEqual(originalFinZ, rightFin.Triangles[0].vert1.z, 0.001f);
        Assert.AreEqual(InitialX, fish.ObjectOffsets!.x, 0.001f);
        Assert.AreEqual(InitialY, fish.ObjectOffsets.y, 0.001f);
        Assert.AreEqual(InitialZ, fish.ObjectOffsets.z, 0.001f);
        Assert.AreEqual(0, GetVisibleTailFrame(fish, "TailBase_Frame"));

        fish.IsOnScreen = true;
        MoveOneFrame(controls, fish);
        MoveOneFrame(controls, fish);

        Assert.AreEqual(1, CountVisibleTailParts(fish, "TailBase_Frame"));
        Assert.AreEqual(1, CountVisibleTailParts(fish, "TailTip_Frame"));
        Assert.AreNotEqual(0, GetVisibleTailFrame(fish, "TailBase_Frame"));
        Assert.AreNotEqual(originalFinZ, rightFin.Triangles[0].vert1.z, "Pectoral fins should flap only when the fish is on screen.");
    }

    [TestMethod]
    public void MoveObject_WithPathBounds_ContinuesForwardUntilNearEdgeBeforeTurning()
    {
        var fish = CreateFish(isOnScreen: true);
        var controls = new JumpingFishControls(jumpHorizontalSpan: 100f, minPathOffsetX: -250f, maxPathOffsetX: 250f);

        MoveOneFrame(controls, fish);

        Assert.AreEqual(250f, fish.ObjectOffsets!.x, 0.001f, "The bounded fish should start at the right edge.");
        Assert.AreEqual(-90f, fish.Rotation!.z, 0.001f);

        AdvanceFrames(controls, fish, 21);

        Assert.IsTrue(fish.ObjectOffsets.x > 100f && fish.ObjectOffsets.x < 180f, "The second jump should continue left instead of immediately turning back.");
        Assert.IsTrue(fish.Rotation.z < -90f, "The fish should still be moving in the first direction while there is room.");

        AdvanceFrames(controls, fish, 80);
        float turnStartX = fish.ObjectOffsets.x;

        Assert.IsTrue(turnStartX < -220f, "The fish should only turn after it reaches the left edge.");
        Assert.IsTrue(fish.Rotation.z > -90f, "After the edge turn, the fish should start a mirrored jump back to the right.");

        AdvanceFrames(controls, fish, 10);

        Assert.IsTrue(fish.ObjectOffsets.x > turnStartX, "After turning at the edge, the fish should move right.");
    }

    [TestMethod]
    public void MoveObject_ReleasesBlueSplashParticlesAtTakeoffAndLanding()
    {
        var fish = CreateFish(isOnScreen: true);
        var controls = new JumpingFishControls();

        MoveOneFrame(controls, fish);

        Assert.IsNotNull(fish.Particles);
        Assert.IsTrue(fish.Particles!.Particles.Count > 0, "The fish should release splash particles when the jump starts.");
        Assert.IsTrue(HasBlueParticle(fish), "Splash particles should use blue water colors.");
        AssertSplashStartsNearWaterline(fish.Particles.Particles[0], "Takeoff splash should start at the waterline, not at explosion height.");
        int takeoffParticleCount = fish.Particles.Particles.Count;

        AdvanceFrames(controls, fish, 20);

        Assert.IsTrue(
            fish.Particles.Particles.Count > takeoffParticleCount,
            "The fish should release a second splash just before landing.");
        Assert.IsTrue(HasBlueParticle(fish), "Landing splash particles should stay blue while they age.");
        AssertSplashStartsNearWaterline(fish.Particles.Particles[takeoffParticleCount], "Landing splash should start where the fish breaks the water.");
    }

    [TestMethod]
    public void MoveObject_SendsUpwardVelocityBoostForSplashParticles()
    {
        var fish = CreateFish(isOnScreen: true);
        var particles = new CapturingParticles();
        fish.Particles = particles;
        var controls = new JumpingFishControls();

        MoveOneFrame(controls, fish);

        Assert.IsTrue(particles.ReleaseCount > 0, "The fish should release a splash when the jump starts.");
        Assert.IsTrue(particles.LastUpwardVelocityBoost > 0f, "Fish splash particles should ask for extra upward travel.");
        Assert.AreEqual(true, particles.LastExplosionFlag, "Fish splash should still use the explosion-style burst spread.");
    }

    [TestMethod]
    public void MoveObject_KeepsReleasedSplashParticlesAnchoredToWaterline()
    {
        var fish = CreateFish(isOnScreen: true);
        var particles = new CapturingParticles();
        fish.Particles = particles;
        var controls = new JumpingFishControls();

        MoveOneFrame(controls, fish);

        Assert.IsTrue(particles.Particles.Count > 0, "Expected takeoff splash particles.");
        var particle = particles.Particles[0];
        float releasedX = fish.ObjectOffsets!.x + particle.Position.x;
        float releasedY = fish.ObjectOffsets.y + particle.Position.y;
        float releasedZ = fish.ObjectOffsets.z + particle.Position.z;

        MoveOneFrame(controls, fish);

        Assert.AreEqual(releasedX, fish.ObjectOffsets!.x + particle.Position.x, 0.001f);
        Assert.AreEqual(releasedY, fish.ObjectOffsets.y + particle.Position.y, 0.001f);
        Assert.AreEqual(releasedZ, fish.ObjectOffsets.z + particle.Position.z, 0.001f);
    }

    [TestMethod]
    public void MoveObject_ReleasesLandingSplashUnderCurrentFishPosition()
    {
        var fish = CreateFish(isOnScreen: true);
        var particles = new CapturingParticles();
        fish.Particles = particles;
        var controls = new JumpingFishControls();

        MoveOneFrame(controls, fish);
        AdvanceFrames(controls, fish, 19);

        Assert.IsTrue(particles.Particles.Count >= 2, "Expected a landing splash particle.");
        var landingParticle = particles.Particles[1];
        float splashX = fish.ObjectOffsets!.x + landingParticle.Position.x;
        float splashY = fish.ObjectOffsets.y + landingParticle.Position.y;
        float splashZ = fish.ObjectOffsets.z + landingParticle.Position.z;

        Assert.AreEqual(fish.ObjectOffsets.x, splashX, 0.001f, "Landing splash should appear directly under the fish's current X position.");
        Assert.IsTrue(splashY < InitialY && splashY > InitialY - 30f, $"Landing splash should sit slightly above the waterline. Actual Y: {splashY:0.##}.");
        Assert.AreEqual(InitialZ, splashZ, 0.001f, "Landing splash should stay on the water surface depth.");
    }

    private static _3dObject CreateFish(bool isOnScreen)
    {
        var fish = JumpingFish.CreateJumpingFish(parentSurface: null!);
        fish.ObjectOffsets = new Vector3 { x = InitialX, y = InitialY, z = InitialZ };
        fish.WorldPosition = new Vector3();
        fish.Rotation = new Vector3();
        fish.IsOnScreen = isOnScreen;
        return fish;
    }

    private static void MoveOneFrame(JumpingFishControls controls, I3dObject fish)
    {
        GameState.DeltaTime = 0.1f;
        controls.MoveObject(fish, null, null);
    }

    private static void AdvanceFrames(JumpingFishControls controls, I3dObject fish, int frameCount)
    {
        for (int i = 0; i < frameCount; i++)
            MoveOneFrame(controls, fish);
    }

    private static int CountVisibleTailParts(I3dObject fish, string prefix)
    {
        int count = 0;
        foreach (var part in fish.ObjectParts)
        {
            if (part.PartName?.StartsWith(prefix, StringComparison.Ordinal) == true && part.IsVisible)
                count++;
        }

        return count;
    }

    private static int GetVisibleTailFrame(I3dObject fish, string prefix)
    {
        foreach (var part in fish.ObjectParts)
        {
            if (part.PartName?.StartsWith(prefix, StringComparison.Ordinal) != true || !part.IsVisible)
                continue;

            string frameText = part.PartName.Substring(prefix.Length);
            return int.Parse(frameText);
        }

        return -1;
    }

    private static bool HasBlueParticle(I3dObject fish)
    {
        if (fish.Particles == null)
            return false;

        foreach (var particle in fish.Particles.Particles)
        {
            string color = particle.ParticleTriangle.Color;
            if (color.Length < 6)
                continue;

            int red = Convert.ToInt32(color.Substring(0, 2), 16);
            int green = Convert.ToInt32(color.Substring(2, 2), 16);
            int blue = Convert.ToInt32(color.Substring(4, 2), 16);
            if (blue > red && blue >= green)
                return true;
        }

        return false;
    }

    private static void AssertSplashStartsNearWaterline(IParticle particle, string message)
    {
        Assert.IsNotNull(particle.Position);
        Assert.IsTrue(
            particle.Position!.y > -80f && particle.Position.y < 80f,
            $"{message} Local particle y was {particle.Position.y:0.##}.");
    }

    private sealed class CapturingParticles : IParticles
    {
        public IObjectMovement ParentShip { get; set; } = null!;
        public List<IParticle> Particles { get; set; } = new();
        public float LifeMultiplier { get; set; } = 1f;
        public int MaxParticlesOverride { get; set; }
        public int ReleaseCount { get; private set; }
        public bool? LastExplosionFlag { get; private set; }
        public float LastUpwardVelocityBoost { get; private set; }

        public void ReleaseParticles(
            ITriangleMeshWithColor Trajectory,
            ITriangleMeshWithColor StartPosition,
            IVector3 WorldPosition,
            IObjectMovement ParentShip,
            int Thrust,
            bool? explosion,
            float upwardVelocityBoost = 0f)
        {
            ReleaseCount++;
            LastExplosionFlag = explosion;
            LastUpwardVelocityBoost = upwardVelocityBoost;

            var startPosition = GetTriangleCenter(StartPosition);
            Particles.Add(new CapturedParticle
            {
                ParticleTriangle = StartPosition,
                Position = startPosition,
                WorldPosition = new Vector3
                {
                    x = WorldPosition.x,
                    y = WorldPosition.y,
                    z = WorldPosition.z
                }
            });
        }

        public void MoveParticles()
        {
        }

        private static Vector3 GetTriangleCenter(ITriangleMeshWithColor triangle)
        {
            return new Vector3
            {
                x = (triangle.vert1.x + triangle.vert2.x + triangle.vert3.x) / 3f,
                y = (triangle.vert1.y + triangle.vert2.y + triangle.vert3.y) / 3f,
                z = (triangle.vert1.z + triangle.vert2.z + triangle.vert3.z) / 3f
            };
        }
    }

    private sealed class CapturedParticle : IParticle
    {
        public ITriangleMeshWithColor ParticleTriangle { get; set; } = null!;
        public IVector3 Velocity { get; set; } = new Vector3();
        public IVector3 Acceleration { get; set; } = new Vector3();
        public long VariedStart { get; set; }
        public float Life { get; set; } = 1f;
        public float Size { get; set; } = 1f;
        public string Color { get; set; } = "d8f6ff";
        public DateTime BirthTime { get; set; } = DateTime.UtcNow;
        public bool IsRotated { get; set; }
        public IVector3 Position { get; set; } = new Vector3();
        public IVector3 WorldPosition { get; set; } = new Vector3();
        public IVector3? Rotation { get; set; }
        public IVector3? RotationSpeed { get; set; }
        public bool? NoShading { get; set; }
        public bool Visible { get; set; } = true;
        public IImpactStatus? ImpactStatus { get; set; }
        public IPhysics? Physics { get; set; }
    }
}
