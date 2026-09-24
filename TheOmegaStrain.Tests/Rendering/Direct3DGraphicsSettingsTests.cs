using RetroMesh.Engine;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Wpf.Rendering;

namespace TheOmegaStrain.Tests.Rendering;

[TestClass]
public class Direct3DGraphicsSettingsTests
{
    [TestMethod]
    public void Apply_GlowEnabled_AddsTwoTransparentScaledPassesBeforeRendering()
    {
        var triangles = new List<ProjectedTriangleMesh>
        {
            CreateTriangle("Lazer_Core", "00ff00")
        };

        Direct3DGraphicsSettings.Apply(triangles, new GameSettingsState { GlowEffectsEnabled = true });

        Assert.AreEqual(3, triangles.Count);
        Assert.AreEqual(0f, triangles[0].Opacity);
        Assert.IsTrue(triangles.Skip(1).All(t => t.Opacity > 0f && t.Opacity < 1f));
        Assert.IsTrue(triangles.Skip(1).All(t => t.TextureId == null));
        Assert.IsTrue(triangles.Skip(1).All(t => TriangleWidth(t) > TriangleWidth(triangles[0])));
    }

    [TestMethod]
    public void Apply_HighEnhancedShadows_AddsSoftShadowPass()
    {
        var triangles = new List<ProjectedTriangleMesh> { CreateTriangle("Shadow", "black") };

        Direct3DGraphicsSettings.Apply(triangles, new GameSettingsState
        {
            GraphicsQuality = GraphicsQualityPreset.High,
            EnhancedShadowsEnabled = true
        });

        Assert.AreEqual(2, triangles.Count);
        Assert.AreEqual("000000", triangles[1].Color);
        Assert.AreEqual(70f / 255f, triangles[1].Opacity, 0.0001f);
        Assert.IsTrue(TriangleWidth(triangles[1]) > TriangleWidth(triangles[0]));
    }

    [TestMethod]
    public void Apply_QualityPresetChangesColorAndSupportsNamedLegacyColors()
    {
        var low = new List<ProjectedTriangleMesh> { CreateTriangle("Hull", "red") };
        var high = new List<ProjectedTriangleMesh> { CreateTriangle("Hull", "red") };

        Direct3DGraphicsSettings.Apply(low, new GameSettingsState { GraphicsQuality = GraphicsQualityPreset.Low });
        Direct3DGraphicsSettings.Apply(high, new GameSettingsState { GraphicsQuality = GraphicsQualityPreset.High });

        Assert.AreNotEqual("000000", low[0].Color);
        Assert.AreNotEqual(low[0].Color, high[0].Color);
    }

    [TestMethod]
    public void Apply_CrashBoxUsesSameTransparencyAsWpfBackend()
    {
        var triangles = new List<ProjectedTriangleMesh> { CreateTriangle("CrashBox-Ship", "ffffff") };

        Direct3DGraphicsSettings.Apply(triangles, new GameSettingsState());

        Assert.AreEqual(0.25f, triangles[0].Opacity);
    }

    [TestMethod]
    public void Apply_CloudBodyIsOpaqueWithoutChangingOtherObjects()
    {
        var triangles = new List<ProjectedTriangleMesh>
        {
            CreateTriangle(CloudVisualSetup.BodyPartName, "D3E5EA"),
            CreateTriangle("SeederBody", "D3E5EA")
        };

        Direct3DGraphicsSettings.Apply(triangles, new GameSettingsState());

        Assert.AreEqual(1f, triangles[0].Opacity);
        Assert.AreEqual(0f, triangles[1].Opacity);
    }

    private static ProjectedTriangleMesh CreateTriangle(string partName, string color) => new()
    {
        PartName = partName,
        Color = color,
        X1 = 10,
        Y1 = 10,
        X2 = 20,
        Y2 = 10,
        X3 = 15,
        Y3 = 20,
        CalculatedZ = 100f,
        Rhw1 = 1f,
        Rhw2 = 1f,
        Rhw3 = 1f
    };

    private static int TriangleWidth(ProjectedTriangleMesh triangle) =>
        Math.Max(triangle.X1, Math.Max(triangle.X2, triangle.X3)) -
        Math.Min(triangle.X1, Math.Min(triangle.X2, triangle.X3));
}
