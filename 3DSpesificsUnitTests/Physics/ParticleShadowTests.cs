using _3dRotations.World.Objects;
using _3dTesting.MainWindowClasses;
using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;
using CommonUtilities.CommonSetup;
using Domain;
using static Domain._3dSpecificsImplementations;

namespace _3DSpesificsUnitTests.Physics;

// The tests exist primarily to catch REGRESSIONS of the bug that caused
// particle shadows to vanish:
//   * If the altitude-to-projection factor becomes too large, the projected
//     (x, y) offset grows huge (hundreds of units) and the shadow ends up
//     far away from the particle's ground point — often off-screen.
//   * If MaxParticleAltitudeForProjection is not applied, very-high particles
//     never produce a readable shadow near themselves.
//   * If scale drops to zero the shadow becomes a degenerate zero-area
//     triangle and is invisible.
[TestClass]
public class ParticleShadowProjectionTests
{
    // Mirror of ParticleManager constants/knobs at the time of writing.
    private const float ParticleShadowSize = 6.0f;
    private const float BaseProjectedScale = 1.0f;
    private const float MinProjectedScale = 0.3f;
    private const float AltitudeShrinkFactor = 0.003f;
    private const float ParticleAltitudeProjection = 0.15f;
    private const float MaxParticleAltitudeForProjection = 120f;
    private const float ParticleShadowMinAltitude = 12f;
    private const float ShadowSlopeX = -0.15f;   // ObjectShadowManager.ShadowSlopeX
    private const float ShadowSlopeY = -0.55f;   // ObjectShadowManager.ShadowSlopeY

    private const float SurfaceTiltDegrees = WorldViewSetup.SurfacePitchDegrees;
    private static readonly float SurfaceTiltCos = MathF.Cos(SurfaceTiltDegrees * MathF.PI / 180f);
    private static readonly float SurfaceTiltSin = MathF.Sin(SurfaceTiltDegrees * MathF.PI / 180f);

    private readonly record struct ShadowVerts(Vector3 V1, Vector3 V2, Vector3 V3, float Scale);

    private static ShadowVerts ProjectShadow(float particleScreenY, float surfaceScreenY,
        float groundLocalX, float groundLocalY, float groundLocalZ)
    {
        var projected = GroundShadowProjectionMath.ProjectTriangleShadow(
            particleScreenY,
            surfaceScreenY + groundLocalY,
            groundLocalX,
            groundLocalY,
            groundLocalZ,
            new GroundShadowProjectionOptions
            {
                ShadowSize = ParticleShadowSize,
                BaseProjectedScale = BaseProjectedScale,
                MinProjectedScale = MinProjectedScale,
                AltitudeShrinkFactor = AltitudeShrinkFactor,
                AltitudeProjection = ParticleAltitudeProjection,
                MaxAltitudeForProjection = MaxParticleAltitudeForProjection,
                ShadowLift = 0f,
                ShadowSlopeX = ShadowSlopeX,
                ShadowSlopeY = ShadowSlopeY,
                SurfaceTiltDegrees = SurfaceTiltDegrees
            });

        return new ShadowVerts(
            ToVector3(projected.Vertex1),
            ToVector3(projected.Vertex2),
            ToVector3(projected.Vertex3),
            projected.Scale);
    }

    [TestInitialize]
    public void Setup()
    {
        GameState.GamePlayState = new GamePlayState();
        GameState.SurfaceState = new SurfaceState();
        GameState.ScreenOverlayState = new ScreenOverlayState();
        GameState.ObjectIdCounter = 0;
        ScreenSetup.Initialize(1500, 1024);
    }

    // ---------------------------------------------------------------
    // Visibility: the shadow must have non-zero area and stay near the
    // ground point the particle is above. If either of these fails, the
    // shadow effectively disappears on-screen.
    // ---------------------------------------------------------------

