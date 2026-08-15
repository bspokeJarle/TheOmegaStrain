using System.Collections.Generic;

namespace RetroMesh.Engine
{
    public static class ObjectCollisionGeometry
    {
        public static EngineVector3 GetLocalCrashCenter(IRenderable3dObject? obj)
        {
            if (obj?.CrashBoxes == null || obj.CrashBoxes.Count == 0)
                return new EngineVector3();

            var localPoints = new List<IVector3>();
            foreach (var box in obj.CrashBoxes)
            {
                if (box == null)
                    continue;

                foreach (var point in box)
                {
                    if (point != null)
                        localPoints.Add(point);
                }
            }

            return localPoints.Count > 0
                ? GeometryMath.GetCenterOfBox(localPoints)
                : new EngineVector3();
        }

        public static EngineVector3 RotateLocalPoint(
            IVector3 point,
            IVector3? rotation,
            MeshRotation? meshRotation = null)
        {
            if (rotation == null)
                return new EngineVector3(point.x, point.y, point.z);

            meshRotation ??= new MeshRotation();
            var rotatedPoint = meshRotation.RotatePoint(rotation.z, point, 'Z');
            rotatedPoint = meshRotation.RotatePoint(rotation.y, rotatedPoint, 'Y');
            rotatedPoint = meshRotation.RotatePoint(rotation.x, rotatedPoint, 'X');
            return rotatedPoint;
        }

        public static EngineVector3 GetRotatedLocalCrashCenter(
            IRenderable3dObject obj,
            MeshRotation? meshRotation = null)
        {
            return RotateLocalPoint(GetLocalCrashCenter(obj), obj.Rotation, meshRotation);
        }

        public static EngineVector3 GetObjectCrashCenterWorldPosition(
            IRenderable3dObject obj,
            bool includeObjectOffsets,
            MeshRotation? meshRotation = null)
        {
            var rotatedLocalCrashCenter = GetRotatedLocalCrashCenter(obj, meshRotation);
            var worldPosition = obj.WorldPosition;
            var objectOffsets = obj.ObjectOffsets;

            return new EngineVector3
            {
                x = (worldPosition?.x ?? 0f)
                    + (includeObjectOffsets ? objectOffsets?.x ?? 0f : 0f)
                    + rotatedLocalCrashCenter.x,
                y = (worldPosition?.y ?? 0f)
                    + (includeObjectOffsets ? objectOffsets?.y ?? 0f : 0f)
                    + rotatedLocalCrashCenter.y,
                z = (worldPosition?.z ?? 0f)
                    + (includeObjectOffsets ? objectOffsets?.z ?? 0f : 0f)
                    + rotatedLocalCrashCenter.z
            };
        }

        public static EngineVector3 GetCompensatedHuntTargetWorldPosition(
            IRenderable3dObject hunter,
            IRenderable3dObject target,
            MeshRotation? meshRotation = null)
        {
            var hunterCenter = GetRotatedLocalCrashCenter(hunter, meshRotation);
            var targetCenter = GetRotatedLocalCrashCenter(target, meshRotation);
            var hunterOffsets = hunter.ObjectOffsets;
            var targetOffsets = target.ObjectOffsets;
            var targetWorld = target.WorldPosition;

            return new EngineVector3
            {
                x = (targetWorld?.x ?? 0f)
                    + (targetOffsets?.x ?? 0f)
                    + targetCenter.x
                    - (hunterOffsets?.x ?? 0f)
                    - hunterCenter.x,
                y = (targetWorld?.y ?? 0f)
                    + (targetOffsets?.y ?? 0f)
                    + targetCenter.y
                    - (hunterOffsets?.y ?? 0f)
                    - hunterCenter.y,
                z = (targetWorld?.z ?? 0f)
                    - (targetOffsets?.z ?? 0f)
                    - targetCenter.z
                    + (hunterOffsets?.z ?? 0f)
                    + hunterCenter.z
            };
        }

        public static float GetApproximateCrashRadius(
            IRenderable3dObject? obj,
            MeshRotation? meshRotation = null)
        {
            if (obj?.CrashBoxes == null || obj.CrashBoxes.Count == 0)
                return 0f;

            var localCenter = GetRotatedLocalCrashCenter(obj, meshRotation);
            float maxDistance = 0f;

            foreach (var box in obj.CrashBoxes)
            {
                if (box == null)
                    continue;

                foreach (var point in box)
                {
                    if (point == null)
                        continue;

                    var rotatedPoint = RotateLocalPoint(point, obj.Rotation, meshRotation);
                    float dx = rotatedPoint.x - localCenter.x;
                    float dy = rotatedPoint.y - localCenter.y;
                    float dz = rotatedPoint.z - localCenter.z;
                    float distance = System.MathF.Sqrt(dx * dx + dy * dy + dz * dz);
                    if (distance > maxDistance)
                        maxDistance = distance;
                }
            }

            return maxDistance;
        }
    }
}
