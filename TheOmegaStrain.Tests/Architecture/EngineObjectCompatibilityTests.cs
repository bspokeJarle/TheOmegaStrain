using TheOmegaStrain.Domain;
using TheOmegaStrain.Game.Projection;
using TheOmegaStrain.Game.World.Objects;
using TheOmegaStrain.Wpf.Rendering;

namespace TheOmegaStrain.Tests.Architecture;

[TestClass]
public class EngineObjectCompatibilityTests
{
    [TestMethod]
    public void DomainVector3_KeepsExistingOperatorsAndType()
    {
        var result = new Vector3(1, 2, 3) + new Vector3(4, 5, 6);

        Assert.IsInstanceOfType(result, typeof(Vector3));
        Assert.AreEqual(5, result.x);
        Assert.AreEqual(7, result.y);
        Assert.AreEqual(9, result.z);
    }

    [TestMethod]
    public void DomainTriangleMesh_LazyVectorsRemainDomainVector3()
    {
        var mesh = new TriangleMesh();

        Assert.IsNull(mesh.Vert1Raw);
        Assert.IsInstanceOfType(mesh.vert1, typeof(Vector3));
        Assert.IsInstanceOfType(mesh.Vert1Raw, typeof(Vector3));
    }

    [TestMethod]
    public void DomainObject_ImplementsGameplayAndEngineContracts()
    {
        I3dObject gameplayObject = new OmegaObject3D
        {
            ObjectId = 1,
            ObjectName = "TestObject"
        };

        Assert.IsInstanceOfType(gameplayObject, typeof(IRenderable3dObject));

        var renderable = (IRenderable3dObject)gameplayObject;
        renderable.ObjectParts.Add(new OmegaObjectPart3D { PartName = "Hull" });

        gameplayObject.HasPowerUp = true;
        gameplayObject.PowerUpType = PowerUpType.TravelSpeedLevel1;

        Assert.AreEqual(1, renderable.ObjectParts.Count);
        Assert.AreEqual("Hull", renderable.ObjectParts[0].PartName);
        Assert.IsTrue(gameplayObject.HasPowerUp);
        Assert.AreEqual(PowerUpType.TravelSpeedLevel1, gameplayObject.PowerUpType);
    }

    [TestMethod]
    public void DomainWorld_ExposesRenderableWorldView()
    {
        I3dWorld world = new TestWorld
        {
            WorldInhabitants = new List<I3dObject>
            {
                new OmegaObject3D { ObjectId = 1, ObjectName = "Ship" },
                new OmegaObject3D { ObjectId = 2, ObjectName = "Surface" }
            }
        };

        var renderableWorld = (IRenderable3dWorldView)world;

        Assert.AreEqual(2, renderableWorld.RenderableObjects.Count());
        Assert.IsTrue(renderableWorld.RenderableObjects.All(o => o is IRenderable3dObject));
    }

    [TestMethod]
    public void RenderableWorldView_TracksWorldInhabitantsWithoutCopying()
    {
        I3dWorld world = new TestWorld();
        var renderableWorld = (IRenderable3dWorldView)world;

        world.WorldInhabitants.Add(new OmegaObject3D { ObjectId = 1, ObjectName = "Ship" });

        Assert.AreEqual("Ship", renderableWorld.RenderableObjects.Single().ObjectName);

        world.WorldInhabitants.Clear();

        Assert.AreEqual(0, renderableWorld.RenderableObjects.Count());
    }

    [TestMethod]
    public void DomainImpactStatus_ImplementsGameplayAndEngineImpactContracts()
    {
        IImpactStatus gameplayImpact = new ImpactStatus
        {
            HasCrashed = true,
            HasExploded = true,
            ObjectName = "Surface",
            CrashBoxName = "MainSurface",
            ImpactDirection = ImpactDirection.Bottom,
            ObjectHealth = 42
        };

        Assert.IsInstanceOfType(gameplayImpact, typeof(IImpactState));

        var engineImpact = (IImpactState)gameplayImpact;

        Assert.IsTrue(engineImpact.HasCrashed);
        Assert.IsTrue(engineImpact.HasExploded);
        Assert.AreEqual("Surface", engineImpact.ObjectName);
        Assert.AreEqual("MainSurface", engineImpact.CrashBoxName);
        Assert.AreEqual(ImpactDirection.Bottom, engineImpact.ImpactDirection);
        Assert.AreEqual(42, gameplayImpact.ObjectHealth);
    }

