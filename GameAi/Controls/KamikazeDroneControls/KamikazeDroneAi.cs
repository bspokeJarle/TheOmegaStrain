using CommonUtilities.OmegaEngineAdapters;
using CommonUtilities.CommonGlobalState;
using Domain;
using GameAiAndControls.Helpers;
using static Domain._3dSpecificsImplementations;

namespace GameAiAndControls.Controls.KamikazeDroneControls
{
    internal static class KamikazeDroneAi
    {
        internal static _3dObject? GetAuthoritativeDrone(I3dObject obj)
        {
            var aiObjects = GameState.SurfaceState?.AiObjects;
            if (aiObjects == null)
            {
                return null;
            }

            for (int i = 0; i < aiObjects.Count; i++)
            {
                var aiObject = aiObjects[i];
                if (aiObject.ObjectId == obj.ObjectId)
                {
                    return aiObject;
                }
            }

            return null;
        }

        internal static _3dObject? GetClosestActiveDecoy(I3dObject currentDrone)
        {
            var aiObjects = GameState.SurfaceState?.AiObjects;
            if (aiObjects == null || aiObjects.Count == 0)
            {
                return null;
            }

            var droneCenter = KamikazeDroneMovementHelpers.GetNavigationCrashCenterWorldPosition(currentDrone);
            _3dObject? closestDecoy = null;
            double closestDistance = double.MaxValue;

            for (int i = 0; i < aiObjects.Count; i++)
            {
                var candidate = aiObjects[i];
                if (candidate == null || candidate.ObjectId == currentDrone.ObjectId)
                {
                    continue;
                }

                if (candidate.ObjectName != "DroneDecoy" || candidate.ObjectParts == null || candidate.ObjectParts.Count == 0)
                {
                    continue;
                }

                if (candidate.ImpactStatus?.HasExploded == true)
                {
                    continue;
                }

                var candidateCenter = KamikazeDroneMovementHelpers.GetCompensatedHuntTargetWorldPosition(currentDrone, candidate);
                double distance = OmegaObjectHelpers.GetDistance(droneCenter, candidateCenter);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestDecoy = candidate;
                }
            }

            return closestDecoy;
        }

        internal static Vector3? GetClosestDecoyCrashCenterWorldPosition(I3dObject currentDrone)
        {
            var closestDecoy = GetClosestActiveDecoy(currentDrone);
            return closestDecoy == null
                ? null
                : KamikazeDroneMovementHelpers.GetCompensatedHuntTargetWorldPosition(currentDrone, closestDecoy);
        }

        internal static void TriggerDecoyCollision(I3dObject droneObject, _3dObject decoyObject)
        {
            if (droneObject?.ImpactStatus != null)
            {
                droneObject.ImpactStatus.HasCrashed = true;
                droneObject.ImpactStatus.ObjectName = decoyObject.ObjectName;
            }

            var authoritativeDrone = GetAuthoritativeDrone(droneObject);
            if (authoritativeDrone?.ImpactStatus != null)
            {
                authoritativeDrone.ImpactStatus.HasCrashed = true;
                authoritativeDrone.ImpactStatus.ObjectName = decoyObject.ObjectName;
            }

            if (decoyObject?.ImpactStatus != null)
            {
                decoyObject.ImpactStatus.HasCrashed = true;
                decoyObject.ImpactStatus.ObjectName = droneObject.ObjectName;
            }
        }

        internal static void SyncAuthoritativeDroneState(I3dObject source)
        {
            var authoritativeDrone = GetAuthoritativeDrone(source);
            if (authoritativeDrone == null || ReferenceEquals(authoritativeDrone, source))
            {
                return;
            }

            authoritativeDrone.WorldPosition = KamikazeDroneMovementHelpers.ToVector3(source.WorldPosition);
            authoritativeDrone.ObjectOffsets = KamikazeDroneMovementHelpers.ToVector3(source.ObjectOffsets);
            authoritativeDrone.Rotation = KamikazeDroneMovementHelpers.ToVector3(source.Rotation);
        }
    }
}
