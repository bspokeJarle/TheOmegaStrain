using System.Globalization;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Common.Localization;

namespace TheOmegaStrain.Game.Helpers;

internal static class SceneBriefingText
{
    public static void Apply(
        ScreenOverlayState overlay,
        int sceneNumber,
        string language,
        int seeders,
        int drones,
        int bombers,
        int attackShips,
        float infectionThresholdPercent,
        float spreadDelaySeconds)
    {
        string headerKey = sceneNumber switch
        {
            1 => "briefing.bootHeader",
            8 => "briefing.finalHeader",
            _ => "briefing.sectorHeader"
        };
        overlay.Header = GameText.Get(headerKey, language);
        overlay.Title = GameText.Get($"briefing.scene{sceneNumber}.title", language);
        overlay.Body = GameText.Format($"briefing.scene{sceneNumber}.body", language,
            ("seeders", seeders.ToString(CultureInfo.InvariantCulture)),
            ("drones", drones.ToString(CultureInfo.InvariantCulture)),
            ("bombers", bombers.ToString(CultureInfo.InvariantCulture)),
            ("attackShips", attackShips.ToString(CultureInfo.InvariantCulture)),
            ("threshold", infectionThresholdPercent.ToString("0.0", CultureInfo.InvariantCulture)),
            ("delay", spreadDelaySeconds.ToString("0.#", CultureInfo.InvariantCulture)));
        overlay.Footer = GameText.Get(sceneNumber == 1 ? "briefing.bootFooter" : "briefing.descendFooter", language);
    }
}
