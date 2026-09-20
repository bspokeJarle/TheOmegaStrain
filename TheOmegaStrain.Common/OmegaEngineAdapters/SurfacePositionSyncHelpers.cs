using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;

using RetroMesh.Engine;

namespace TheOmegaStrain.Common.OmegaEngineAdapters
{
    public static class SurfacePositionSyncHelpers
    {
        public const float DefaultEnemySurfaceSyncFactorY = 2.5f;

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
            var enemyLocalCentre = ObjectCollisionGeometry.GetRotatedLocalCrashCenter(enemyObject);
            return GetShipRamTargetWorldPosition(
                VectorMath.Add(enemyObject.ObjectOffsets ?? new Vector3(), enemyLocalCentre));
        }

        // Also accepts a muzzle/weapon anchor without adding the enemy's collision centre.
        public static Vector3 GetShipRamTargetWorldPosition(IVector3 enemyLocalAnchor)
        {
            var globalMapPosition = GameState.SurfaceState.GlobalMapPosition;
            var shipState = GameState.ShipState;
            var shipOffsets = shipState.ShipObjectOffsets ?? new Vector3();
            var shipWorld = shipState.ShipWorldPosition ?? GetShipWorldPosition(shipOffsets.y, shipOffsets.z);
            var shipLocalCentre = shipState.ShipCrashCenterWorldPosition == null
                ? new Vector3()
                : VectorMath.Subtract(shipState.ShipCrashCenterWorldPosition, shipWorld);
            return WorldPositionMath.GetShipRamTargetWorldPosition(
                globalMapPosition,
                enemyLocalAnchor,
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
