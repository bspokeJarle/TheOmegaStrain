using System;
using System.Collections.Generic;
using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Domain;

namespace TheOmegaStrain.Game.Helpers;

public static class SpaceSwanPlacementHelpers
{
    // Space swans use the scenes' unscaled world coordinates before WorldScale is applied.
    public const float MinimumSpacing = 4500f;
    private const int PlacementAttempts = 128;

    public static Vector3 GetNextPosition(Random random, IReadOnlyList<I3dObject> sceneObjects, float worldScale)
    {
        Vector3 best = new();
        float bestNearestDistanceSquared = -1f;
        float minimumSquared = MinimumSpacing * worldScale * MinimumSpacing * worldScale;

        for (int attempt = 0; attempt < PlacementAttempts; attempt++)
        {
            var candidate = new Vector3
            {
                x = (95700 + random.Next(-40000, 40000)) * worldScale,
                y = 0,
                z = (92000 + random.Next(-40000, 40000)) * worldScale
            };
            float nearestSquared = float.MaxValue;
            foreach (var existing in sceneObjects)
            {
                if (existing.ObjectName != "SpaceSwan" || existing.WorldPosition == null)
                    continue;
                nearestSquared = MathF.Min(nearestSquared,
                    OmegaObjectHelpers.GetDistanceSquared(candidate, existing.WorldPosition));
            }

            if (nearestSquared >= minimumSquared)
                return candidate;
            if (nearestSquared > bestNearestDistanceSquared)
            {
                best = candidate;
                bestNearestDistanceSquared = nearestSquared;
            }
        }

        // Crowded scenes still get the least clustered candidate instead of dropping a swan.
        return best;
    }
}
