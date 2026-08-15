using CommonUtilities.CommonSetup;
using CommonUtilities.GamePlayHelpers;
using Domain;

namespace _3DSpesificsUnitTests.Scoring;

[TestClass]
public sealed class PlanetRewardCalculatorTests
{
    [TestMethod]
    public void Calculate_ReturnsExpectedPlanetCompletionRewards()
    {
        var gameplay = new GamePlayState
        {
            SceneIndex = 6,
            Health = 50,
            MaxHealth = 100,
            Lives = 2,
            TotalBioTiles = 1000,
            InfectionLevel = 125,
            TotalShotsFired = 10,
            TotalKills = 5,
            HasPlanetStartSnapshot = true,
            PlanetStartTotalDeaths = 1,
            TotalDeaths = 1,
            InitialMotherShips = 1,
            MotherShipsRemaining = 0,
            PlanetStyleBonusScore = 600
        };

        var rewards = PlanetRewardCalculator.Calculate(gameplay);

        Assert.AreEqual(6, rewards.SceneIndex);
        Assert.AreEqual(7, rewards.Lines.Count);
        Assert.AreEqual(880, GetLine(rewards, "BIOMASS CONTAINED"));
        Assert.AreEqual(400, GetLine(rewards, "HULL INTEGRITY"));
        Assert.AreEqual(1000, GetLine(rewards, "LIVES PRESERVED"));
        Assert.AreEqual(GameSetup.PlanetPrecisionBonusTier2, GetLine(rewards, "PRECISION BONUS"));
        Assert.AreEqual(600, GetLine(rewards, "STYLE FLYING"));
        Assert.AreEqual(GameSetup.PlanetDeathlessBonus, GetLine(rewards, "CLEAN OPERATION"));
        Assert.AreEqual(1500, GetLine(rewards, "MOTHERSHIP TAKEDOWN"));
        Assert.AreEqual(5630, rewards.TotalPoints);
    }

    [TestMethod]
    public void BuildOverlayBody_CountsFromZeroToTotal()
    {
        var rewards = new PlanetRewardBreakdown(
            sceneIndex: 1,
            new[]
            {
                new PlanetRewardLine("BIOMASS CONTAINED", 1000),
                new PlanetRewardLine("HULL INTEGRITY", 800)
            });

        StringAssert.Contains(rewards.BuildOverlayBody(0f), "TOTAL BONUS");
        StringAssert.Contains(rewards.BuildOverlayBody(0f), "+    0");
        StringAssert.Contains(rewards.BuildOverlayBody(1f), "TOTAL BONUS");
        StringAssert.Contains(rewards.BuildOverlayBody(1f), "+ 1800");
    }

    [TestMethod]
    public void Calculate_FiltersZeroPointRows()
    {
        var gameplay = new GamePlayState
        {
            SceneIndex = 1,
            Health = 0,
            MaxHealth = 100,
            Lives = 0,
            TotalBioTiles = 100,
            InfectionLevel = 100,
            TotalShotsFired = 10,
            TotalKills = 0,
            HasPlanetStartSnapshot = false,
            InitialMotherShips = 0,
            MotherShipsRemaining = 0
        };

        var rewards = PlanetRewardCalculator.Calculate(gameplay);

        Assert.AreEqual(0, rewards.Lines.Count);
        Assert.AreEqual(0, rewards.TotalPoints);
    }

    private static int GetLine(PlanetRewardBreakdown rewards, string label)
    {
        return rewards.Lines.Single(line => line.Label == label).Points;
    }
}
