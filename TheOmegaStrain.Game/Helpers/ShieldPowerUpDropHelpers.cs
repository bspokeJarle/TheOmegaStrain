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
        private const bool EnableDiagnostics = false;

        public static bool CanDropShield(
            I3dObject source,
            IReadOnlyList<OmegaObject3D> aiObjects,
            IVector3 shipWorldPosition)
        {
            var sourcePosition = GetRenderedWorldPosition(source);
            if (sourcePosition == null)
            {
                LogDecision(source, null, shipWorldPosition, 0f, 0, 0, null,
                    allowed: false, reason: "MissingSourcePosition");
                return false;
            }

            float sourceDistance = GetDistance(sourcePosition, shipWorldPosition);
            bool isWithinDropDistance = sourceDistance <= MaximumDropDistance;
            if (!source.IsOnScreen && !isWithinDropDistance)
            {
                LogDecision(source, sourcePosition, shipWorldPosition, sourceDistance, 0, 0, null,
                    allowed: false, reason: "OffScreenAndBeyondMaximumDropDistance");
                return false;
            }

            float screenWorldSize = SurfaceSetup.DefaultViewPortSize * SurfaceSetup.tileSize;
            float droneSearchDistance = screenWorldSize * DroneSearchDistanceInScreens;
            int liveDrones = 0;
            int nearbyLiveDrones = 0;
            float? nearestDroneDistance = null;

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

                liveDrones++;
                var candidatePosition = GetRenderedWorldPosition(candidate);
                if (candidatePosition == null)
                    continue;

                float droneDistance = GetDistance(candidatePosition, shipWorldPosition);
                if (!nearestDroneDistance.HasValue || droneDistance < nearestDroneDistance.Value)
                    nearestDroneDistance = droneDistance;

                if (droneDistance <= droneSearchDistance)
                    nearbyLiveDrones++;
            }

            bool allowed = nearbyLiveDrones > 0;
            LogDecision(
                source,
                sourcePosition,
                shipWorldPosition,
                sourceDistance,
                liveDrones,
                nearbyLiveDrones,
                nearestDroneDistance,
                allowed,
                allowed ? "Allowed" : "NoLiveDroneWithinFiveScreens");
            return allowed;
        }

        private static float GetDistance(IVector3 first, IVector3 second) =>
            System.MathF.Sqrt(OmegaObjectHelpers.GetDistanceSquared(first, second));

        private static IVector3? GetRenderedWorldPosition(I3dObject obj) =>
            SurfacePositionSyncHelpers.GetMinimapMarkerWorldPosition(obj) ?? obj.WorldPosition;

        private static void LogDecision(
            I3dObject source,
            IVector3? sourcePosition,
            IVector3 shipPosition,
            float sourceDistance,
            int liveDrones,
            int nearbyLiveDrones,
            float? nearestDroneDistance,
            bool allowed,
            string reason)
        {
            if (!Logger.ShouldLog(EnableDiagnostics))
                return;

            var raw = source.WorldPosition;
            Logger.Log(
                $"DROP_DECISION id={source.ObjectId}; allowed={allowed}; reason={reason}; " +
                $"hasPowerUp={source.HasPowerUp}; powerUpType={source.PowerUpType}; " +
                $"onScreen={source.IsOnScreen}; sourceDistance={sourceDistance:0.##}; " +
                $"maximumDropDistance={MaximumDropDistance:0.##}; liveDrones={liveDrones}; " +
                $"nearbyLiveDrones={nearbyLiveDrones}; nearestDroneDistance={(nearestDroneDistance?.ToString("0.##") ?? "none")}; " +
                $"droneSearchDistance={(SurfaceSetup.DefaultViewPortSize * SurfaceSetup.tileSize * DroneSearchDistanceInScreens):0.##}; " +
                $"raw=({raw?.x:0.##},{raw?.y:0.##},{raw?.z:0.##}); " +
                $"rendered=({sourcePosition?.x:0.##},{sourcePosition?.y:0.##},{sourcePosition?.z:0.##}); " +
                $"ship=({shipPosition.x:0.##},{shipPosition.y:0.##},{shipPosition.z:0.##})",
                "ShieldDrop");
            Logger.Flush();
        }
    }
}
