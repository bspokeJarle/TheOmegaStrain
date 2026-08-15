using TheOmegaStrain.Common.CommonSetup;
using System;
using System.Collections.Generic;

namespace TheOmegaStrain.Game.Helpers
{
    public static class PolarBearPlacementHelpers
    {
        public static bool IsAtLeastOneScreenFromExisting(
            int tileX,
            int tileZ,
            int tileSize,
            IEnumerable<(int tileX, int tileZ)> existingPlacements)
        {
            if (tileSize <= 0)
                return true;

            float minDistanceTiles = ScreenSetup.screenSizeX / (float)tileSize;
            float minDistanceSquared = minDistanceTiles * minDistanceTiles;

            foreach (var placement in existingPlacements)
            {
                int dx = tileX - placement.tileX;
                int dz = tileZ - placement.tileZ;
                if ((dx * dx) + (dz * dz) < minDistanceSquared)
                    return false;
            }

            return true;
        }
    }
}
