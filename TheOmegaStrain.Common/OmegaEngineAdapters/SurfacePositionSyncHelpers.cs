using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;
using static TheOmegaStrain.Domain._3dSpecificsImplementations;

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
            return WorldPositionMath.GetWorldPositionWithXOffset(obj, viewportCenterOffset, CreateVector);
        }

        public static Vector3? GetGuidanceTargetWorldPosition(I3dObject obj)
        {
            return WorldPositionMath.GetWorldPositionWithXOffset(obj, ScreenSetup.screenSizeX / 2f, CreateVector);
        }

        public static Vector3 GetShipRamTargetWorldPosition(I3dObject enemyObject)
        {
            if (GameState.ShipState.ShipCrashCenterWorldPosition is Vector3 shipCrashCenterWorldPosition)
            {
                return shipCrashCenterWorldPosition;
            }

            var globalMapPosition = GameState.SurfaceState.GlobalMapPosition;
            var enemyOffsets = enemyObject.ObjectOffsets;
            var shipOffsets = GameState.ShipState.ShipObjectOffsets;

            return WorldPositionMath.GetShipRamTargetWorldPosition(
                globalMapPosition,
                enemyOffsets,
                shipOffsets,
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
