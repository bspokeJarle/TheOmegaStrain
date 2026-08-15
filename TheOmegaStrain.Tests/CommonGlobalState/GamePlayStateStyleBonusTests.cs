using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;

namespace TheOmegaStrain.Tests.CommonGlobalState;

[TestClass]
public class GamePlayStateStyleBonusTests
{
    private int _originalCap;
    private int _originalCleanLoopScore;
    private float _originalLowAltitudeRunMaxHeight;
    private float _originalLowAltitudeRunMinHeight;
    private float _originalLowAltitudeRunRequiredSeconds;

    [TestInitialize]
    public void Setup()
    {
        _originalCap = GameSetup.PlanetStyleBonusScoreCap;
        _originalCleanLoopScore = GameSetup.CleanLoopStyleBonusScore;
        _originalLowAltitudeRunMinHeight = GameSetup.LowAltitudeRunMinHeight;
        _originalLowAltitudeRunMaxHeight = GameSetup.LowAltitudeRunMaxHeight;
        _originalLowAltitudeRunRequiredSeconds = GameSetup.LowAltitudeRunRequiredSeconds;
    }

    [TestCleanup]
    public void Cleanup()
    {
        GameSetup.PlanetStyleBonusScoreCap = _originalCap;
        GameSetup.CleanLoopStyleBonusScore = _originalCleanLoopScore;
        GameSetup.LowAltitudeRunMinHeight = _originalLowAltitudeRunMinHeight;
        GameSetup.LowAltitudeRunMaxHeight = _originalLowAltitudeRunMaxHeight;
        GameSetup.LowAltitudeRunRequiredSeconds = _originalLowAltitudeRunRequiredSeconds;
    }

    [TestMethod]
    public void AwardStyleBonus_AccruesPlanetBudgetWithoutChangingScore()
    {
        GameSetup.PlanetStyleBonusScoreCap = 500;
        var gameplay = new GamePlayState { SceneIndex = 1 };

        int first = gameplay.AwardStyleBonus(300);
        int second = gameplay.AwardStyleBonus(300);

        Assert.AreEqual(300, first);
        Assert.AreEqual(200, second);
        Assert.AreEqual(0L, gameplay.Score);
        Assert.AreEqual(500, gameplay.PlanetStyleBonusScore);
        Assert.AreEqual(0, gameplay.PlanetStyleBonusRemaining);
    }

    [TestMethod]
    public void AwardStyleBonus_WhenSceneChanges_StartsFreshPlanetBudget()
    {
        GameSetup.PlanetStyleBonusScoreCap = 500;
        var gameplay = new GamePlayState { SceneIndex = 1 };

        gameplay.AwardStyleBonus(500);
        gameplay.SceneIndex = 2;
        int awarded = gameplay.AwardStyleBonus(100);

        Assert.AreEqual(100, awarded);
        Assert.AreEqual(0L, gameplay.Score);
        Assert.AreEqual(100, gameplay.PlanetStyleBonusScore);
        Assert.AreEqual(2, gameplay.PlanetStyleBonusSceneIndex);
    }

    [TestMethod]
    public void RestoreCheckpoint_RestoresCheckpointStyleBonusBudget()
    {
        var gameplay = new GamePlayState { SceneIndex = 3, Score = 1200 };

        gameplay.AwardStyleBonus(400);
        gameplay.SaveCheckpoint();
        gameplay.Score = 1800;
        gameplay.AwardStyleBonus(250);

        gameplay.RestoreCheckpoint();

        Assert.AreEqual(1200L, gameplay.Score);
        Assert.AreEqual(400, gameplay.PlanetStyleBonusScore);
        Assert.AreEqual(3, gameplay.PlanetStyleBonusSceneIndex);
    }

    [TestMethod]
    public void LowAltitudeRun_DefaultTuningRequiresLowerAndLongerFlight()
    {
        Assert.AreEqual(20f, GameSetup.LowAltitudeRunMinHeight, 0.001f,
            "Very low flight should still count once the ship is airborne; landed state already blocks pad idling.");
        Assert.AreEqual(126f, GameSetup.LowAltitudeRunMaxHeight, 0.001f,
            "Low-altitude flying should require about 10% lower altitude than the old 140-unit window.");
        Assert.AreEqual(4.6f, GameSetup.LowAltitudeRunRequiredSeconds, 0.001f,
            "Low-altitude flying should require about 15% longer sustained flight than the old 4 second window.");
    }

    [TestMethod]
    public void LowAltitudeRunAttemptBonus_ResetForNewPlanetAttempt()
    {
        var gameplay = new GamePlayState { SceneIndex = 1 };

        Assert.IsTrue(gameplay.TryConsumeLowAltitudeRunAttemptBonus());
        Assert.IsFalse(gameplay.TryConsumeLowAltitudeRunAttemptBonus());

        gameplay.ResetForNewGame();

        Assert.IsFalse(gameplay.LowAltitudeRunAttemptBonusConsumed);
        Assert.IsTrue(gameplay.TryConsumeLowAltitudeRunAttemptBonus());

        gameplay.ConsumeLifeAndRespawn();

        Assert.IsFalse(gameplay.LowAltitudeRunAttemptBonusConsumed);
    }
}
