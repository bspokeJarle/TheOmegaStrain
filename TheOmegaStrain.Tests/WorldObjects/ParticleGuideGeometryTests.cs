using TheOmegaStrain.Domain;
using TheOmegaStrain.Game.World.Objects;

namespace TheOmegaStrain.Tests.WorldObjects;

[TestClass]
public class ParticleGuideGeometryTests
{
    [TestMethod]
    public void ExhaustGuides_StartBehindObjectAndPointAwayFromHull()
    {
        AssertExhaustGuide(
            KamikazeDrone.ParticlesStartGuide()![0],
            KamikazeDrone.ParticlesDirectionGuide()![0],
            rearMostX: -47f,
            minimumClearance: 7f,
            "KamikazeDrone");

        AssertExhaustGuide(
            DecoyBeacon.ParticlesStartGuide()![0],
            DecoyBeacon.ParticlesDirectionGuide()![0],
            rearMostX: -10f,
            minimumClearance: 7f,
            "DecoyBeacon");

        AssertExhaustGuide(
            ZeppelinBomber.ParticlesStartGuide()![0],
            ZeppelinBomber.ParticlesDirectionGuide()![0],
            rearMostX: -51.2f,
            minimumClearance: 6f,
            "ZeppelinBomber");

        AssertExhaustGuide(
            SpaceSwan.ParticlesStartGuide()![0],
            SpaceSwan.ParticlesDirectionGuide()![0],
            rearMostX: -40f,
            minimumClearance: 4f,
            "SpaceSwan");
    }

    [TestMethod]
    public void RocketExhaustGuide_StartsClearOfNozzleAndPointsAwayFromHull()
    {
        var rocket = Rocket.CreateRocket(parentSurface: null!);
        Assert.IsTrue(rocket.ObjectParts.Where(p => p.PartName.Contains("Guide")).All(p => !p.IsVisible));
        var start = rocket.ObjectParts.Single(p => p.PartName == "RocketParticlesStartGuide").Triangles[0];
        var guide = rocket.ObjectParts.Single(p => p.PartName == "RocketParticlesDirectionGuide").Triangles[0];

        AssertExhaustGuide(start, guide, rearMostX: -10f * 3.5f, minimumClearance: 2.5f * 3.5f, "Rocket");
        Assert.AreEqual(-13f * 3.5f, Centroid(start).x, 0.001f);
        Assert.AreEqual(-15.5f * 3.5f, Centroid(guide).x, 0.001f);
    }

    [TestMethod]
    public void Rocket_ScalesHullAndCrashBoxByThreePointFive()
    {
        var rocket = Rocket.CreateRocket(null!);
        var hull = rocket.ObjectParts.Where(part => part.IsVisible).SelectMany(part => part.Triangles)
            .SelectMany(triangle => new[] { triangle.vert1, triangle.vert2, triangle.vert3 }).ToList();
        var bounds = AabbBounds.FromPoints(hull);
        Assert.AreEqual(-35f, bounds.MinX, 0.001f);
        Assert.AreEqual(38.5f, bounds.MaxX, 0.001f);
        Assert.AreEqual(73.5f, bounds.MaxX - bounds.MinX, 0.001f);
        Assert.AreEqual(new AabbBounds(-9.5f * 3.5f, 11f * 3.5f, -1.4f * 3.5f, 1.4f * 3.5f, -1.4f * 3.5f, 1.4f * 3.5f),
            AabbBounds.FromPoints(rocket.CrashBoxes.Single()));
    }

    private static void AssertExhaustGuide(
        ITriangleMeshWithColorAndTexture startGuide,
        ITriangleMeshWithColorAndTexture directionGuide,
        float rearMostX,
        float minimumClearance,
        string objectName)
    {
        var start = Centroid(startGuide);
        var guide = Centroid(directionGuide);

        Assert.IsTrue(start.x <= rearMostX - minimumClearance,
            $"{objectName} particle start x={start.x:F1}; expected outside rear x={rearMostX:F1} with at least {minimumClearance:F1} units clearance.");
        Assert.IsTrue(guide.x < start.x,
            $"{objectName} particle direction guide x={guide.x:F1}; expected behind particle start x={start.x:F1}.");
        Assert.IsTrue(startGuide.noHidden == true && directionGuide.noHidden == true,
            $"{objectName} particle guide triangles should remain two-sided guide geometry.");
    }

    private static Vector3 Centroid(ITriangleMeshWithColorAndTexture tri) => new()
    {
        x = (tri.vert1.x + tri.vert2.x + tri.vert3.x) / 3f,
        y = (tri.vert1.y + tri.vert2.y + tri.vert3.y) / 3f,
        z = (tri.vert1.z + tri.vert2.z + tri.vert3.z) / 3f,
    };
}
