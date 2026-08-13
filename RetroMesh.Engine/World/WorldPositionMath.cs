using System.Collections.Generic;

namespace Domain
{
    public static class WorldPositionMath
    {
        public static bool IsOrigin(IVector3? position)
        {
            return position == null ||
                   position.x == 0f &&
                   position.y == 0f &&
                   position.z == 0f;
        }

        public static TVector? GetLocalWorldPosition<TVector>(
            IRenderable3dObject? obj,
            IVector3 globalMapPosition,
            Func<float, float, float, TVector> vectorFactory)
            where TVector : class, IVector3
        {
            if (obj?.WorldPosition == null || IsOrigin(obj.WorldPosition))
                return null;

            return vectorFactory(
                globalMapPosition.x - obj.WorldPosition.x,
                globalMapPosition.y - obj.WorldPosition.y,
                globalMapPosition.z - obj.WorldPosition.z);
        }

        public static TVector GetAudioPosition<TVector>(
            IRenderable3dObject? obj,
            IVector3? localWorldPosition,
            Func<float, float, float, TVector> vectorFactory)
            where TVector : IVector3
        {
            var objectOffsets = obj?.ObjectOffsets;
            float offsetX = objectOffsets?.x ?? 0f;
            float offsetY = objectOffsets?.y ?? 0f;
            float offsetZ = objectOffsets?.z ?? 0f;

            if (localWorldPosition == null)
                return vectorFactory(offsetX, offsetY, offsetZ);

            return vectorFactory(
                -localWorldPosition.x + offsetX,
                -localWorldPosition.y + offsetY,
                localWorldPosition.z + offsetZ);
        }

        public static bool IsWithinDistance(IVector3 point1, IVector3 point2, float maxDistance)
        {
            float maxDistanceSquared = maxDistance * maxDistance;
            return GeometryMath.GetDistanceSquared(point1, point2) <= maxDistanceSquared;
        }

        public static TVector GetSurfaceAlignedWorldPosition<TVector>(
            IRenderable3dObject obj,
            IVector3? surfaceOffsets,
            Func<float, float, float, TVector> vectorFactory)
            where TVector : IVector3
        {
            var worldPosition = obj.WorldPosition;
            if (worldPosition == null)
                return vectorFactory(0f, 0f, 0f);

            var objectOffsets = obj.ObjectOffsets;
            float deltaX = (surfaceOffsets?.x ?? 0f) - (objectOffsets?.x ?? 0f);
            float deltaZ = (surfaceOffsets?.z ?? 0f) - (objectOffsets?.z ?? 0f);

            return vectorFactory(
                worldPosition.x - deltaX,
                worldPosition.y,
                worldPosition.z - deltaZ);
        }

        public static TVector GetSurfaceSyncedObjectOffsets<TVector>(
            IVector3? objectOffsets,
            float globalMapY,
            float initialOffsetY,
            float syncFactorY,
            Func<float, float, float, TVector> vectorFactory)
            where TVector : IVector3
        {
            return vectorFactory(
                objectOffsets?.x ?? 0f,
                globalMapY * syncFactorY + initialOffsetY,
                objectOffsets?.z ?? 0f);
        }

        public static TVector GetShipWorldPosition<TVector>(
            IVector3 globalMapPosition,
            float screenSizeX,
            float screenSizeY,
            float shipOffsetY,
            float zoom,
            Func<float, float, float, TVector> vectorFactory)
            where TVector : IVector3
        {
            return vectorFactory(
                globalMapPosition.x + screenSizeX / 2f,
                globalMapPosition.y + shipOffsetY,
                globalMapPosition.z + screenSizeY / 2f + zoom);
        }

        public static TVector? GetWorldPositionWithXOffset<TVector>(
            IRenderable3dObject obj,
            float xOffset,
            Func<float, float, float, TVector> vectorFactory)
            where TVector : class, IVector3
        {
            var worldPosition = obj.WorldPosition;
            if (worldPosition == null)
                return null;

            return vectorFactory(
                worldPosition.x + xOffset + (obj.ObjectOffsets?.x ?? 0f),
                worldPosition.y,
                worldPosition.z);
        }

        public static TVector GetShipRamTargetWorldPosition<TVector>(
            IVector3 globalMapPosition,
            IVector3? enemyOffsets,
            IVector3? shipOffsets,
            Func<float, float, float, TVector> vectorFactory)
            where TVector : IVector3
        {
            return vectorFactory(
                globalMapPosition.x + (shipOffsets?.x ?? 0f) - (enemyOffsets?.x ?? 0f),
                globalMapPosition.y + (shipOffsets?.y ?? 0f) - (enemyOffsets?.y ?? 0f),
                globalMapPosition.z + (enemyOffsets?.z ?? 0f) - (shipOffsets?.z ?? 0f));
        }

        public static TVector GetLocalCrashCenter<TVector>(
            IReadOnlyList<List<IVector3>>? crashBoxes,
            Func<float, float, float, TVector> vectorFactory)
            where TVector : IVector3
        {
            if (crashBoxes == null || crashBoxes.Count == 0)
                return vectorFactory(0f, 0f, 0f);

            var localPoints = new List<IVector3>();
            foreach (var box in crashBoxes)
            {
                if (box == null)
                    continue;

                foreach (var point in box)
                {
                    localPoints.Add(point);
                }
            }

            var center = GeometryMath.GetCenterOfBox(localPoints);
            return vectorFactory(center.x, center.y, center.z);
        }

        public static TVector RotatePointByRotation<TVector>(
            IVector3 point,
            IVector3? rotation,
            Func<float, float, float, TVector> vectorFactory)
            where TVector : IVector3
        {
            if (rotation == null)
                return vectorFactory(point.x, point.y, point.z);

            var rotate = new MeshRotation();
            var rotatedPoint = rotate.RotatePoint(rotation.z, point, 'Z');
            rotatedPoint = rotate.RotatePoint(rotation.y, rotatedPoint, 'Y');
            rotatedPoint = rotate.RotatePoint(rotation.x, rotatedPoint, 'X');
            return vectorFactory(rotatedPoint.x, rotatedPoint.y, rotatedPoint.z);
        }

        public static TVector GetObjectCrashCenterWorldPosition<TVector>(
            IRenderable3dObject obj,
            IVector3 basePosition,
            Func<float, float, float, TVector> vectorFactory)
            where TVector : IVector3
        {
            var localCrashCenter = RotatePointByRotation(
                GetLocalCrashCenter(obj.CrashBoxes, static (x, y, z) => new EngineVector3(x, y, z)),
                obj.Rotation,
                static (x, y, z) => new EngineVector3(x, y, z));

            return vectorFactory(
                basePosition.x + localCrashCenter.x,
                basePosition.y + localCrashCenter.y,
                basePosition.z + localCrashCenter.z);
        }
    }
}
