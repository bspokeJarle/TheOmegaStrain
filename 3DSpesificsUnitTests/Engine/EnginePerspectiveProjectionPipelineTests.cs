using Domain;

namespace _3DSpesificsUnitTests.Engine;

[TestClass]
public class EnginePerspectiveProjectionPipelineTests
{
    [TestMethod]
    public void ConvertObjectTo2d_ProjectsVisibleTriangles()
    {
        var pipeline = CreatePipeline();
        var result = new List<ProjectedTriangleMesh>();

        pipeline.ConvertObjectTo2d(
            CreateRenderableObject(),
            objPosX: 500,
            objPosY: 400,
            objPosZ: 0,
            result);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(480, result[0].X1);
        Assert.AreEqual(380, result[0].Y1);
        Assert.AreEqual(520, result[0].X2);
        Assert.AreEqual(380, result[0].Y2);
        Assert.AreEqual(500, result[0].X3);
        Assert.AreEqual(420, result[0].Y3);
        Assert.AreEqual("Main", result[0].PartName);
        Assert.AreEqual("ffffff", result[0].Color);
    }

    [TestMethod]
    public void ConvertObjectTo2d_MarksDynamicEffectParts()
    {
        var pipeline = CreatePipeline();
        var result = new List<ProjectedTriangleMesh>();

        pipeline.ConvertObjectTo2d(
            CreateRenderableObject(partName: RenderPipelineMarkers.MuzzleFlashPartName),
            objPosX: 500,
            objPosY: 400,
            objPosZ: 0,
            result);

        Assert.AreEqual(1, result.Count);
        Assert.IsTrue(result[0].UseEffectRenderingPipeline);
    }

    [TestMethod]
    public void ConvertCrashBoxesTo2d_ProjectsAabbDebugFaces()
    {
        var pipeline = CreatePipeline();
        var result = new List<ProjectedTriangleMesh>();
        var obj = CreateRenderableObject();
        obj.ObjectName = "Surface";
        obj.CrashBoxes = new List<List<IVector3>>
        {
            MeshGeometryOperations.GenerateCrashBoxCorners(
                new EngineVector3(-50f, -50f, -50f),
                new EngineVector3(50f, 50f, 50f),
                static (x, y, z) => new EngineVector3(x, y, z))
                .Cast<IVector3>()
                .ToList()
        };

        pipeline.ConvertCrashBoxesTo2d(
            obj,
            objPosX: 500,
            objPosY: 400,
            objPosZ: 0,
            result);

        Assert.IsTrue(result.Count > 0);
        Assert.IsTrue(result.All(t => t.PartName == "CrashBox-Surface"));
        Assert.IsTrue(result.All(t => t.Color == "FF00FF"));
    }

    [TestMethod]
    public void EstimateTriangleCapacity_UsesClientVisibilityPredicateWithoutAllocatingFilteredObjects()
    {
        var pipeline = CreatePipeline();
        var objects = new List<IRenderable3dObject>
        {
            CreateRenderableObject(),
            CreateRenderableObject("Hidden"),
            CreateRenderableObject()
        };

        int capacity = pipeline.EstimateTriangleCapacity(
            objects,
            obj => obj.ObjectName != "Hidden",
            includeCrashBoxDebug: null);

        Assert.AreEqual(4, capacity);
    }

    [TestMethod]
    public void PerspectiveWorldProjector_UsesInjectedVisibilityAndRenderPosition()
    {
        var projector = new PerspectiveWorldProjector<Engine3dObject, ProjectedTriangleMesh>(
            new ProjectionViewport(
                screenWidth: 1000,
                screenHeight: 800,
                perspectiveAdjustment: 1500,
                objectZoom: 2),
            static () => new ProjectedTriangleMesh(),
            static (Engine3dObject obj, IProjectionViewport viewport, out RenderPosition position) =>
            {
                position = new RenderPosition(viewport.ScreenCenterX, viewport.ScreenCenterY, 0);
                return true;
            },
            static obj => obj.ObjectName != "Hidden",
            static _ => false);
        var reusable = new List<ProjectedTriangleMesh>(capacity: 1);

        var result = projector.ProjectToTriangles(
            new List<Engine3dObject>
            {
                CreateRenderableObject(),
                CreateRenderableObject("Hidden")
            },
            currentFrame: 1,
            reusable);

        Assert.AreSame(reusable, result);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Main", result[0].PartName);
        Assert.IsTrue(result.Capacity >= 2);
    }

    private static PerspectiveProjectionPipeline<ProjectedTriangleMesh> CreatePipeline()
    {
        return new PerspectiveProjectionPipeline<ProjectedTriangleMesh>(
            new ProjectionViewport(
                screenWidth: 1000,
                screenHeight: 800,
                perspectiveAdjustment: 1500,
                objectZoom: 2),
            static () => new ProjectedTriangleMesh());
    }

    private static Engine3dObject CreateRenderableObject(string partName = "Main")
    {
        return new Engine3dObject
        {
            ObjectId = 1,
            ObjectName = partName,
            ObjectOffsets = new EngineVector3(),
            WorldPosition = new EngineVector3(),
            CrashBoxes = new List<List<IVector3>>(),
            ObjectParts = new List<I3dObjectPart>
            {
                new Engine3dObjectPart
                {
                    PartName = partName,
                    IsVisible = true,
                    Triangles = new List<ITriangleMeshWithColor>
                    {
                        new EngineTriangleMeshWithColor
                        {
                            Color = "ffffff",
                            noHidden = true,
                            normal1 = new EngineVector3(0f, 0f, 1f),
                            vert1 = new EngineVector3(-10f, -10f, 0f),
                            vert2 = new EngineVector3(10f, -10f, 0f),
                            vert3 = new EngineVector3(0f, 10f, 0f)
                        }
                    }
                }
            }
        };
    }

    private sealed class EngineTriangleMeshWithColor : EngineTriangleMesh, ITriangleMeshWithColor
    {
        public string? Color { get; set; }
    }
}
