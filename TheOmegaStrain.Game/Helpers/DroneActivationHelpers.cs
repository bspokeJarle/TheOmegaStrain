using System;
using System.Collections.Generic;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Controls.KamikazeDroneControls;

namespace TheOmegaStrain.Game.Helpers;

public static class DroneActivationHelpers
{
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

        // Inactive drones may outlive the delays assigned during scene setup. Restart the
        // countdown on activation and use the entire configured range to prevent crowding.
        var startTime = activationTime ?? DateTime.Now;
        float delayRange = GameSetup.KamikazeDroneMaxHuntDelay - GameSetup.KamikazeDroneMinHuntDelay;
        float delaySlot = delayRange / drones.Count;

        for (int i = 0; i < drones.Count; i++)
        {
            var drone = drones[i];
            drone.IsActive = true;

            if (drone.Movement is KamikazeDroneControls controls)
            {
                double delaySeconds = GameSetup.KamikazeDroneMinHuntDelay +
                    ((i + random.NextDouble()) * delaySlot);
                controls.StartHuntDateTime = startTime.AddSeconds(delaySeconds);
            }
        }

        return drones.Count;
    }
}