    [TestMethod]
    public void DomainGameLoop_ExtendsEngineFrameLoopContract()
    {
        Assert.IsTrue(
            typeof(IGameLoop<object>).GetInterfaces().Contains(typeof(IWorldFrameLoop<I3dWorld, object>)));
    }

    [TestMethod]
    public void ProjectionConverter_ImplementsEngineProjectionContract()
    {
        Assert.IsInstanceOfType(
            OmegaPerspectiveProjectorFactory.Create(),
            typeof(IWorldProjector<OmegaObject3D, ProjectedTriangleMesh>));
        Assert.AreEqual("RetroMesh.Engine", typeof(PerspectiveWorldProjector<,>).Namespace);
    }

    [TestMethod]
    public void ProjectionViewport_ExposesEngineProjectionSettings()
    {
        IProjectionViewport viewport = new ProjectionViewport(
            screenWidth: 1000,
            screenHeight: 800,
            perspectiveAdjustment: 1500,
            objectZoom: 2);

        Assert.AreEqual(1000, viewport.ScreenWidth);
        Assert.AreEqual(800, viewport.ScreenHeight);
        Assert.AreEqual(500, viewport.ScreenCenterX);
        Assert.AreEqual(400, viewport.ScreenCenterY);
        Assert.AreEqual(1500, viewport.PerspectiveAdjustment);
        Assert.AreEqual(2, viewport.ObjectZoom);
    }

    [TestMethod]
    public void EngineRenderingHelpers_ExposeDynamicEffectPipelineRules()
    {
        Assert.IsTrue(RenderPipelineMarkers.IsDynamicEffectPartName("ExplodingPart"));
        Assert.IsTrue(RenderPipelineMarkers.IsDynamicEffectPartName("Particle"));
        Assert.IsTrue(RenderPipelineMarkers.IsDynamicEffectPartName("ParticleShadow"));
        Assert.IsTrue(RenderPipelineMarkers.IsDynamicEffectPartName("MuzzleFlash"));
        Assert.IsFalse(RenderPipelineMarkers.IsDynamicEffectPartName("Surface"));

        Assert.IsTrue(RenderPipelineMarkers.ShouldUseEffectRenderingPipeline("Particle", "Main"));
        Assert.IsFalse(RenderPipelineMarkers.ShouldUseEffectRenderingPipeline("Seeder", "Hull"));
    }

    [TestMethod]
    public void EngineRenderingHelpers_ShadeHexColorsWithoutGameplayDependencies()
    {
        Assert.AreEqual("#030303", RenderColorShading.GetShadeOfColorFromNormal(0f, "FF0000"));
        Assert.AreEqual("#820303", RenderColorShading.GetShadeOfColorFromNormal(0.5f, "FF0000"));
        Assert.AreEqual("#000000", RenderColorShading.GetShadeOfColorFromNormal(1f, "bad"));
        Assert.AreEqual("#040506", RenderColorShading.GetShadeOfColorFromNormal(1f, "010203"));
    }

