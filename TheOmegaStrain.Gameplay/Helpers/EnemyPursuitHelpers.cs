using TheOmegaStrain.Common.CommonGlobalState;

namespace TheOmegaStrain.Gameplay.Helpers;

/// <summary>Briefings and other visible overlays must not let enemies hunt an idle player.</summary>
public static class EnemyPursuitHelpers
{
    public static bool CanPursueShip => !GameState.ScreenOverlayState.ShowOverlay;
}
