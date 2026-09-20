using System.Collections.Generic;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Domain;

namespace TheOmegaStrain.Game.Helpers
{
    public static class ShieldPowerUpDropHelpers
    {
        public const float MaximumDropDistance = 500f;
        public const int DroneSearchDistanceInScreens = 5;

        public static bool CanDropShield(
            I3dObject source,
            IReadOnlyList<OmegaObject3D> aiObjects,
            IVector3 shipWorldPosition)
        {
            if (source.WorldPosition == null ||
                !IsWithinDistance(source.WorldPosition, shipWorldPosition, MaximumDropDistance))
            {
                return false;
            }

            float screenWorldSize = SurfaceSetup.DefaultViewPortSize * SurfaceSetup.tileSize;
            float droneSearchDistance = screenWorldSize * DroneSearchDistanceInScreens;

            for (int i = 0; i < aiObjects.Count; i++)
            {
                var candidate = aiObjects[i];
                if (candidate.ObjectName != "KamikazeDrone" ||
                    !candidate.IsActive ||
                    candidate.ImpactStatus?.ObjectHealth is not > 0 ||
                    candidate.ImpactStatus?.HasExploded == true ||
                    candidate.WorldPosition == null)
                {
                    continue;
                }

                if (IsWithinDistance(candidate.WorldPosition, shipWorldPosition, droneSearchDistance))
                    return true;
            }

            return false;
        }

        private static bool IsWithinDistance(IVector3 first, IVector3 second, float maxDistance) =>
            OmegaObjectHelpers.GetDistanceSquared(first, second) <= maxDistance * maxDistance;
    }
}
