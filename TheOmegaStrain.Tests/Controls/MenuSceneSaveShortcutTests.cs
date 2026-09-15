using System.Windows.Input;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Wpf.Input;

namespace TheOmegaStrain.Tests.Controls;

[TestClass]
public class MenuSceneSaveShortcutTests
{
    private static ScreenOverlayState MainMenu() => new()
    {
        ShowOverlay = true,
        Type = ScreenOverlayType.Intro,
        CurrentPage = 0,
        ChoiceAction = ScreenOverlayChoiceAction.IntroMainMenu
    };

    [TestMethod]
    public void HeldC_MapsBothDigitRowsToScenesOneThroughNine()
    {
        for (int i = 1; i <= 9; i++)
        {
            foreach (var key in new[] { Key.D0 + i, Key.NumPad0 + i })
            {
                int savedScene = 0;
                Assert.IsTrue(MenuSceneSaveShortcut.TryHandle(key, true, false, MainMenu(),
                    SceneTypes.Intro, scene => savedScene = scene));
                Assert.AreEqual(i, savedScene);
            }
        }
    }

    [TestMethod]
    public void CIsReservedButNeverSavesWithoutANumber_AndDigitsRequireHeldC()
    {
        int saves = 0;
        Assert.IsTrue(MenuSceneSaveShortcut.TryHandle(Key.C, true, false, MainMenu(), SceneTypes.Intro, _ => saves++));
        Assert.IsFalse(MenuSceneSaveShortcut.TryHandle(Key.D6, false, false, MainMenu(), SceneTypes.Intro, _ => saves++));
        Assert.IsFalse(MenuSceneSaveShortcut.TryHandle(Key.D0, true, false, MainMenu(), SceneTypes.Intro, _ => saves++));
        Assert.IsFalse(MenuSceneSaveShortcut.TryHandle(Key.A, true, false, MainMenu(), SceneTypes.Intro, _ => saves++));
        Assert.AreEqual(0, saves);
    }

    [TestMethod]
    public void RepeatedKeyDown_IsConsumedWithoutRepeatingTheSave()
    {
        int saves = 0;
        Assert.IsTrue(MenuSceneSaveShortcut.TryHandle(Key.D6, true, false, MainMenu(), SceneTypes.Intro, _ => saves++));
        for (int i = 0; i < 10; i++)
            Assert.IsTrue(MenuSceneSaveShortcut.TryHandle(Key.D6, true, true, MainMenu(), SceneTypes.Intro, _ => saves++));
        Assert.AreEqual(1, saves);
    }

    [DataTestMethod]
    [DataRow(SceneTypes.Game)]
    [DataRow(SceneTypes.Simulation)]
    [DataRow(SceneTypes.Tutorial)]
    [DataRow(SceneTypes.Outro)]
    public void NeverHandlesGameplayOrOtherScenes(SceneTypes sceneType)
    {
        int saves = 0;
        Assert.IsFalse(MenuSceneSaveShortcut.TryHandle(Key.C, true, false, MainMenu(), sceneType, _ => saves++));
        Assert.IsFalse(MenuSceneSaveShortcut.TryHandle(Key.D1, true, false, MainMenu(), sceneType, _ => saves++));
        Assert.AreEqual(0, saves);
    }

    [DataTestMethod]
    [DataRow("hidden")]
    [DataRow("settings")]
    [DataRow("name-entry")]
    [DataRow("info-page")]
    public void NeverHandlesOtherOverlays(string mode)
    {
        var overlay = MainMenu();
        switch (mode)
        {
            case "hidden": overlay.ShowOverlay = false; break;
            case "settings": overlay.Type = ScreenOverlayType.Settings; break;
            case "name-entry": overlay.ChoiceAction = ScreenOverlayChoiceAction.None; break;
            case "info-page": overlay.CurrentPage = 1; break;
        }
        Assert.IsFalse(MenuSceneSaveShortcut.TryHandle(Key.D6, true, false, overlay,
            SceneTypes.Intro, _ => Assert.Fail("Only the main menu may save scene selection.")));
    }
}
