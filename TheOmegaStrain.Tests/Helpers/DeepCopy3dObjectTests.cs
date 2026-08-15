using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Domain;
using static TheOmegaStrain.Domain._3dSpecificsImplementations;

namespace TheOmegaStrain.Tests.Helpers;

[TestClass]
public class DeepCopy3dObjectTests
{
    [TestMethod]
    public void DeepCopy3dObjects_ReusesProvidedResultListAndCopiesCrashBoxes()
    {
        var source = CreateObject();
        var result = new List<_3dObject>
        {
            CreateObject()
        };

        OmegaObjectHelpers.DeepCopy3dObjects(new List<_3dObject> { source }, result);

        Assert.AreEqual(1, result.Count);
        var copy = result[0];

        Assert.AreNotSame(source, copy);
        Assert.AreNotSame(source.CrashBoxes, copy.CrashBoxes);
        Assert.AreNotSame(source.CrashBoxes[0], copy.CrashBoxes[0]);
        Assert.AreNotSame(source.CrashBoxes[0][0], copy.CrashBoxes[0][0]);
        Assert.AreEqual(source.UseSurfaceFootprintPivot, copy.UseSurfaceFootprintPivot);
        Assert.AreNotSame(source.CalculatedCrashOffset, copy.CalculatedCrashOffset);
        Assert.AreNotSame(source.ShadowOffset, copy.ShadowOffset);

        copy.CrashBoxes[0][0].x = 999f;
        copy.CalculatedCrashOffset!.x = 999f;
        copy.ShadowOffset!.x = 999f;

        Assert.AreEqual(1f, source.CrashBoxes[0][0].x);
        Assert.AreEqual(7f, source.CalculatedCrashOffset!.x);
        Assert.AreEqual(10f, source.ShadowOffset!.x);
    }

    [TestMethod]
    public void DeepCopySingleObject_DoesNotMaterializeMissingTriangleVectors()
    {
        var source = CreateObject();
        var triangle = (TriangleMeshWithColor)source.ObjectParts[0].Triangles[0];
        triangle.normal2 = null!;
        triangle.normal3 = null!;

        var copy = (_3dObject)OmegaObjectHelpers.DeepCopySingleObject(source);
        var copiedTriangle = (TriangleMeshWithColor)copy.ObjectParts[0].Triangles[0];

        Assert.IsNull(copiedTriangle.Normal2Raw);
        Assert.IsNull(copiedTriangle.Normal3Raw);
        Assert.IsNotNull(copiedTriangle.Vert1Raw);
        Assert.AreNotSame(triangle.Vert1Raw, copiedTriangle.Vert1Raw);
    }

    private static _3dObject CreateObject()
    {
        return new _3dObject
        {
            ObjectId = 7,
            ObjectName = "DeepCopyTarget",
            UseSurfaceFootprintPivot = true,
            ObjectOffsets = new Vector3(10f, 20f, 30f),
            Rotation = new Vector3(1f, 2f, 3f),
            WorldPosition = new Vector3(4f, 5f, 6f),
            CalculatedCrashOffset = new Vector3(7f, 8f, 9f),
            ShadowOffset = new Vector3(10f, 11f, 12f),
            CrashBoxes = new List<List<IVector3>>
            {
                new()
                {
                    new Vector3(1f, 2f, 3f),
                    new Vector3(4f, 5f, 6f)
                }
            },
            ObjectParts = new List<I3dObjectPart>
            {
                new _3dObjectPart
                {
                    PartName = "Main",
                    IsVisible = true,
                    Triangles = new List<ITriangleMeshWithColor>
                    {
                        new TriangleMeshWithColor
                        {
                            Color = "ffffff",
                            landBasedPosition = 42,
                            angle = 0.75f,
                            noHidden = true,
                            vert1 = new Vector3(1f, 2f, 3f),
                            vert2 = new Vector3(4f, 5f, 6f),
                            vert3 = new Vector3(7f, 8f, 9f),
                            normal1 = new Vector3(0f, 0f, 1f),
                            normal2 = new Vector3(0f, 1f, 0f),
                            normal3 = new Vector3(1f, 0f, 0f)
                        }
                    }
                }
            }
        };
    }
}