    [TestMethod]
    public void EngineRenderingHelpers_CalculateDepthAndAngleShadeKeys()
    {
        Assert.AreEqual(0f, RenderShadeMath.GetDepthFactor01(-2000f, -1000f, 1000f));
        Assert.AreEqual(0.5f, RenderShadeMath.GetDepthFactor01(0f, -1000f, 1000f));
        Assert.AreEqual(1f, RenderShadeMath.GetDepthFactor01(1000f, -1000f, 1000f));

        Assert.AreEqual(0f, RenderShadeMath.NormalizeAngleTo01(-1f));
        Assert.AreEqual(0.5f, RenderShadeMath.NormalizeAngleTo01(0f));
        Assert.AreEqual(1f, RenderShadeMath.NormalizeAngleTo01(1f));

        Assert.AreEqual(0.25f, RenderShadeMath.GetTriangleShadeKey(
            calculatedZ: 0f,
            triangleAngle: 0f,
            nearZ: -1000f,
            farZ: 1000f));
        Assert.AreEqual(0.5f, RenderShadeMath.GetTriangleShadeKey(
            calculatedZ: 0f,
            triangleAngle: -1f,
            nearZ: -1000f,
            farZ: 1000f,
            useDepthOnlyShading: true));
    }

    [TestMethod]
    public void ProjectedTriangleMesh_ImplementsEngineProjectedTriangleContract()
    {
        IProjectedTriangle triangle = new ProjectedTriangleMesh
        {
            PartName = "Main",
            Color = "00FFFF",
            X1 = 1,
            Y1 = 2,
            X2 = 3,
            Y2 = 4,
            X3 = 5,
            Y3 = 6,
            CalculatedZ = 7,
            TriangleAngle = 0.5f,
            Normal = 1f,
            UseEffectRenderingPipeline = true
        };

        Assert.AreEqual("Main", triangle.PartName);
        Assert.AreEqual("00FFFF", triangle.Color);
        Assert.IsTrue(triangle.UseEffectRenderingPipeline);
        Assert.AreEqual(7, triangle.CalculatedZ);
    }

    [TestMethod]
    public void WorldRenderer_ImplementsEngineProjectedTriangleRendererContract()
    {
        Assert.IsTrue(
            typeof(IProjectedTriangleRenderer<ProjectedTriangleMesh>).IsAssignableFrom(typeof(WorldRenderer)));
    }

    [TestMethod]
    public void ThreeDRotationsSource_DoesNotUseWpfApisDirectly()
    {
        var repositoryRoot = FindRepositoryRoot();
        var forbiddenHits = FindForbiddenSourceHits(repositoryRoot, Path.Combine(repositoryRoot, "TheOmegaStrain.Game"));

        Assert.AreEqual(0, forbiddenHits.Count, string.Join(Environment.NewLine, forbiddenHits));
    }

    [TestMethod]
    public void OmegaEngineAdaptersSource_DoesNotUseWpfApisDirectly()
    {
        var repositoryRoot = FindRepositoryRoot();
        var forbiddenHits = FindForbiddenSourceHits(
            repositoryRoot,
            Path.Combine(repositoryRoot, "TheOmegaStrain.Common", "OmegaEngineAdapters"));

        Assert.AreEqual(0, forbiddenHits.Count, string.Join(Environment.NewLine, forbiddenHits));
    }

    [TestMethod]
    public void TheOmegaStrainCommonSource_DoesNotUseWpfApisDirectly()
    {
        var repositoryRoot = FindRepositoryRoot();
        var forbiddenHits = FindForbiddenSourceHits(repositoryRoot, Path.Combine(repositoryRoot, "TheOmegaStrain.Common"));

        Assert.AreEqual(0, forbiddenHits.Count, string.Join(Environment.NewLine, forbiddenHits));
    }

