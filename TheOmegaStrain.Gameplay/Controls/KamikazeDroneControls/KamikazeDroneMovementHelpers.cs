using CommonUtilities.CommonGlobalState;
using Domain;
using System;
using static Domain._3dSpecificsImplementations;

namespace GameAiAndControls.Helpers
{
    internal static class KamikazeDroneMovementHelpers
    {
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
            return ToVector3(VectorMath.Normalize(v));
        }

        internal static float Length(Vector3 v)
        {
            return VectorMath.Length(v);
        }

        internal static float Dot(Vector3 a, Vector3 b)
        {
            return VectorMath.Dot(a, b);
        }

        internal static Vector3 GetLocalCrashCenter(I3dObject obj)
        {
            return ToVector3(ObjectCollisionGeometry.GetLocalCrashCenter(obj));
        }

        internal static Vector3 RotateLocalPoint(Vector3 point, IVector3? rotation)
        {
            return ToVector3(ObjectCollisionGeometry.RotateLocalPoint(point, rotation));
        }

        internal static Vector3 GetRotatedLocalCrashCenter(I3dObject obj)
        {
            return ToVector3(ObjectCollisionGeometry.GetRotatedLocalCrashCenter(obj));
        }

        internal static Vector3 GetDroneCrashCenterWorldPosition(I3dObject obj)
        {
            return ToVector3(ObjectCollisionGeometry.GetObjectCrashCenterWorldPosition(
                obj,
                includeObjectOffsets: true));
        }

        internal static Vector3 GetNavigationCrashCenterWorldPosition(I3dObject obj)
        {
            return ToVector3(ObjectCollisionGeometry.GetObjectCrashCenterWorldPosition(
                obj,
                includeObjectOffsets: false));
        }

        internal static Vector3 GetCompensatedHuntTargetWorldPosition(I3dObject hunter, I3dObject target)
        {
            return ToVector3(ObjectCollisionGeometry.GetCompensatedHuntTargetWorldPosition(hunter, target));
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
            return ObjectCollisionGeometry.GetApproximateCrashRadius(obj);
        }
    }
}
