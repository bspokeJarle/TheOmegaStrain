using System.Collections.Generic;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Domain;

namespace TheOmegaStrain.Gameplay.Helpers
{
    public static class ShieldPowerUpAvailabilityHelpers
    {
        public const int ShieldSupportDroneThreshold = 3;
        public const int MaximumNearbyShieldCarriers = 1;
        private const float ShieldSupportTargetScreenWidth = 0.65f;

        public static bool TryMoveShieldCarrierIntoArea(
            I3dObject carrier,
            IReadOnlyList<OmegaObject3D> aiObjects)
        {
            if (carrier.IsOnScreen ||
                !IsLivingShieldCarrier(carrier) ||
                GameState.GamePlayState.IsShieldActive ||
                GameState.ShipState.ShipWorldPosition == null)
            {
                return false;
            }

            int liveDrones = 0;
            for (int i = 0; i < aiObjects.Count; i++)
            {
                var candidate = aiObjects[i];
                if (candidate.ObjectName == "PowerUp" &&
                    candidate.PowerUpType == PowerUpType.Shield &&
                    candidate.ImpactStatus?.HasExploded != true)
                {
                    return false;
                }

                if (IsLivingDrone(candidate))
                    liveDrones++;
            }

            if (liveDrones < ShieldSupportDroneThreshold)
                return false;

            var shipPosition = GameState.ShipState.ShipWorldPosition;
            float nearbyDistanceSquared =
                ScreenSetup.ObjectVisibilityDistance * ScreenSetup.ObjectVisibilityDistance;
            int nearbyShieldCarriers = 0;
            int distantCarrierRank = 0;

            for (int i = 0; i < aiObjects.Count; i++)
            {
                var candidate = aiObjects[i];
                if (!IsLivingShieldCarrier(candidate))
                    continue;

                var renderedPosition = SurfacePositionSyncHelpers.GetMinimapMarkerWorldPosition(candidate)
                    ?? candidate.WorldPosition;
                bool isNearby = renderedPosition != null &&
                    OmegaObjectHelpers.GetDistanceSquared(renderedPosition, shipPosition) <= nearbyDistanceSquared;

                if (isNearby)
                {
                    nearbyShieldCarriers++;
                    continue;
                }

                if (candidate.IsOnScreen)
                    continue;

                if (candidate.ObjectId == carrier.ObjectId)
                    break;

                distantCarrierRank++;
            }

            int carriersNeeded = MaximumNearbyShieldCarriers - nearbyShieldCarriers;
            if (carriersNeeded <= 0 || distantCarrierRank >= carriersNeeded)
                return false;

            float swanWorldY = carrier.WorldPosition.y;
            var target = SurfacePositionSyncHelpers.GetShipRamTargetWorldPosition(carrier);
            float side = distantCarrierRank == 0 ? -1f : 1f;
            target.x += side * ScreenSetup.screenSizeX * ShieldSupportTargetScreenWidth;
            // Relocation only changes the horizontal world position. SpaceSwanControls
            // already owns flight height through its normal ObjectOffsets/surface sync.
            target.y = swanWorldY;
            carrier.WorldPosition = target;
            return true;
        }

        private static bool IsLivingShieldCarrier(I3dObject candidate) =>
            candidate.ObjectName == "SpaceSwan" &&
            candidate.HasPowerUp &&
            candidate.PowerUpType == PowerUpType.Shield &&
            candidate.IsActive &&
            candidate.WorldPosition != null &&
            candidate.ImpactStatus?.ObjectHealth is > 0 &&
            candidate.ImpactStatus?.HasExploded != true;

        private static bool IsLivingDrone(I3dObject candidate) =>
            candidate.ObjectName == "KamikazeDrone" &&
            candidate.IsActive &&
            candidate.WorldPosition != null &&
            candidate.ImpactStatus?.ObjectHealth is > 0 &&
            candidate.ImpactStatus?.HasExploded != true;
    }
}