    [TestMethod]
    public void Shadow_NearSurface_HasNonZeroAreaAndSitsAtGroundPoint()
    {
        var s = ProjectShadow(particleScreenY: 500f, surfaceScreenY: 500f,
            groundLocalX: 0f, groundLocalY: 0f, groundLocalZ: 0f);

        // Altitude == 0 -> no projection offset.
        Assert.AreEqual(0f, (s.V1.x + s.V2.x) / 2f, 0.01f,
            "Centre X should equal ground point when particle is on the surface.");
        Assert.IsTrue(s.Scale >= MinProjectedScale, "Scale must be at least MinProjectedScale.");
        // Non-degenerate triangle (positive spread)
        Assert.IsTrue(MathF.Abs(s.V2.x - s.V1.x) > 1f,
            "Shadow silhouette must have a visible on-screen width.");
    }

    [TestMethod]
    public void Shadow_HighParticle_ProjectsButStaysCloseToGround()
    {
        // Particle 300px above surface on screen.
        var s = ProjectShadow(particleScreenY: 200f, surfaceScreenY: 500f,
            groundLocalX: 0f, groundLocalY: 0f, groundLocalZ: 0f);

        // With clamp 120 + slope -0.55 + factor 0.15 the X displacement is
        // 120 * -0.15 * 0.15 ≈ -2.7. Y displacement is 120 * -0.55 * 0.15 ≈ -9.9,
        // then * cos(70°) ≈ -3.4. Both MUST be tiny compared to screen extents.
        float centerX = (s.V1.x + s.V2.x) / 2f;
        Assert.IsTrue(MathF.Abs(centerX) < 10f,
            $"Projected centre X ({centerX:F2}) must stay within ~10 units of ground; " +
            "otherwise the shadow flies off-screen (regression of the bug where " +
            "the altitude factor was too large).");
        Assert.IsTrue(MathF.Abs(s.V1.y) < 10f && MathF.Abs(s.V2.y) < 10f,
            "Projected shadow must not wander far from the ground point on Y.");
    }

    [TestMethod]
    public void Shadow_VeryHighParticle_IsClampedByMaxAltitude()
    {
        // Particle 1000px above surface — without clamp, projX would be
        // 1000 * -0.15 * 0.15 = -22.5 on X and -82.5 on Y (before tilt).
        var sClamped = ProjectShadow(particleScreenY: -500f, surfaceScreenY: 500f,
            groundLocalX: 0f, groundLocalY: 0f, groundLocalZ: 0f);
        var sAtCap = ProjectShadow(particleScreenY: 500f - MaxParticleAltitudeForProjection,
            surfaceScreenY: 500f, groundLocalX: 0f, groundLocalY: 0f, groundLocalZ: 0f);

        float clampedCenterX = (sClamped.V1.x + sClamped.V2.x) / 2f;
        float capCenterX = (sAtCap.V1.x + sAtCap.V2.x) / 2f;
        Assert.AreEqual(capCenterX, clampedCenterX, 0.01f,
            "Altitudes above MaxParticleAltitudeForProjection must project identically " +
            "to the clamp altitude (otherwise high-flying particles' shadows fly off-screen).");
    }

    // ---------------------------------------------------------------
    // Directionality: shadow must cast in the same direction as the light.
    // ---------------------------------------------------------------

    [TestMethod]
    public void Shadow_CastsInLightDirection()
    {
        // Surface light in this project leans slightly LEFT (slopeX = -0.15)
        // and BEHIND the camera (slopeY = -0.55). A raised particle must
        // therefore cast its shadow to the left of (and further up-screen
        // than) the ground point.
        var sGround = ProjectShadow(particleScreenY: 500f, surfaceScreenY: 500f,
            groundLocalX: 0f, groundLocalY: 0f, groundLocalZ: 0f);
        var sHigh = ProjectShadow(particleScreenY: 400f, surfaceScreenY: 500f,
            groundLocalX: 0f, groundLocalY: 0f, groundLocalZ: 0f);

        float groundCenterX = (sGround.V1.x + sGround.V2.x) / 2f;
        float highCenterX = (sHigh.V1.x + sHigh.V2.x) / 2f;
        Assert.IsTrue(highCenterX < groundCenterX,
            $"Raised-particle shadow centre X ({highCenterX:F2}) should be LEFT of " +
            $"the on-surface shadow ({groundCenterX:F2}) because ShadowSlopeX < 0.");
    }

