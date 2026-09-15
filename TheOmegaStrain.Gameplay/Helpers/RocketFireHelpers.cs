using System;

namespace TheOmegaStrain.Gameplay.Helpers;

public static class RocketFireHelpers
{
    public const float MinimumCooldownSeconds = 10f;

    // Runtime reload gate. AttackShipControls also checks launch range and guide readiness.
    // Visibility comes from the render/AI system; a failed launch never starts the timer.
    public static bool CanFireAfterReload(
        bool isAttackShipVisible,
        float secondsSinceRocketRemoved,
        float reloadDelaySeconds,
        int activeRocketCount)
    {
        return isAttackShipVisible && activeRocketCount == 0 &&
               secondsSinceRocketRemoved >= MathF.Max(0f, reloadDelaySeconds);
    }

    // Keep the original range/launch-cooldown helper available for workshop exercises.
    public static bool CanFire(
        bool isAttackShipVisible,
        float distanceToShip,
        float maxRange,
        float secondsSinceSuccessfulLaunch,
        float configuredCooldownSeconds,
        int activeRocketCount)
    {
        float requiredCooldown = MathF.Max(MinimumCooldownSeconds, configuredCooldownSeconds);

        return isAttackShipVisible &&
               maxRange > 0f &&
               distanceToShip >= 0f &&
               distanceToShip <= maxRange &&
               secondsSinceSuccessfulLaunch >= requiredCooldown &&
               activeRocketCount == 0;
    }
}
