using Domain;

namespace _3DSpesificsUnitTests.CommonGlobalState;

[TestClass]
public class GamePlayStatePlanetStartSnapshotTests
{
    [TestMethod]
    public void ApplyPlanetStartSnapshot_RestoresPlanetStartProgressAndClearsCheckpoint()
    {
        var gameplay = new GamePlayState
        {
            SceneIndex = 6,
            Score = 5000,
            Lives = 2,
            Health = 72f,
            PowerUpsCollected = 1,
            SeedersRemaining = 24,
            DronesRemaining = 7,
            MotherShipsRemaining = 1,
            TotalShotsFired = 120,
            TotalKills = 18,
            TotalDeaths = 3,
            InfectionLevel = 12f,
            WaveNumber = 4,
            InitialSeeders = 24,
            InitialDrones = 7,
            InitialMotherShips = 1,
            PlanetStyleBonusScore = 300,
            PlanetStyleBonusSceneIndex = 6
        };

        gameplay.SavePlanetStartSnapshot();

        gameplay.Score = 9000;
        gameplay.Lives = 1;
        gameplay.Health = 18f;
        gameplay.PowerUpsCollected = 2;
        gameplay.SeedersRemaining = 4;
        gameplay.DronesRemaining = 1;
        gameplay.MotherShipsRemaining = 0;
        gameplay.TotalShotsFired = 220;
        gameplay.TotalKills = 30;
        gameplay.TotalDeaths = 4;
        gameplay.InfectionLevel = 95f;
        gameplay.WaveNumber = 6;
        gameplay.PlanetStyleBonusScore = 1000;
        gameplay.SaveCheckpoint();

        gameplay.ApplyPlanetStartSnapshot();

        Assert.IsTrue(gameplay.HasPlanetStartSnapshot);
        Assert.IsFalse(gameplay.HasCheckpoint);
        Assert.AreEqual(6, gameplay.PlanetStartSceneIndex);
        Assert.AreEqual(5000L, gameplay.Score);
        Assert.AreEqual(2, gameplay.Lives);
        Assert.AreEqual(72f, gameplay.Health);
        Assert.AreEqual(1, gameplay.PowerUpsCollected);
        Assert.AreEqual(24, gameplay.SeedersRemaining);
        Assert.AreEqual(7, gameplay.DronesRemaining);
        Assert.AreEqual(1, gameplay.MotherShipsRemaining);
        Assert.AreEqual(120, gameplay.TotalShotsFired);
        Assert.AreEqual(18, gameplay.TotalKills);
        Assert.AreEqual(3, gameplay.TotalDeaths);
        Assert.AreEqual(12f, gameplay.InfectionLevel);
        Assert.AreEqual(4, gameplay.WaveNumber);
        Assert.AreEqual(300, gameplay.PlanetStyleBonusScore);
        Assert.AreEqual(6, gameplay.PlanetStyleBonusSceneIndex);
    }
}