    // ---------------------------------------------------------------
    // Scale shrinks with altitude, but never below MinProjectedScale.
    // ---------------------------------------------------------------

    [TestMethod]
    public void Shadow_ScaleShrinksWithAltitudeButHasFloor()
    {
        var low = ProjectShadow(particleScreenY: 490f, surfaceScreenY: 500f,
            groundLocalX: 0, groundLocalY: 0, groundLocalZ: 0);
        var mid = ProjectShadow(particleScreenY: 300f, surfaceScreenY: 500f,
            groundLocalX: 0, groundLocalY: 0, groundLocalZ: 0);
        var veryHigh = ProjectShadow(particleScreenY: -5000f, surfaceScreenY: 500f,
            groundLocalX: 0, groundLocalY: 0, groundLocalZ: 0);

        Assert.IsTrue(mid.Scale < low.Scale,
            $"Shadow at altitude should be smaller ({mid.Scale:F2}) than one at the surface ({low.Scale:F2}).");
        Assert.IsTrue(veryHigh.Scale >= MinProjectedScale,
            $"Shadow scale ({veryHigh.Scale:F2}) must never fall below MinProjectedScale ({MinProjectedScale}).");
    }

    // ---------------------------------------------------------------
    // Tilt baking: the shadow vertex that points "forward" on the surface
    // must move into the ground plane (positive Z) and up-screen (negative
    // Y delta because +Y is down on screen).
    // ---------------------------------------------------------------

    [TestMethod]
    public void Shadow_TiltIsBakedIntoVertices()
    {
        var s = ProjectShadow(particleScreenY: 500f, surfaceScreenY: 500f,
            groundLocalX: 0f, groundLocalY: 0f, groundLocalZ: 0f);

        // v3 is the "front" vertex — offset by +s along the forward Y of the
        // silhouette, which after 70° tilt should yield:
        //   y = s * cos(70°), z = s * sin(70°)
        float expectedY = ParticleShadowSize * s.Scale * SurfaceTiltCos;
        float expectedZ = ParticleShadowSize * s.Scale * SurfaceTiltSin;
        Assert.AreEqual(expectedY, s.V3.y, 0.01f,
            "V3 y must equal size * cos(tilt) — surface tilt baked into the vertex.");
        Assert.AreEqual(expectedZ, s.V3.z, 0.01f,
            "V3 z must equal size * sin(tilt) — surface tilt baked into the vertex.");
    }

    // ---------------------------------------------------------------
    // Ground anchor: shadow follows the particle's X, not a global origin.
    // ---------------------------------------------------------------

    [TestMethod]
    public void Shadow_FollowsGroundAnchorX()
    {
        var sLeft = ProjectShadow(particleScreenY: 500f, surfaceScreenY: 500f,
            groundLocalX: -200f, groundLocalY: 0, groundLocalZ: 0);
        var sRight = ProjectShadow(particleScreenY: 500f, surfaceScreenY: 500f,
            groundLocalX: 200f, groundLocalY: 0, groundLocalZ: 0);

        float leftCenterX = (sLeft.V1.x + sLeft.V2.x) / 2f;
        float rightCenterX = (sRight.V1.x + sRight.V2.x) / 2f;
        Assert.IsTrue(rightCenterX - leftCenterX > 399f,
            "Shadow must move with the ground anchor (delta 400) — else it detaches from the particle.");
    }

