namespace RetroMesh.Engine.Tests;

[TestClass]
public class EngineObjectClonerTests
{
    [TestMethod]
    public void CopyRenderableObjects_ReusesResultListAndRunsAdditionalStateCallback()
    {
        var source = CreateRenderableObject();
        var result = new List<Engine3dObject>
        {
            CreateRenderableObject()
        };
        int originalCapacity = result.Capacity;
        string? copiedTag = null;

        EngineObjectCloner.CopyRenderableObjects(
            new[] { source },
            result,
            static objectId => new Engine3dObject { ObjectId = objectId },
            static () => new Engine3dObjectPart(),
            static () => new EngineTriangleMeshWithColor(),
            static vector => new EngineVector3(vector.x, vector.y, vector.z),
            copyCrashboxes: true,
            (original, copy) =>
            {
                copiedTag = $"{original.ObjectName}:{copy.ObjectId}";
                copy.ObjectName = "CopiedByCallback";
            });

        Assert.AreEqual(1, result.Count);
        Assert.IsTrue(result.Capacity >= originalCapacity);
        Assert.AreEqual("Source:12", copiedTag);
        Assert.AreEqual("CopiedByCallback", result[0].ObjectName);
        Assert.AreNotSame(source.CrashBoxes, result[0].CrashBoxes);
        Assert.AreNotSame(source.CrashBoxes[0][0], result[0].CrashBoxes[0][0]);
    }

    private static Engine3dObject CreateRenderableObject() =>
        new()
        {
            ObjectId = 12,
            ObjectName = "Source",
            ObjectParts = new List<I3dObjectPart>
            {
                new Engine3dObjectPart
                {
                    PartName = "Body",
                    IsVisible = true,
                    Triangles = new List<ITriangleMeshWithColor>
                    {
                        new EngineTriangleMeshWithColor
                        {
                            Color = "ffffff",
                            vert1 = new EngineVector3(1, 2, 3),
                            vert2 = new EngineVector3(4, 5, 6),
                            vert3 = new EngineVector3(7, 8, 9),
                            normal1 = new EngineVector3(0, 0, 1),
                            normal2 = new EngineVector3(0, 1, 0),
                            normal3 = new EngineVector3(1, 0, 0)
                        }
                    }
                }
            },
            CrashBoxes = new List<List<IVector3>>
            {
                new()
                {
                    new EngineVector3(1, 2, 3),
                    new EngineVector3(4, 5, 6)
                }
            }
        };

    private sealed class EngineTriangleMeshWithColor : EngineTriangleMesh, ITriangleMeshWithColor
    {
        public string? Color { get; set; }
    }
}
