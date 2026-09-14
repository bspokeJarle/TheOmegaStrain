using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;

using RetroMesh.Engine;

namespace TheOmegaStrain.Common.OmegaEngineAdapters
{
    public static class SurfacePositionSyncHelpers
    {
        public const float DefaultEnemySurfaceSyncFactorY = 2.5f;
        public const float LowAngleBackgroundCorrectionFactor = 1.5f;
        public const float NormalAndHighBackgroundLiftPerWorldUnit = 0.02f;

        private static Vector3 CreateVector(float x, float y, float z) => new(x, y, z);

        public static Vector3 GetSurfaceAlignedWorldPosition(I3dObject obj)
        {
            var surfaceOffsets = GameState.SurfaceState.SurfaceViewportObject?.ObjectOffsets;
            return WorldPositionMath.GetSurfaceAlignedWorldPosition(obj, surfaceOffsets, CreateVector);
        }

        public static Vector3 GetSurfaceSyncedObjectOffsets(I3dObject obj, float initialOffsetY, float syncFactorY = DefaultEnemySurfaceSyncFactorY)
        {
            return WorldPositionMath.GetSurfaceSyncedObjectOffsets(
                obj.ObjectOffsets,
                GameState.SurfaceState.GlobalMapPosition.y,
                initialOffsetY,
                syncFactorY,
                CreateVector);
        }

        /// <summary>
        /// Returns the extra screen-Y correction relative to Omega's original 70°
        /// presentation. Existing object offsets were authored for that angle, so
        /// applying the complete plane slope would count the original tilt twice.
        /// The viewport centre (surface-local Z = 0) is the neutral point, and
        /// terrain height is deliberately excluded so flying objects do not bob.
        /// </summary>
        public static float GetSurfacePitchHeightCorrectionY(float surfaceLongitudinalDistance, float pitchDegrees)
        {
            float pitchRadians = pitchDegrees * (MathF.PI / 180f);
            float originalPitchRadians = OmegaWorldViewSetup.OriginalWorldPitchDegrees * (MathF.PI / 180f);
            float currentCosine = MathF.Cos(pitchRadians);
            float originalCosine = MathF.Cos(originalPitchRadians);

            if (!float.IsFinite(surfaceLongitudinalDistance)
                || !float.IsFinite(currentCosine)
                || !float.IsFinite(originalCosine))
                return 0f;

            // World Z is the unrotated longitudinal distance along the surface.
            // X rotation projects that distance onto Y by cos(pitch). Existing
            // offsets already contain the original 70-degree presentation, so
            // only the difference between the two projections is added.
            float correction = surfaceLongitudinalDistance * (currentCosine - originalCosine);

            if (surfaceLongitudinalDistance > 0f)
            {
                // From the viewport centre toward the player, preserve the
                // original combat height exactly. Depth correction here made
                // nearby targets unnecessarily difficult to hit.
                return 0f;
            }

            // Low needs the stronger angle correction. Normal and High instead
            // receive a small explicit upward lift toward the horizon; High is
            // Omega's original angle and therefore has no angle delta of its own.
            if (pitchDegrees <= 56.5f)
                return correction * LowAngleBackgroundCorrectionFactor;

            return correction + surfaceLongitudinalDistance * NormalAndHighBackgroundLiftPerWorldUnit;
        }

        /// <summary>
        /// Calculates an object's Z relative to the visible surface, then converts
        /// that local position to the matching ground-plane Y correction.
        /// </summary>
        public static float GetSurfacePitchHeightCorrectionY(I3dObject obj, float pitchDegrees)
        {
            var worldPosition = obj.WorldPosition;
            float surfaceLongitudinalDistance = worldPosition == null || WorldPositionMath.IsOrigin(worldPosition)
                ? 0f
                : worldPosition.z - GameState.SurfaceState.GlobalMapPosition.z;

            return GetSurfacePitchHeightCorrectionY(surfaceLongitudinalDistance, pitchDegrees);
        }

        /// <summary>
        /// Adds only the camera-angle correction to an already calculated object
        /// position. Movement controllers remain responsible for the base Y.
        /// </summary>
        public static void AddSurfacePitchHeightCorrectionY(I3dObject obj, float pitchDegrees)
        {
            if (!obj.IsOnScreen || obj.ObjectOffsets == null)
                return;

            obj.ObjectOffsets.y += GetSurfacePitchHeightCorrectionY(obj, pitchDegrees);
        }