    [TestMethod]
    public void Shadow_UsesGroundLocalYForAltitude()
    {
        var flatGround = ProjectShadow(particleScreenY: 480f, surfaceScreenY: 500f,
            groundLocalX: 0f, groundLocalY: 0f, groundLocalZ: 0f);
        var lowerGround = ProjectShadow(particleScreenY: 480f, surfaceScreenY: 500f,
            groundLocalX: 0f, groundLocalY: 40f, groundLocalZ: 0f);

        float flatCenterX = (flatGround.V1.x + flatGround.V2.x) / 2f;
        float lowerGroundCenterX = (lowerGround.V1.x + lowerGround.V2.x) / 2f;

        Assert.IsTrue(lowerGroundCenterX < flatCenterX,
            "Altitude should be measured against the actual tile ground Y, not only the surface object's base Y.");
    }

    [TestMethod]
    public void ShouldRenderParticleShadow_SuppressesJumpingFishWaterParticles()
    {
        bool shouldRender = ParticleManager.ShouldRenderParticleShadow(
            sourceObjectName: "JumpingFish",
            particleScreenY: 100f,
            groundScreenY: 500f);

        Assert.IsFalse(shouldRender, "Water splash particles should not cast ground shadows.");
    }

    [TestMethod]
    public void ShouldRenderParticleShadow_SuppressesParticlesAtGroundLevel()
    {
        bool shouldRender = ParticleManager.ShouldRenderParticleShadow(
            sourceObjectName: "ParticleEmitter",
            particleScreenY: 495f,
            groundScreenY: 500f);

        Assert.IsFalse(shouldRender,
            $"Particles within {ParticleShadowMinAltitude} screen units of ground should not cast visible shadows.");
    }

    [TestMethod]
    public void ShouldRenderParticleShadow_AllowsAirborneNonWaterParticles()
    {
        bool shouldRender = ParticleManager.ShouldRenderParticleShadow(
            sourceObjectName: "ZeppelinBomber",
            particleScreenY: 430f,
            groundScreenY: 500f);

        Assert.IsTrue(shouldRender, "Airborne non-water particles should keep their shadows.");
    }

    [TestMethod]
    public void HandleParticles_KeepsSurfaceBasedAnchorForRenderedParticles()
    {
        var surface = new Surface
        {
            RotatedSurfaceTriangles = new List<ITriangleMeshWithColor>()
        };

        GameState.SurfaceState.SurfaceViewportObject = new _3dObject
        {
            ObjectId = 1,
            ObjectName = "Surface",
            ObjectOffsets = new Vector3(),
            WorldPosition = new Vector3(),
            Rotation = new Vector3()
        };

        var source = new _3dObject
        {
            ObjectId = 2,
            ObjectName = "JumpingFish",
            ParentSurface = surface,
            SurfaceBasedId = 4242,
            ObjectOffsets = new Vector3 { x = 10f, y = 20f, z = 30f },
            WorldPosition = new Vector3(),
            Rotation = new Vector3(),
            Particles = new ParticlesAI
            {
                Particles = new List<IParticle>
                {
                    new Particle
                    {
                        ParticleTriangle = CreateParticleTriangle(),
                        Position = new Vector3 { x = 1f, y = 2f, z = 3f },
                        WorldPosition = new Vector3(),
                        Rotation = new Vector3(),
                        RotationSpeed = new Vector3(),
                        Velocity = new Vector3(),
                        Acceleration = new Vector3(),
                        Color = "d8f6ff",
                        BirthTime = DateTime.UtcNow,
                        Visible = true,
                        ImpactStatus = new ImpactStatus()
                    }
                }
            }
        };

        var renderedParticles = new List<_3dObject>();
        new ParticleManager().HandleParticles(source, renderedParticles);

        Assert.AreEqual(1, renderedParticles.Count, "Only the visible particle should be rendered; JumpingFish splash shadows are suppressed.");
        Assert.AreEqual(source.SurfaceBasedId, renderedParticles[0].SurfaceBasedId,
            "Particles from surface-based objects must keep the same tile anchor as their source object.");
    }

