
namespace RetroMesh.Engine.Tests;

[TestClass]
public class EngineProjectedTriangleRenderMathTests
{
    [TestMethod]
    public void CullTrianglesOutsideRenderDepth_RemovesOutOfRangeTriangles()
    {
        var triangles = new List<ProjectedTriangleMesh>
        {
            new() { CalculatedZ = -11, PartName = "TooNear" },
            new() { CalculatedZ = 0, PartName = "KeepMiddle" },
            new() { CalculatedZ = 11, PartName = "TooFar" },
            new() { CalculatedZ = -10, PartName = "KeepNearBoundary" },
            new() { CalculatedZ = 10, PartName = "KeepFarBoundary" }
        };

        int kept = ProjectedTriangleRenderMath.CullTrianglesOutsideRenderDepth(triangles, nearZ: -10, farZ: 10);

        Assert.AreEqual(3, kept);
        Assert.AreEqual(3, triangles.Count);
        Assert.AreEqual("KeepMiddle", triangles[0].PartName);
        Assert.AreEqual("KeepNearBoundary", triangles[1].PartName);
        Assert.AreEqual("KeepFarBoundary", triangles[2].PartName);
    }

    [TestMethod]
    public void SortTrianglesByDepth_OrdersByCalculatedZ()
    {
        var triangles = new List<ProjectedTriangleMesh>
        {
            new() { CalculatedZ = 15, PartName = "Far" },
            new() { CalculatedZ = -5, PartName = "Near" },
            new() { CalculatedZ = 2, PartName = "Middle" }
        };

        ProjectedTriangleRenderMath.SortTrianglesByDepth(triangles);

        CollectionAssert.AreEqual(
            new[] { "Near", "Middle", "Far" },
            triangles.Select(t => t.PartName).ToArray());
    }

    [TestMethod]
    public void ShouldUseEffectRenderingPipeline_UsesMarkersAndClientPredicates()
    {
        var defaultOptions = new ProjectedTriangleRenderOptions();
        Assert.IsTrue(ProjectedTriangleRenderMath.ShouldUseEffectRenderingPipeline(
            new ProjectedTriangleMesh { PartName = RenderPipelineMarkers.MuzzleFlashPartName },
            defaultOptions));

        Assert.IsTrue(ProjectedTriangleRenderMath.ShouldUseEffectRenderingPipeline(
            new ProjectedTriangleMesh { PartName = "AnyPart", UseEffectRenderingPipeline = true },
            defaultOptions));

        var glowOptions = new ProjectedTriangleRenderOptions
        {
            GlowEffectsEnabled = true,
            IsGlowCandidatePartName = partName => partName == "PowerUpBody"
        };
        Assert.IsTrue(ProjectedTriangleRenderMath.ShouldUseEffectRenderingPipeline(
            new ProjectedTriangleMesh { PartName = "PowerUpBody" },
            glowOptions));

        var shadowOptions = new ProjectedTriangleRenderOptions
        {
            HighGraphicsQuality = true,
            EnhancedShadowsEnabled = true,
            IsEnhancedShadowCandidatePartName = partName => partName == "Shadow"
        };
        Assert.IsTrue(ProjectedTriangleRenderMath.ShouldUseEffectRenderingPipeline(
            new ProjectedTriangleMesh { PartName = "Shadow" },
            shadowOptions));

        Assert.IsFalse(ProjectedTriangleRenderMath.ShouldUseEffectRenderingPipeline(
            new ProjectedTriangleMesh { PartName = "HouseWall" },
            defaultOptions));
    }

    [TestMethod]
    public void NormalizeColor_TrimsHashAndFallsBackToBlack()
    {
        Assert.AreEqual("000000", ProjectedTriangleRenderMath.NormalizeColor(null));
        Assert.AreEqual("00ffaa", ProjectedTriangleRenderMath.NormalizeColor(" #00FFAA "));
    }

    [TestMethod]
    public void PartNameHelpers_IdentifyCrashBoxesAndDynamicEffects()
    {
        Assert.IsTrue(ProjectedTriangleRenderMath.IsCrashBoxPartName("CrashBox-Surface"));
        Assert.IsFalse(ProjectedTriangleRenderMath.IsCrashBoxPartName("Surface"));
        Assert.IsTrue(ProjectedTriangleRenderMath.ShouldRenderAsSeparateTriangle(RenderPipelineMarkers.ParticlePartName));
        Assert.IsTrue(ProjectedTriangleRenderMath.IsExplodingPartName(RenderPipelineMarkers.ExplodingPartName));
        Assert.AreEqual(2, ProjectedTriangleRenderMath.CountCrashBoxParts(
            new[] { "Surface", "CrashBox-A", "CrashBox-B" }));
    }
}
