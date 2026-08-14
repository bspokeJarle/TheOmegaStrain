using CommonUtilities.OmegaEngineAdapters;
using CommonUtilities.CommonGlobalState;
using Domain;
using System;
using System.Collections.Generic;
using static Domain._3dSpecificsImplementations;

namespace GameAiAndControls.Helpers
{
    internal static class KamikazeDroneMovementHelpers
    {
        private static readonly OmegaMeshRotation Rotate3d = new();

        internal static Vector3 ToVector3(IVector3? v)
        {
            if (v is null)
            {
                return new Vector3();
            }

            return new Vector3
            {
                x = v.x,
                y = v.y,
                z = v.z
            };
        }

        internal static Vector3 Normalize(Vector3 v)
        {
            float lenSq = v.x * v.x + v.y * v.y + v.z * v.z;
            if (lenSq <= 1e-6f)
            {
                return new Vector3 { x = 0, y = 0, z = 0 };
            }

            float invLen = 1f / MathF.Sqrt(lenSq);
            return new Vector3
            {
                x = v.x * invLen,
                y = v.y * invLen,
                z = v.z * invLen
            };
        }

        internal static float Length(Vector3 v)
        {
            return MathF.Sqrt(v.x * v.x + v.y * v.y + v.z * v.z);
        }

        internal static float Dot(Vector3 a, Vector3 b)
        {
            return (a.x * b.x) + (a.y * b.y) + (a.z * b.z);
        }

        internal static Vector3 GetLocalCrashCenter(I3dObject obj)
        {
            if (obj.CrashBoxes == null || obj.CrashBoxes.Count == 0)
            {
                return new Vector3();
            }

            var localPoints = new List<Vector3>();
            foreach (var box in obj.CrashBoxes)
            {
                foreach (var point in box)
                {
                    localPoints.Add((Vector3)point);
                }
            }

            return localPoints.Count > 0
                ? OmegaObjectHelpers.GetCenterOfBox(localPoints)
                : new Vector3();
        }

        internal static Vector3 RotateLocalPoint(Vector3 point, IVector3? rotation)
        {
            if (rotation is not Vector3 rotationVector)
            {
                return point;
            }

            var rotatedPoint = (Vector3)Rotate3d.RotatePoint(rotationVector.z, point, 'Z');
            rotatedPoint = (Vector3)Rotate3d.RotatePoint(rotationVector.y, rotatedPoint, 'Y');
            rotatedPoint = (Vector3)Rotate3d.RotatePoint(rotationVector.x, rotatedPoint, 'X');
            return rotatedPoint;
        }

        internal static Vector3 GetRotatedLocalCrashCenter(I3dObject obj)
        {
            return RotateLocalPoint(GetLocalCrashCenter(obj), obj.Rotation);
        }

        internal static Vector3 GetDroneCrashCenterWorldPosition(I3dObject obj)
        {
            var rotatedLocalCrashCenter = GetRotatedLocalCrashCenter(obj);

            var worldPosition = obj.WorldPosition;
            var objectOffsets = obj.ObjectOffsets;

            return new Vector3
            {
                x = (worldPosition?.x ?? 0f) + (objectOffsets?.x ?? 0f) + rotatedLocalCrashCenter.x,
                y = (worldPosition?.y ?? 0f) + (objectOffsets?.y ?? 0f) + rotatedLocalCrashCenter.y,
                z = (worldPosition?.z ?? 0f) + (objectOffsets?.z ?? 0f) + rotatedLocalCrashCenter.z
            };
        }

        internal static Vector3 GetNavigationCrashCenterWorldPosition(I3dObject obj)
        {
            var rotatedLocalCrashCenter = GetRotatedLocalCrashCenter(obj);
            var worldPosition = obj.WorldPosition;

            return new Vector3
            {
                x = (worldPosition?.x ?? 0f) + rotatedLocalCrashCenter.x,
                y = (worldPosition?.y ?? 0f) + rotatedLocalCrashCenter.y,
                z = (worldPosition?.z ?? 0f) + rotatedLocalCrashCenter.z
            };
        }

        internal static Vector3 GetCompensatedHuntTargetWorldPosition(I3dObject hunter, I3dObject target)
        {
            var hunterCenter = GetRotatedLocalCrashCenter(hunter);
            var targetCenter = GetRotatedLocalCrashCenter(target);
            var hunterOffsets = hunter.ObjectOffsets;
            var targetOffsets = target.ObjectOffsets;
            var targetWorld = target.WorldPosition;

            return new Vector3
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

        internal static Vector3? GetShipCrashCenterWorldPosition()
        {
            if (GameState.ShipState?.ShipCrashCenterWorldPosition is Vector3 shipCrashCenter)
            {
                return new Vector3
                {
                    x = shipCrashCenter.x - (CommonUtilities.CommonSetup.ScreenSetup.screenSizeX / 2f),
                    y = shipCrashCenter.y,
                    z = shipCrashCenter.z - (CommonUtilities.CommonSetup.ScreenSetup.screenSizeY / 2f)
                };
            }

            if (GameState.ShipState?.ShipWorldPosition is Vector3 shipWorldPosition)
            {
                return new Vector3
                {
                    x = shipWorldPosition.x - (CommonUtilities.CommonSetup.ScreenSetup.screenSizeX / 2f),
                    y = shipWorldPosition.y,
                    z = shipWorldPosition.z - (CommonUtilities.CommonSetup.ScreenSetup.screenSizeY / 2f)
                };
            }

            return null;
        }

        internal static float GetApproximateCrashRadius(I3dObject obj)
        {
            if (obj?.CrashBoxes == null || obj.CrashBoxes.Count == 0)
            {
                return 0f;
            }

            var localCenter = GetRotatedLocalCrashCenter(obj);
            float maxDistance = 0f;

            foreach (var box in obj.CrashBoxes)
            {
                foreach (var point in box)
                {
                    var rotatedPoint = RotateLocalPoint((Vector3)point, obj.Rotation);
                    float dx = rotatedPoint.x - localCenter.x;
                    float dy = rotatedPoint.y - localCenter.y;
                    float dz = rotatedPoint.z - localCenter.z;
                    float distance = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
                    if (distance > maxDistance)
                    {
                        maxDistance = distance;
                    }
                }
            }

            return maxDistance;
        }
    }
}
