using _3dTesting.MainWindowClasses;
using Domain;

namespace _3DSpesificsUnitTests.MapAndOverlay;

[TestClass]
public class HudInfectionMeterTests
{
    [TestMethod]
    public void CalculateInfectionMeterFill_UsesSceneCriticalMassAsFullScale()
    {
        var gameplay = new GamePlayState
        {
            TotalBioTiles = 1000,
            InfectionCriticalMass = 2f,
            InfectionLevel = 10f
        };

        Assert.AreEqual(1f, gameplay.InfectionPercent);
        Assert.AreEqual(0.5f, HudOverlayHandlerV2.CalculateInfectionMeterFill(gameplay), 0.0001f);

        gameplay.InfectionLevel = 20f;

        Assert.AreEqual(2f, gameplay.InfectionPercent);
        Assert.AreEqual(1f, HudOverlayHandlerV2.CalculateInfectionMeterFill(gameplay), 0.0001f);
        Assert.IsTrue(gameplay.IsInfectionCritical);
    }

    [TestMethod]
    public void CalculateInfectionMeterFill_ClampsAboveCriticalMass()
    {
        var gameplay = new GamePlayState
        {
            TotalBioTiles = 1000,
            InfectionCriticalMass = 2f,
            InfectionLevel = 40f
        };

        Assert.AreEqual(1f, HudOverlayHandlerV2.CalculateInfectionMeterFill(gameplay), 0.0001f);
    }

    [TestMethod]
    public void IsBiomassCriticalWarning_TriggersAtEightyPercentOfCriticalMass()
    {
        var gameplay = new GamePlayState
        {
            TotalBioTiles = 1000,
            InfectionCriticalMass = 5f,
            InfectionLevel = 39f
        };

        Assert.AreEqual(0.78f, gameplay.InfectionCriticalProgress, 0.0001f);
        Assert.IsFalse(gameplay.IsBiomassCriticalWarning);

        gameplay.InfectionLevel = 40f;

        Assert.AreEqual(GamePlayState.BiomassCriticalWarningRatio, gameplay.InfectionCriticalProgress, 0.0001f);
        Assert.IsTrue(gameplay.IsBiomassCriticalWarning);
    }

    [TestMethod]
    public void IsBiomassAbortWarning_TriggersAtNinetySixPercentOfCriticalMass()
    {
        var gameplay = new GamePlayState
        {
            TotalBioTiles = 1000,
            InfectionCriticalMass = 5f,
            InfectionLevel = 47f
        };

        Assert.AreEqual(0.94f, gameplay.InfectionCriticalProgress, 0.0001f);
        Assert.IsFalse(gameplay.IsBiomassAbortWarning);

        gameplay.InfectionLevel = 48f;

        Assert.AreEqual(GamePlayState.BiomassAbortWarningRatio, gameplay.InfectionCriticalProgress, 0.0001f);
        Assert.IsTrue(gameplay.IsBiomassAbortWarning);
    }

    [TestMethod]
    public void BiomassAbortWarningThreshold_IsAfterCriticalWarningAndBeforeReset()
    {
        Assert.IsTrue(GamePlayState.BiomassAbortWarningRatio > GamePlayState.BiomassCriticalWarningRatio);
        Assert.IsTrue(GamePlayState.BiomassAbortWarningRatio < 1f);
    }
}
