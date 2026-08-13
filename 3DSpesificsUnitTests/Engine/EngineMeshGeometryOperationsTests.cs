using Domain;

namespace _3DSpesificsUnitTests.Engine;

[TestClass]
public class EngineMeshGeometryOperationsTests
{
    [TestMethod]
    public void ApplyScaleToObject_ScalesSharedVerticesOnlyOnceAndScalesCrashBoxes()
    {
        var shared = new EngineVector3(1f, 2f, 3f);
        var obj = new Engine3dObject
        {
            ObjectId = 1,
            ObjectParts = new List<I3dObjectPart>
            {
                new Engine3dObjectPart
                {
                    Triangles = new List<ITriangleMeshWithColor>
                    {
                        new EngineTriangleMeshWithColor
                        {
                            vert1 = shared,
                            vert2 = shared,
                            vert3 = new EngineVector3(2f, 3f, 4f)
                        }
                    }
                }
            },
            CrashBoxes = new List<List<IVector3>>
            {
                new()
                {
                    new EngineVector3(1f, 2f, 3f),
                    new EngineVector3(4f, 5f, 6f)
                }
            }
        };

        MeshGeometryOperations.ApplyScaleToObject(
            obj,
            2f,
            static v => new EngineVector3(v.x, v.y, v.z));

        Assert.AreEqual(2f, shared.x, 0.001f);
        Assert.AreEqual(4f, shared.y, 0.001f);
        Assert.AreEqual(6f, shared.z, 0.001f);
        Assert.AreEqual(8f, obj.CrashBoxes[0][1].x, 0.001f);
        Assert.AreEqual(10f, obj.CrashBoxes[0][1].y, 0.001f);
        Assert.AreEqual(12f, obj.CrashBoxes[0][1].z, 0.001f);
    }

    [TestMethod]
    public void NormalizeSurfaceFootprintPivot_MovesMainMeshAndKeepsShadowGroundPlanePinned()
    {
        var obj = new Engine3dObject
        {
            ObjectId = 2,
            ObjectParts = new List<I3dObjectPart>
            {
                new Engine3dObjectPart
                {
                    IsVisible = true,
                    PartName = "Main",
                    Triangles = new List<ITriangleMeshWithColor>
                    {
                        new EngineTriangleMeshWithColor
                        {
                            vert1 = new EngineVector3(10f, 4f, 10f),
                            vert2 = new EngineVector3(30f, 6f, 10f),
                            vert3 = new EngineVector3(200f, 80f, 110f)
                        }
                    }
                },
                new Engine3dObjectPart
                {
                    IsVisible = false,
                    PartName = "Shadow",
                    Triangles = new List<ITriangleMeshWithColor>
                    {
                        new EngineTriangleMeshWithColor
                        {
                            vert1 = new EngineVector3(10f, 4f, 0f),
                            vert2 = new EngineVector3(30f, 6f, 0f),
                            vert3 = new EngineVector3(20f, 5f, 20f)
                        }
                    }
                }
            },
            CrashBoxes = new List<List<IVector3>>
            {
                new()
                {
                    new EngineVector3(10f, 4f, 10f),
                    new EngineVector3(30f, 6f, 20f)
                }
            }
        };

        MeshGeometryOperations.NormalizeSurfaceFootprintPivot(
            obj,
            static v => new EngineVector3(v.x, v.y, v.z));

        Assert.IsTrue(obj.UseSurfaceFootprintPivot);
        Assert.AreEqual(-10f, obj.ObjectParts[0].Triangles[0].vert1.x, 0.001f);
        Assert.AreEqual(-1f, obj.ObjectParts[0].Triangles[0].vert1.y, 0.001f);
        Assert.AreEqual(0f, obj.ObjectParts[0].Triangles[0].vert1.z, 0.001f);
        Assert.AreEqual(0f, obj.ObjectParts[1].Triangles[0].vert1.z, 0.001f);
        Assert.AreEqual(20f, obj.ObjectParts[1].Triangles[0].vert3.z, 0.001f);
        Assert.AreEqual(-10f, obj.CrashBoxes[0][0].x, 0.001f);
        Assert.AreEqual(0f, obj.CrashBoxes[0][0].z, 0.001f);
    }

    [TestMethod]
    public void AddSimplifiedShadowPart_AddsInvisibleShadowWithGroundPlaneTriangles()
    {
        var obj = new Engine3dObject
        {
            ObjectId = 3,
            ObjectParts = new List<I3dObjectPart>
            {
                new Engine3dObjectPart
                {
                    IsVisible = true,
                    PartName = "Main",
                    Triangles = new List<ITriangleMeshWithColor>
                    {
                        new EngineTriangleMeshWithColor
                        {
                            vert1 = new EngineVector3(-5f, -5f, 0f),
                            vert2 = new EngineVector3(5f, -5f, 0f),
                            vert3 = new EngineVector3(0f, 5f, 10f)
                        }
                    }
                }
            }
        };

        MeshGeometryOperations.AddSimplifiedShadowPart(
            obj,
            static () => new Engine3dObjectPart(),
            static () => new EngineTriangleMeshWithColor(),
            static (x, y, z) => new EngineVector3(x, y, z),
            useFlatQuad: true);

        var shadow = obj.ObjectParts.Single(part => part.PartName == "Shadow");
        Assert.IsFalse(shadow.IsVisible);
        Assert.IsTrue(shadow.Triangles.Count > 0);
        Assert.IsTrue(shadow.Triangles.All(t => Math.Abs(t.vert1.z) < 0.001f
            && Math.Abs(t.vert2.z) < 0.001f
            && Math.Abs(t.vert3.z) < 0.001f));
    }

    [TestMethod]
    public void GenerateCrashBoxCorners_UsesExistingCornerOrder()
    {
        var corners = MeshGeometryOperations.GenerateCrashBoxCorners(
            new EngineVector3(1f, 2f, 3f),
            new EngineVector3(4f, 5f, 6f),
            static (x, y, z) => new EngineVector3(x, y, z));

        Assert.AreEqual(8, corners.Count);
        Assert.AreEqual(1f, corners[0].x, 0.001f);
        Assert.AreEqual(5f, corners[0].y, 0.001f);
        Assert.AreEqual(3f, corners[0].z, 0.001f);
        Assert.AreEqual(1f, corners[7].x, 0.001f);
        Assert.AreEqual(2f, corners[7].y, 0.001f);
        Assert.AreEqual(6f, corners[7].z, 0.001f);
    }

    [TestMethod]
    public void CheckAabbOverlap_ReturnsFalseForEmptyBoxes()
    {
        bool overlaps = MeshGeometryOperations.CheckAabbOverlap(
            Array.Empty<IVector3>(),
            new List<IVector3>
            {
                new EngineVector3(0f, 0f, 0f),
                new EngineVector3(1f, 1f, 1f)
            },
            0f,
            0f,
            0f,
            out _,
            out _);

        Assert.IsFalse(overlaps);
    }

    private sealed class EngineTriangleMeshWithColor : EngineTriangleMesh, ITriangleMeshWithColor
    {
        public string? Color { get; set; }
    }
}
