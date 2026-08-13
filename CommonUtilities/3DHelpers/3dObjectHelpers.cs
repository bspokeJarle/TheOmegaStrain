using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonSetup;
using Domain;
using System.Runtime.CompilerServices;
using static Domain._3dSpecificsImplementations;


namespace CommonUtilities._3DHelpers
{
    public static class Common3dObjectHelpers
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector3? CopyVector(IVector3? vector)
        {
            if (vector == null)
            {
                return null;
            }

            return new Vector3(vector.x, vector.y, vector.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector3 CopyRequiredVector(IVector3 vector)
        {
            return new Vector3(vector.x, vector.y, vector.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector3 CreateVector(float x, float y, float z)
        {
            return new Vector3(x, y, z);
        }

        // -----------------------------------------------------------------
        //  HEADING HELPERS
        //  Shared heading logic for pointing objects toward a target.
        //  Z rotation is applied first to geometry (+X forward).
        //  After Z rotation by θ: tip at (cosθ, sinθ, 0).
        //  After X=WorldViewSetup.CameraPitchDegrees camera tilt: screenX ∝ cosθ, screenY ∝ sinθ.
        //  Z=0→right, Z=90→down, Z=180→left, Z=270→up.
        //  World-to-screen: screen right = world +X, screen down = world +Z.
        //  Therefore heading Z = atan2(dz, dx).
        // -----------------------------------------------------------------

        /// <summary>
        /// Computes the Z rotation (heading) that points an object's +X forward
        /// along the given world-space XZ direction. Base X rotation = WorldViewSetup.CameraPitchDegrees.
        /// Returns (Xrotation, Yrotation, Zrotation).
        /// </summary>
        public static (float X, float Y, float Z) GetHeadingFromDirection(float dx, float dz)
        {
            return GeometryMath.GetHeadingFromDirection(dx, dz, WorldViewSetup.CameraPitchDegrees);
        }

        /// <summary>
        /// Computes the heading rotation to point from a source position toward
        /// a target position in the world XZ plane.
        /// Returns (Xrotation, Yrotation, Zrotation).
        /// </summary>
        public static (float X, float Y, float Z) GetHeadingToTarget(IVector3 source, IVector3 target)
        {
            return GeometryMath.GetHeadingToTarget(source, target, WorldViewSetup.CameraPitchDegrees);
        }

        /// <summary>
        /// Normalizes an angle to the range (-180, 180].
        /// </summary>
        public static float NormalizeAngle(float angle)
        {
            return GeometryMath.NormalizeAngle(angle);
        }

        /// <summary>
        /// Smoothly moves a current angle toward a target angle by at most maxDelta degrees.
        /// </summary>
        public static float MoveAngleTowards(float current, float target, float maxDelta)
        {
            return GeometryMath.MoveAngleTowards(current, target, maxDelta);
        }

        public static float DotNormalized(IVector3 a, IVector3 b)
        {
            return GeometryMath.DotNormalized(a, b);
        }
        public static IVector3? GetLocalWorldPosition(this _3dObject inhabitant)
        {
            var globalMapPosition = GameState.SurfaceState.GlobalMapPosition;
            return WorldPositionMath.GetLocalWorldPosition(inhabitant, globalMapPosition, CreateVector);
        }

        public static Vector3 GetAudioPosition(this _3dObject inhabitant)
        {
            var localWorldPosition = inhabitant?.GetLocalWorldPosition();
            return WorldPositionMath.GetAudioPosition(inhabitant, localWorldPosition, CreateVector);
        }
        public static bool CheckInhabitantVisibility(this _3dObject inhabitant)
        {
            // 1. Land-based check
            if (inhabitant.SurfaceBasedId > 0 && inhabitant.ParentSurface?.LandBasedIds != null)
            {
                return inhabitant.ParentSurface.LandBasedIds.Contains(inhabitant.SurfaceBasedId);
            }

            // 2. Always-visible (onscreen) objects — world position (0, 0, 0)
            if (WorldPositionMath.IsOrigin(inhabitant.WorldPosition))
            {
                return true;
            }

            // 3. Distance-based visibility check
            var globalMapPosition = GameState.SurfaceState.GlobalMapPosition;
            var inhabitantPosition = inhabitant.WorldPosition;

            float maxDistance = ScreenSetup.ObjectVisibilityDistance * ScreenSetup.ScreenScaleX;
            return WorldPositionMath.IsWithinDistance(globalMapPosition, inhabitantPosition, maxDistance);
        }


        public static double GetDistance(Vector3 point1, Vector3 point2)
        {
            return GeometryMath.GetDistance(point1, point2);
        }

        public static float GetDistanceSquared(IVector3 point1, IVector3 point2)
        {
            return GeometryMath.GetDistanceSquared(point1, point2);
        }

        public struct CosSin
        {
            public float CosRes { get; set; }
            public float SinRes { get; set; }
        }
        public static CosSin ConvertFromAngleToCosSin(this float angle)
        {
            var cosSin = GeometryMath.ConvertFromAngleToCosSin(angle);
            return new CosSin { CosRes = cosSin.CosRes, SinRes = cosSin.SinRes };
        }

        public static List<ITriangleMeshWithColor> ConvertToTrianglesWithColor(List<TriangleMesh> triangles, string color)
        {
            var triangleswithcolor = new List<ITriangleMeshWithColor>();
            foreach (var triangle in triangles)
            {
                triangleswithcolor.Add(new TriangleMeshWithColor
                {
                    vert1 = new Vector3 { x = triangle.vert1.x, y = triangle.vert1.y, z = triangle.vert1.z },
                    vert2 = new Vector3 { x = triangle.vert2.x, y = triangle.vert2.y, z = triangle.vert2.z },
                    vert3 = new Vector3 { x = triangle.vert3.x, y = triangle.vert3.y, z = triangle.vert3.z },
                    normal1 = new Vector3 { x = triangle.normal1.x, y = triangle.normal1.y, z = triangle.normal1.z },
                    normal2 = new Vector3 { x = triangle.normal2.x, y = triangle.normal2.y, z = triangle.normal2.z },
                    normal3 = new Vector3 { x = triangle.normal3.x, y = triangle.normal3.y, z = triangle.normal3.z },
                    angle = triangle.angle,
                    Color = color
                });
            }
            return triangleswithcolor;
        }

        public static List<TriangleMeshWithColor> DeepCopyTriangles(List<TriangleMeshWithColor> originalList)
        {
            List<TriangleMeshWithColor> copiedList = new List<TriangleMeshWithColor>();

            foreach (var original in originalList)
            {
                var copy = new TriangleMeshWithColor
                {
                    Color = original.Color,
                    normal1 = new Vector3 { x = original.normal1.x, y = original.normal1.y, z = original.normal1.z },
                    normal2 = new Vector3 { x = original.normal2.x, y = original.normal2.y, z = original.normal2.z },
                    normal3 = new Vector3 { x = original.normal3.x, y = original.normal3.y, z = original.normal3.z },
                    vert1 = new Vector3 { x = original.vert1.x, y = original.vert1.y, z = original.vert1.z },
                    vert2 = new Vector3 { x = original.vert2.x, y = original.vert2.y, z = original.vert2.z },
                    vert3 = new Vector3 { x = original.vert3.x, y = original.vert3.y, z = original.vert3.z },
                    landBasedPosition = original.landBasedPosition,
                    angle = original.angle,
                    noHidden = original.noHidden
                };

                copiedList.Add(copy);
            }

            return copiedList;
        }

        public static List<_3dObject> DeepCopy3dObjects(List<_3dObject> inhabitants)
        {
            var result = new List<_3dObject>(inhabitants.Count);
            DeepCopy3dObjects(inhabitants, result);
            return result;
        }

        public static void DeepCopy3dObjects(List<_3dObject> inhabitants, List<_3dObject> result)
        {
            result.Clear();

            if (result.Capacity < inhabitants.Count)
                result.Capacity = inhabitants.Count;

            for (int i = 0; i < inhabitants.Count; i++)
            {
                result.Add(DeepCopyObject(inhabitants[i], copyCrashboxes: true));
            }
        }

        public static I3dObject DeepCopySingleObject(I3dObject original)
        {
            return original is _3dObject concrete
                ? DeepCopyObject(concrete, copyCrashboxes: false)
                : DeepCopyObject(original, copyCrashboxes: false);
        }

        private static _3dObject DeepCopyObject(_3dObject original, bool copyCrashboxes)
        {
            var copy = CreateEngineCopy(original, copyCrashboxes);
            CopyGameObjectFields(original, copy);
            return copy;
        }

        private static _3dObject DeepCopyObject(I3dObject original, bool copyCrashboxes)
        {
            var copy = CreateEngineCopy(original, copyCrashboxes);
            CopyGameObjectFields(original, copy);
            return copy;
        }

        private static _3dObject CreateEngineCopy(IRenderable3dObject original, bool copyCrashboxes)
        {
            return EngineObjectCloner.CopyRenderableObject(
                original,
                static objectId => new _3dObject { ObjectId = objectId },
                static () => new _3dObjectPart(),
                static () => new TriangleMeshWithColor(),
                CopyRequiredVector,
                copyCrashboxes);
        }

        private static void CopyGameObjectFields(I3dObject original, _3dObject copy)
        {
            copy.Movement = original.Movement;
            copy.Particles = original.Particles;
            copy.ImpactStatus = original.ImpactStatus;
            copy.Mass = original.Mass;
            copy.ParentSurface = original.ParentSurface;
            copy.SurfaceBasedId = original.SurfaceBasedId;
            copy.CrashBoxDebugMode = original.CrashBoxDebugMode;
            copy.WeaponSystems = original.WeaponSystems;
            copy.HasPowerUp = original.HasPowerUp;
            copy.PowerUpType = original.PowerUpType;
        }

        private static List<I3dObjectPart> CopyObjectParts(List<I3dObjectPart> originalParts)
        {
            return EngineObjectCloner.CopyObjectParts(
                originalParts,
                static () => new _3dObjectPart(),
                static () => new TriangleMeshWithColor(),
                CopyRequiredVector);
        }

        private static TriangleMeshWithColor CopyTriangle(ITriangleMeshWithColor triangle)
        {
            return (TriangleMeshWithColor)EngineObjectCloner.CopyTriangle(
                triangle,
                static () => new TriangleMeshWithColor(),
                CopyRequiredVector);
        }

        public static List<List<IVector3>> CopyCrashboxes(List<List<IVector3>> original)
        {
            return GeometryMath.CopyCrashboxes(original, CopyRequiredVector);
        }

        public static Vector3 GetCenterOfBox(List<Vector3> points)
        {
            var center = GeometryMath.GetCenterOfBox(points);
            return new Vector3(center.x, center.y, center.z);
        }

    }
}
