using TheOmegaStrain.Domain;
using RetroMesh.Engine;

namespace TheOmegaStrain.Tests.Physics;

[TestClass]
public class CollisionBoxMathTests
{
    [TestMethod]
    public void CheckAabbOverlap_UsesConfiguredMargins()
    {
        var boxA = Box(-10f, -10f, -10f, 10f, 10f, 10f);
        var boxB = Box(11f, -10f, -10f, 30f, 10f, 10f);

        Assert.IsFalse(CollisionBoxMath.CheckAabbOverlap(
            boxA,
            boxB,
            marginX: 0f,
            marginY: 0f,
            marginZ: 0f,
            out _,
            out _));

        Assert.IsTrue(CollisionBoxMath.CheckAabbOverlap(
            boxA,
            boxB,
            marginX: 1f,
            marginY: 0f,
            marginZ: 0f,
            out _,
            out _));
    }

    [TestMethod]
    public void ContainsPoint_ReturnsBoundsAndPointStatus()
    {
        var box = Box(-10f, 100f, -20f, 10f, 300f, 20f);

        Assert.IsTrue(CollisionBoxMath.ContainsPoint(
            box,
            new Vector3 { x = 0f, y = 150f, z = 0f },
            out var bounds));

        Assert.AreEqual(-10f, bounds.MinX);
        Assert.AreEqual(300f, bounds.MaxY);

        Assert.IsFalse(CollisionBoxMath.ContainsPoint(
            box,
            new Vector3 { x = 0f, y = 301f, z = 0f },
            out _));
    }

    [TestMethod]
    public void GetCenter_ReturnsAabbCenter()
    {
        var center = CollisionBoxMath.GetCenter(new AabbBounds(-10f, 30f, 100f, 300f, -20f, 20f));

        Assert.AreEqual(10f, center.x);
        Assert.AreEqual(200f, center.y);
        Assert.AreEqual(0f, center.z);
    }

    private static List<IVector3> Box(float minX, float minY, float minZ, float maxX, float maxY, float maxZ)
    {
        return new List<IVector3>
        {
            new Vector3 { x = minX, y = maxY, z = minZ },
            new Vector3 { x = maxX, y = maxY, z = minZ },
            new Vector3 { x = maxX, y = minY, z = minZ },
            new Vector3 { x = minX, y = minY, z = minZ },
            new Vector3 { x = minX, y = maxY, z = maxZ },
            new Vector3 { x = maxX, y = maxY, z = maxZ },
            new Vector3 { x = maxX, y = minY, z = maxZ },
            new Vector3 { x = minX, y = minY, z = maxZ }
        };
    }
}
