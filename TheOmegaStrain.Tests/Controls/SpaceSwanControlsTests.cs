using Domain;
using GameAiAndControls.Controls.SpaceSwanControls;
using System.Reflection;
using static Domain._3dSpecificsImplementations;

namespace _3DSpesificsUnitTests.Controls;

[TestClass]
public class SpaceSwanControlsTests
{
    [TestMethod]
    public void RotateAroundPivot_WithZeroAngle_DoesNotTranslateWingWhenRotationMutatesInput()
    {
        var controls = new SpaceSwanControls();
        var triangles = new List<ITriangleMeshWithColor>
        {
            new TriangleMeshWithColor
            {
                Color = "ffffff",
                vert1 = new Vector3 { x = 0f, y = 2f, z = 3f },
                vert2 = new Vector3 { x = 10f, y = 5f, z = 6f },
                vert3 = new Vector3 { x = 3f, y = 8f, z = 9f },
                normal1 = new Vector3 { x = 0f, y = 0f, z = 1f },
                normal2 = new Vector3 { x = 0f, y = 0f, z = 1f },
                normal3 = new Vector3 { x = 0f, y = 0f, z = 1f }
            }
        };

        var method = typeof(SpaceSwanControls).GetMethod(
            "RotateAroundPivot",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.IsNotNull(method);
        var result = (List<ITriangleMeshWithColor>)method.Invoke(
            controls,
            new object[] { triangles, 17f, -6f, 0f })!;

        AssertVertex(result[0].vert1, 0f, 2f, 3f);
        AssertVertex(result[0].vert2, 10f, 5f, 6f);
        AssertVertex(result[0].vert3, 3f, 8f, 9f);
    }

    private static void AssertVertex(IVector3 actual, float expectedX, float expectedY, float expectedZ)
    {
        Assert.AreEqual(expectedX, actual.x, 0.0001f);
        Assert.AreEqual(expectedY, actual.y, 0.0001f);
        Assert.AreEqual(expectedZ, actual.z, 0.0001f);
    }
}