    [TestMethod]
    public void RetroMeshEngineSource_DoesNotReferenceGameProjects()
    {
        var repositoryRoot = FindRepositoryRoot();
        var engineDirectory = Path.Combine(repositoryRoot, "RetroMesh", "RetroMesh.Engine");
        var forbiddenHits = Directory
            .EnumerateFiles(engineDirectory, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                           path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                           !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .SelectMany(path => File
                .ReadLines(path)
                .Select((line, index) => new { path, line, lineNumber = index + 1 }))
            .Where(hit =>
                hit.line.Contains("ProjectReference", StringComparison.Ordinal) ||
                hit.line.Contains("using TheOmegaStrain.Domain", StringComparison.Ordinal) ||
                hit.line.Contains("using TheOmegaStrain.Common", StringComparison.Ordinal) ||
                hit.line.Contains("using GameAi", StringComparison.Ordinal) ||
                hit.line.Contains("using TheOmegaStrain.Game", StringComparison.Ordinal) ||
                hit.line.Contains("using TheOmegaStrain.Wpf", StringComparison.Ordinal) ||
                hit.line.Contains("GameState", StringComparison.Ordinal) ||
                hit.line.Contains("ScreenSetup", StringComparison.Ordinal) ||
                hit.line.Contains("TheOmegaStrain.Steam", StringComparison.Ordinal))
            .Select(hit => $"{Path.GetRelativePath(repositoryRoot, hit.path)}:{hit.lineNumber}: {hit.line.Trim()}")
            .ToList();

        Assert.AreEqual(0, forbiddenHits.Count, string.Join(Environment.NewLine, forbiddenHits));
    }

    [TestMethod]
    public void RuntimeCrashDetectionSource_DoesNotUseRotationHelperMathDirectly()
    {
        var repositoryRoot = FindRepositoryRoot();
        var crashDetectionDirectory = Path.Combine(repositoryRoot, "TheOmegaStrain.Runtime", "CrashDetection");
        var forbiddenHits = Directory
            .EnumerateFiles(crashDetectionDirectory, "*.cs", SearchOption.AllDirectories)
            .SelectMany(path => File
                .ReadLines(path)
                .Select((line, index) => new { path, line, lineNumber = index + 1 }))
            .Where(hit =>
                hit.line.Contains("using TheOmegaStrain.Game.Helpers", StringComparison.Ordinal) ||
                hit.line.Contains("TheOmegaStrain.Common.OmegaEngineAdapters", StringComparison.Ordinal) ||
                hit.line.Contains("OmegaObject3DHelpers", StringComparison.Ordinal) ||
                hit.line.Contains("ObjectPlacementHelpers", StringComparison.Ordinal) ||
                IsForbiddenCrashExtensionCall(hit.line))
            .Select(hit => $"{Path.GetRelativePath(repositoryRoot, hit.path)}:{hit.lineNumber}: {hit.line.Trim()}")
            .ToList();

        Assert.AreEqual(0, forbiddenHits.Count, string.Join(Environment.NewLine, forbiddenHits));
    }

    private static bool IsForbiddenCrashExtensionCall(string line)
    {
        if (line.Contains("CrashBoxTransform.", StringComparison.Ordinal))
            return false;

        return line.Contains(".GetEffectiveCrashOffset(", StringComparison.Ordinal) ||
               line.Contains(".ToCrashWorldPoints(", StringComparison.Ordinal) ||
               line.Contains(".GetAllCrashPointsWorld(", StringComparison.Ordinal);
    }

    private static List<string> FindForbiddenSourceHits(string repositoryRoot, string projectDirectory)
    {
        return Directory
            .EnumerateFiles(projectDirectory, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                           path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                           !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .SelectMany(path => File
                .ReadLines(path)
                .Select((line, index) => new { path, line, lineNumber = index + 1 }))
            .Where(hit =>
                hit.line.Contains("System.Windows", StringComparison.Ordinal) ||
                hit.line.Contains("WriteableBitmap", StringComparison.Ordinal) ||
                hit.line.Contains("BitmapSource", StringComparison.Ordinal) ||
                hit.line.Contains("<UseWPF>", StringComparison.Ordinal))
            .Select(hit => $"{Path.GetRelativePath(repositoryRoot, hit.path)}:{hit.lineNumber}: {hit.line.Trim()}")
            .ToList();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "TheOmegaStrain.sln")))
        {
            directory = directory.Parent;
        }

        Assert.IsNotNull(directory, "Could not locate repository root from test output directory.");
        return directory.FullName;
    }

    private sealed class TestWorld : I3dWorld
    {
        public List<I3dObject> WorldInhabitants { get; set; } = new();
        public ISceneHandler SceneHandler { get; set; } = null!;
        public IGameEventBus? EventBus { get; set; }
        public bool IsPaused { get; set; }
    }
}