    [TestMethod]
    public void HandleParticles_RendersOriginalParticlesWithoutSurfaceViewport()
    {
        GameState.SurfaceState.SurfaceViewportObject = null;

        var source = new _3dObject
        {
            ObjectId = 2,
            ObjectName = "Ship",
            ObjectOffsets = new Vector3 { x = 10f, y = 20f, z = 430f },
            WorldPosition = new Vector3(),
            Rotation = new Vector3(),
            Particles = new ParticlesAI
            {
                Particles = new List<IParticle>
                {
                    new Particle
                    {
                        ParticleTriangle = CreateParticleTriangle(),
                        Position = new Vector3 { x = 1f, y = 2f, z = 3f },
                        WorldPosition = new Vector3(),
                        Rotation = new Vector3(),
                        RotationSpeed = new Vector3(),
                        Velocity = new Vector3(),
                        Acceleration = new Vector3(),
                        Color = "ff9900",
                        BirthTime = DateTime.UtcNow,
                        Visible = true,
                        ImpactStatus = new ImpactStatus()
                    }
                }
            }
        };

        var renderedParticles = new List<_3dObject>();
        new ParticleManager().HandleParticles(source, renderedParticles);

        Assert.AreEqual(1, renderedParticles.Count,
            "Space scenes without a terrain surface should still render the actual particles.");
        Assert.AreEqual("Particle", renderedParticles[0].ObjectName);
    }

    [TestMethod]
    public void HandleParticles_DoesNotMutateStoredParticleTriangleWhenRotatingForRender()
    {
        GameState.SurfaceState.SurfaceViewportObject = null;

        var particleTriangle = CreateParticleTriangle();
        var source = new _3dObject
        {
            ObjectId = 2,
            ObjectName = "Ship",
            ObjectOffsets = new Vector3 { x = 10f, y = 20f, z = 430f },
            WorldPosition = new Vector3(),
            Rotation = new Vector3(),
            Particles = new ParticlesAI
            {
                Particles = new List<IParticle>
                {
                    new Particle
                    {
                        ParticleTriangle = particleTriangle,
                        Position = new Vector3(),
                        WorldPosition = new Vector3(),
                        Rotation = new Vector3 { x = 0f, y = 0f, z = 90f },
                        RotationSpeed = new Vector3(),
                        Velocity = new Vector3(),
                        Acceleration = new Vector3(),
                        Color = "ff9900",
                        BirthTime = DateTime.UtcNow,
                        Visible = true,
                        ImpactStatus = new ImpactStatus()
                    }
                }
            }
        };

        var renderedParticles = new List<_3dObject>();
        new ParticleManager().HandleParticles(source, renderedParticles);

        Assert.AreEqual(-1f, particleTriangle.vert1.x, 0.001f,
            "Render rotation must use a copied particle triangle, not mutate the stored particle state.");
        Assert.AreEqual(-1f, particleTriangle.vert1.y, 0.001f);
        Assert.AreNotSame(particleTriangle, renderedParticles[0].ObjectParts[0].Triangles[0],
            "Rendered particle geometry should be isolated from the particle's persistent state.");
    }

    private static TriangleMeshWithColor CreateParticleTriangle()
    {
        return new TriangleMeshWithColor
        {
            Color = "d8f6ff",
            noHidden = true,
            vert1 = new Vector3 { x = -1f, y = -1f, z = 0f },
            vert2 = new Vector3 { x = 1f, y = -1f, z = 0f },
            vert3 = new Vector3 { x = 0f, y = 1f, z = 0f }
        };
    }

    private static Vector3 ToVector3(IVector3 vector)
    {
        return new Vector3
        {
            x = vector.x,
            y = vector.y,
            z = vector.z
        };
    }
}
