using System;
using System.Windows.Input;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Domain;

namespace TheOmegaStrain.Wpf.Input;

public static class MenuSceneSaveShortcut
{
    public static bool TryHandle(Key key, bool cIsHeld, bool isRepeat,
        ScreenOverlayState? overlay, SceneTypes sceneType, Action<int> saveScene)
    {
        // Same main-menu boundary as XboxQuitHoldAvailability. Never intercept
        // gameplay, pause/settings menus, briefing pages or callsign entry.
        if (sceneType != SceneTypes.Intro || overlay is not
            { ShowOverlay: true, Type: ScreenOverlayType.Intro, CurrentPage: 0,
              ChoiceAction: ScreenOverlayChoiceAction.IntroMainMenu })
            return false;

        // Reserve C here so holding it cannot open another overlay first.
        if (key == Key.C) return true;
        if (!cIsHeld) return false;
        int index = key >= Key.D1 && key <= Key.D9 ? key - Key.D0 :
            key >= Key.NumPad1 && key <= Key.NumPad9 ? key - Key.NumPad0 : 0;
        if (index == 0) return false;
        if (!isRepeat) saveScene(index);
        return true;
    }
}
