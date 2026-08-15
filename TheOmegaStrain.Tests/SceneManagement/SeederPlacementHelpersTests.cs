using _3dRotations.Helpers;
using CommonUtilities.CommonSetup;
using static Domain._3dSpecificsImplementations;

namespace _3DSpesificsUnitTests.SceneManagement;

[TestClass]
public class SeederPlacementHelpersTests
{
    [TestMethod]
    public void CreateRingSeederPositions_KeepsSomeSeedersNearCenterAndSpreadsTheRest()
    {
        var center = new Vector3 { x = 95100f, y = 0, z = 95200f };

        var positions = SeederPlacementHelpers.CreateRingSeederPositions(
            count: 17,
            center: center,
            seed: 42,
            nearSeederCount: 5,
            firstRingRadius: 7500f,
            ringRadiusStep: 11500f);

        Assert.AreEqual(17, positions.Count);

        var distances = positions.Select(p => DistanceXZ(center, p)).ToList();
        Assert.IsTrue(distances.Take(5).All(d => d < 11000f * SurfaceSetup.WorldScale),
            "The first ring should keep several seeders close enough for immediate action.");
        Assert.IsTrue(distances.Skip(5).Any(d => d > 17000f * SurfaceSetup.WorldScale),
            "Outer ring seeders should be pushed further out instead of clustering near the platform.");
        Assert.IsTrue(positions.Select(p => $"{MathF.Round(p.x)}:{MathF.Round(p.z)}").Distinct().Count() == positions.Count,
            "Seeder positions should not collapse onto duplicate points.");
    }

    [TestMethod]
    public void CreateRingSeederPositions_KeepsSeedersAtLeastHalfAScreenApart()
    {
        var center = new Vector3 { x = 95100f, y = 0, z = 95200f };

        // Use the same defaults as Scene4/Scene7 (firstRingRadius 7500/8000) so the
        // default minSeederDistance kicks in.
        const float firstRingRadius = 7500f;
        float expectedMinDistance = firstRingRadius * 0.6f * SurfaceSetup.WorldScale;

        // Try a handful of seeds to exercise the angle/radius jitter in different ways.
        int[] seeds = { 11, 42, 1337, 4041, 7071 };
        foreach (int seed in seeds)
        {
            var positions = SeederPlacementHelpers.CreateRingSeederPositions(
                count: 23,
                center: center,
                seed: seed,
                nearSeederCount: 7,
                firstRingRadius: firstRingRadius,
                ringRadiusStep: 11500f);

            Assert.AreEqual(23, positions.Count, $"Seed {seed} should still place every requested seeder.");
            AssertAllSeparated(positions, positions, expectedMinDistance);
        }
    }

    [TestMethod]
    public void CreateRingSeederPositions_ExpandsDenseRingsInsteadOfAcceptingTooCloseFallbacks()
    {
        var center = new Vector3 { x = 95100f, y = 0, z = 95200f };
        const float minSeederDistance = 4200f;

        var positions = SeederPlacementHelpers.CreateRingSeederPositions(
            count: 8,
            center: center,
            seed: 9191,
            nearSeederCount: 8,
            firstRingRadius: 3000f,
            ringRadiusStep: 6000f,
            radiusJitter: 2800f,
            angleJitterDegrees: 45f,
            minSeederDistance: minSeederDistance);

        Assert.AreEqual(8, positions.Count);
        AssertAllSeparated(positions, positions, minSeederDistance * SurfaceSetup.WorldScale);
        Assert.IsTrue(positions.Any(p => DistanceXZ(center, p) > 6000f * SurfaceSetup.WorldScale),
            "Dense rings should expand outward instead of accepting a too-close final jitter attempt.");
    }

    [TestMethod]
    public void CreateRandomSeederPositions_UsesRequestedRadiusBand()
    {
        var center = new Vector3 { x = 95100f, y = 0, z = 95200f };

        var positions = SeederPlacementHelpers.CreateRandomSeederPositions(
            count: 4,
            center: center,
            seed: 43,
            minRadius: 16000f,
            maxRadius: 42000f);

        Assert.AreEqual(4, positions.Count);
        Assert.IsTrue(positions.All(p =>
        {
            var distance = DistanceXZ(center, p);
            return distance >= 16000f * SurfaceSetup.WorldScale &&
                   distance <= 42000f * SurfaceSetup.WorldScale;
        }));
    }

    [TestMethod]
    public void CreateRandomSeederPositions_AvoidsOccupiedSeederPositions()
    {
        var center = new Vector3 { x = 95100f, y = 0, z = 95200f };
        var regularPositions = SeederPlacementHelpers.CreateRingSeederPositions(
            count: 21,
            center: center,
            seed: 8081,
            nearSeederCount: 11,
            firstRingRadius: 7500f,
            ringRadiusStep: 11500f);

        var powerUpPositions = SeederPlacementHelpers.CreateRandomSeederPositions(
            count: 4,
            center: center,
            seed: 8082,
            minRadius: 16000f,
            maxRadius: 42000f,
            avoidPositions: regularPositions,
            minDistance: 4500f);

        Assert.AreEqual(4, powerUpPositions.Count);
        AssertAllSeparated(powerUpPositions, regularPositions, 4500f * SurfaceSetup.WorldScale);
        AssertAllSeparated(powerUpPositions, powerUpPositions, 4500f * SurfaceSetup.WorldScale);
    }

    [TestMethod]
    public void CreateCheckpointSeederPositions_UsesMiddleBandAwayFromStartAndEnd()
    {
        var center = new Vector3 { x = 95100f, y = 0, z = 95200f };
        var regularPositions = SeederPlacementHelpers.CreateRingSeederPositions(
            count: 17,
            center: center,
            seed: 6061,
            nearSeederCount: 5,
            firstRingRadius: 7500f,
            ringRadiusStep: 11500f);

        var checkpointPositions = SeederPlacementHelpers.CreateCheckpointSeederPositions(
            count: 4,
            center: center,
            seed: 6062,
            firstRingRadius: 7500f,
            ringRadiusStep: 11500f,
            avoidPositions: regularPositions,
            minDistance: 4500f);

        Assert.AreEqual(4, checkpointPositions.Count);
        Assert.IsTrue(checkpointPositions.All(p => DistanceXZ(center, p) > 11000f * SurfaceSetup.WorldScale),
            "Checkpoint seeders should not sit in the opening seeder ring.");
        Assert.IsTrue(checkpointPositions.All(p => DistanceXZ(center, p) < 26000f * SurfaceSetup.WorldScale),
            "Checkpoint seeders should not drift into the late outer cleanup rings.");
        AssertAllSeparated(checkpointPositions, regularPositions, 4500f * SurfaceSetup.WorldScale);
        AssertAllSeparated(checkpointPositions, checkpointPositions, 4500f * SurfaceSetup.WorldScale);
    }

    private static float DistanceXZ(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return MathF.Sqrt((dx * dx) + (dz * dz));
    }

    private static void AssertAllSeparated(
        IReadOnlyList<Vector3> positions,
        IReadOnlyList<Vector3> otherPositions,
        float minDistance)
    {
        for (int i = 0; i < positions.Count; i++)
        {
            for (int j = 0; j < otherPositions.Count; j++)
            {
                if (ReferenceEquals(positions, otherPositions) && i == j)
                    continue;

                Assert.IsTrue(
                    DistanceXZ(positions[i], otherPositions[j]) >= minDistance,
                    $"Expected seeder positions to stay at least {minDistance:0.##} apart.");
            }
        }
    }
}