        public static Vector3 GetShipWorldPosition(float shipOffsetY, float zoom)
        {
            var globalMapPosition = GameState.SurfaceState.GlobalMapPosition;
            return WorldPositionMath.GetShipWorldPosition(
                globalMapPosition,
                ScreenSetup.screenSizeX,
                ScreenSetup.screenSizeY,
                shipOffsetY,
                zoom,
                CreateVector);
        }

        public static Vector3? GetMinimapMarkerWorldPosition(I3dObject obj)
        {
            int viewportCenterOffset = (SurfaceSetup.viewPortSize * SurfaceSetup.tileSize) / 2;
            var shipState = GameState.ShipState;
            var shipOffsets = shipState?.ShipObjectOffsets;
            if (obj.WorldPosition == null || shipOffsets == null)
                return null;

            var centre = ObjectCollisionGeometry.GetRotatedLocalCrashCenter(obj);
            var offsets = obj.ObjectOffsets;
            var shipWorld = shipState?.ShipWorldPosition;
            var shipCentre = shipState?.ShipCrashCenterWorldPosition;
            float shipLocalX = shipWorld != null && shipCentre != null ? shipCentre.x - shipWorld.x : 0f;
            float shipLocalZ = shipWorld != null && shipCentre != null ? shipCentre.z - shipWorld.z : 0f;

            // Rendered X = worldX - mapX + offsetX + localCentreX.
            // Rendered Z = mapZ - worldZ + offsetZ + localCentreZ (opposite sign).
            // Translate the displacement from Ship's rendered centre onto its fixed
            // minimap marker for every world object, including AttackShip.
            return new Vector3(
                obj.WorldPosition.x + viewportCenterOffset + (offsets?.x ?? 0f) + centre.x
                    - shipOffsets.x - shipLocalX,
                obj.WorldPosition.y,
                obj.WorldPosition.z + viewportCenterOffset - (offsets?.z ?? 0f) - centre.z
                    + shipOffsets.z + shipLocalZ);
        }

        public static Vector3? GetGuidanceTargetWorldPosition(I3dObject obj)
        {
            return WorldPositionMath.GetWorldPositionWithXOffset(obj, ScreenSetup.screenSizeX / 2f, CreateVector);
        }

        /// <summary>
        /// Returns the enemy WorldPosition that places its rotated collision centre on Ship.
        /// Rendered Z is mapZ - worldZ + offsetZ: raw ShipState coordinates cannot be
        /// used as a pursuit target by just subtracting half the screen size.
        /// </summary>
        public static Vector3 GetShipRamTargetWorldPosition(I3dObject enemyObject)
        {
            var globalMapPosition = GameState.SurfaceState.GlobalMapPosition;
            var shipState = GameState.ShipState;
            var shipOffsets = shipState.ShipObjectOffsets ?? new Vector3();
            var shipWorld = shipState.ShipWorldPosition ?? GetShipWorldPosition(shipOffsets.y, shipOffsets.z);
            var shipLocalCentre = shipState.ShipCrashCenterWorldPosition == null
                ? new Vector3()
                : VectorMath.Subtract(shipState.ShipCrashCenterWorldPosition, shipWorld);
            var enemyLocalCentre = ObjectCollisionGeometry.GetRotatedLocalCrashCenter(enemyObject);

            return WorldPositionMath.GetShipRamTargetWorldPosition(
                globalMapPosition,
                VectorMath.Add(enemyObject.ObjectOffsets ?? new Vector3(), enemyLocalCentre),
                VectorMath.Add(shipOffsets, shipLocalCentre),
                CreateVector);
        }

        public static Vector3 GetObjectCrashCenterWorldPosition(I3dObject obj)
        {
            Vector3 basePosition;
            if (obj.ObjectName == "Ship" && GameState.ShipState.ShipWorldPosition is Vector3 shipWorldPosition)
            {
                basePosition = shipWorldPosition;
            }
            else
            {
                basePosition = GetSurfaceAlignedWorldPosition(obj);
            }

            return WorldPositionMath.GetObjectCrashCenterWorldPosition(obj, basePosition, CreateVector);
        }
    }
}
