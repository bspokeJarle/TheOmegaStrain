using _3dRotations.Scene.Scene1;
using _3dRotations.Scene.Scene3;
using _3dRotations.Scene.Scene4;
using _3dRotations.Scene.Scene5;
using _3dRotations.Scene.Scene6;
using _3dRotations.Scene.Scene7;
using _3dRotations.Scene.Scene8;
using _3dRotations.Scenes.Intro;
using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;
using Domain;
using System.Globalization;

namespace _3DSpesificsUnitTests.SceneManagement;

[TestClass]
public class SceneInfectionTuningTests
{
    private const int SeederBurstRingCap = 49;

    [TestInitialize]
    public void Setup()
    {
        GameState.ScreenOverlayState = new ScreenOverlayState();
    }

    [TestMethod]
    public void GameScenes_UseTargetedInfectionSpreadRates()
    {
        (IScene Scene, int TargetMinutes, int ExpectedRate)[] tuning =
        [
            (new Scene1(), 25, 4),
            (new Scene2(), 25, 4),
            (new Scene3(), 20, 5),
            (new Scene4(), 17, 6),
            (new Scene5(), 15, 7),
            (new Scene6(), 13, 7),
            (new Scene7(), 11, 8),
            (new Scene8(), 10, 9)
        ];

        foreach (var (scene, _, expectedRate) in tuning)
        {
            Assert.AreEqual(expectedRate, scene.InfectionSpreadRate, $"{scene.GetType().Name} infection tuning changed.");
            Assert.IsTrue(scene.InfectionSpreadRate < SeederBurstRingCap,
                $"{scene.GetType().Name} should stay below the seeder burst ring cap so rate changes remain meaningful.");
        }
    }

    [TestMethod]
    public void GameScenes_UseEnemyWeightedInfectionLimits()
    {
        (IScene Scene, float ExpectedLimit)[] limits =
        [
            (new Scene1(), 14.0f),
            (new Scene2(), 13.5f),
            (new Scene3(), 13.0f),
            (new Scene4(), 12.5f),
            (new Scene5(), 12.0f),
            (new Scene6(), 15.0f),
            (new Scene7(), 14.0f),
            (new Scene8(), 13.0f)
        ];

        foreach (var (scene, expectedLimit) in limits)
        {
            Assert.AreEqual(expectedLimit, scene.InfectionThresholdPercent, 0.0001f,
                $"{scene.GetType().Name} infection limit changed.");
        }
    }

    [TestMethod]
    public void GameSceneIntroText_MatchesCurrentInfectionSettings()
    {
        (IScene Scene, string EnemyText, string ToleranceText, string DelayText)[] scenes =
        [
            (new Scene1(), "Seven units", "14.0%", "8 seconds"),
            (new Scene2(), "Ten seeders", "13.5%", "6 seconds"),
            (new Scene3(), "Twelve seeders", "13.0%", "4.5 seconds"),
            (new Scene4(), "Fifteen seeders", "12.5%", "3 seconds"),
            (new Scene5(), "Eighteen seeders", "12.0%", "2 seconds"),
            (new Scene6(), "Twenty-one seeders", "15.0%", "1.8 seconds"),
            (new Scene7(), "Twenty-three seeders", "14.0%", "1.5 seconds"),
            (new Scene8(), "Twenty-five seeders", "13.0%", "1.2 seconds")
        ];

        foreach (var (scene, enemyText, toleranceText, delayText) in scenes)
        {
            scene.SetupSceneOverlay();
            var body = GameState.ScreenOverlayState.Body;

            Assert.IsTrue(body.Contains(enemyText, StringComparison.Ordinal),
                $"{scene.GetType().Name} briefing should mention current enemy count text.");
            Assert.IsTrue(body.Contains(toleranceText, StringComparison.Ordinal),
                $"{scene.GetType().Name} briefing should mention tolerance {Format(scene.InfectionThresholdPercent)}.");
            Assert.IsTrue(body.Contains(delayText, StringComparison.Ordinal),
                $"{scene.GetType().Name} briefing should mention spread delay {Format(scene.LocalInfectionSpreadDelaySec)} seconds.");
        }
    }

    [TestMethod]
    public void GameplayTips_EmphasizeKillingSeedersToControlInfection()
    {
        var intro = new Intro();

        intro.SetupSceneOverlay();

        var gameplayTips = GameState.ScreenOverlayState.Pages.Single(page => page[1] == "TACTICAL TIPS")[2];
        Assert.IsTrue(gameplayTips.Contains("Destroy Seeders fast", StringComparison.Ordinal));
        Assert.IsTrue(gameplayTips.Contains("Every Seeder kill helps slow the infection cascade", StringComparison.Ordinal));
    }

    [TestMethod]
    public void IntroControls_DescribeKeyboardMouseXboxAndMappingSupport()
    {
        var intro = new Intro();

        intro.SetupSceneOverlay();

        var controls = GameState.ScreenOverlayState.Pages.Single(page => page[1] == "FLIGHT CONTROLS")[2];
        Assert.IsTrue(controls.Contains("[SPACE]       THRUST", StringComparison.Ordinal));
        Assert.IsTrue(controls.Contains("[RIGHT SHIFT] FIRE CURRENT WEAPON", StringComparison.Ordinal));
        Assert.IsTrue(controls.Contains("[1] BULLET", StringComparison.Ordinal));
        Assert.IsTrue(controls.Contains("[2] DECOY", StringComparison.Ordinal));
        Assert.IsTrue(controls.Contains("[3] LAZER", StringComparison.Ordinal));
        Assert.IsTrue(controls.Contains("LEFT BUTTON   FIRE CURRENT WEAPON", StringComparison.Ordinal));
        Assert.IsTrue(controls.Contains("RIGHT BUTTON  THRUST", StringComparison.Ordinal));
        Assert.IsTrue(controls.Contains("LEFT STICK    PITCH / TURN", StringComparison.Ordinal));
        Assert.IsTrue(controls.Contains("[RT]          THRUST", StringComparison.Ordinal));
        Assert.IsTrue(controls.Contains("[LT]          FIRE CURRENT WEAPON", StringComparison.Ordinal));
        Assert.IsTrue(controls.Contains("[X]/[Y]/[B]   1 BULLET / 2 DECOY / 3 LAZER", StringComparison.Ordinal));
        Assert.IsTrue(controls.Contains("[A]           POWERUP 4 RESERVED", StringComparison.Ordinal));
        Assert.IsTrue(controls.Contains("Input type and mappings can be changed in CONTROLS.", StringComparison.Ordinal));
    }

    private static string Format(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
