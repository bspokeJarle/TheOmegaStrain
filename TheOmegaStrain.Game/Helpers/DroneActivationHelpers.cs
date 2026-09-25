using System;
using System.Collections.Generic;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Controls.KamikazeDroneControls;

namespace TheOmegaStrain.Game.Helpers;

public static class DroneActivationHelpers
{
    private const double HuntDelayJitterSeconds = 0.5d;
    private const double AdditionalHuntSpacingSeconds = 20d;

    public static int ActivateWithStaggeredHuntDelays(
        IReadOnlyList<OmegaObject3D> aiObjects,
        Random? random = null,
        DateTime? activationTime = null)
    {
        random ??= new Random();
        var drones = new List<OmegaObject3D>();

        for (int i = 0; i < aiObjects.Count; i++)
        {
            var candidate = aiObjects[i];
            if (candidate.ObjectName == "KamikazeDrone" && !candidate.IsActive)
                drones.Add(candidate);
        }

        if (drones.Count == 0)
            return 0;

        // Inactive drones may outlive their constructor delays. Restart the countdown
        // on activation and spread launch times without predicting where Ship will be.
        var startTime = activationTime ?? DateTime.Now;
        double delaySeconds = GameSetup.KamikazeDroneMinHuntDelay +
            random.NextDouble() * HuntDelayJitterSeconds;

        for (int i = 0; i < drones.Count; i++)
        {
            if (i > 0)
                delaySeconds += GameSetup.KamikazeDroneMinimumHuntSpacingSeconds +
                    random.NextDouble() * AdditionalHuntSpacingSeconds;

            var drone = drones[i];
            drone.IsActive = true;

            if (drone.Movement is KamikazeDroneControls controls)
            {
                controls.ScheduleHunt(startTime.AddSeconds(delaySeconds), honorDelay: drones.Count > 1);
            }
        }

        return drones.Count;
    }
}
