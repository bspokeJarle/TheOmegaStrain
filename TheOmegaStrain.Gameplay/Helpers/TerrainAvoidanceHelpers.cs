using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonSetup;
using Domain;
using System;
using System.Collections.Generic;
using static Domain._3dSpecificsImplementations;

namespace GameAiAndControls.Helpers
{
    public static class TerrainAvoidanceHelpers
    {
        private sealed class RecoveryState
        {
            public float RemainingSeconds { get; set; }
            public float DurationSeconds { get; set; }
            public float LiftSpeed { get; set; }
            public float HorizontalSpeedX { get; set; }
            public float HorizontalSpeedZ { get; set; }
        }

        private static readonly Dictionary<int, RecoveryState> RecoveryStates = new();

        public static bool IsTerrainRecoveryActive(I3dObject obj)
        {
            return obj != null && RecoveryStates.ContainsKey(obj.ObjectId);
        }

        public static bool TryStartTerrainRecovery(I3dObject obj)
        {
            if (obj?.ImpactStatus?.HasCrashed != true)
                return false;

            if (!CanReactToTerrain(obj, obj.ImpactStatus.ObjectName))
                return false;

            var tuning = GetTuning(obj.ObjectName);
            var push = GetPushDirection(obj);
            StartRecovery(obj, tuning.durationSeconds, tuning.liftSpeed, tuning.horizontalSpeed, push);

            obj.ImpactStatus.HasCrashed = false;
            obj.ImpactStatus.ObjectName = string.Empty;
            obj.ImpactStatus.ImpactDirection = null;
            obj.ImpactStatus.CrashBoxName = null;
            return true;
        }

        public static bool TryStartTerrainProximityRecovery(
            I3dObject obj,
            string? obstacleObjectName,
            IVector3? aiCenter,
            IVector3? obstacleCenter)
        {
            if (obj == null)
                return false;

            if (!TerrainAvoidanceSetup.IsHeavyAvoidanceAi(obj.ObjectName))
                return false;

            if (!TerrainAvoidanceSetup.IsProactiveTerrainObstacle(obstacleObjectName))
                return false;

            if (obj.ImpactStatus?.HasCrashed == true &&
                !TerrainAvoidanceSetup.IsTerrainObstacle(obj.ImpactStatus.ObjectName))
            {
                return false;
            }

            if (!CanReactToTerrain(obj, obstacleObjectName))
                return false;

            if (RecoveryStates.ContainsKey(obj.ObjectId))
                return true;

            var tuning = GetTuning(obj.ObjectName);
            var push = GetPushDirectionAwayFromObstacle(obj, aiCenter, obstacleCenter);
            StartRecovery(obj, tuning.durationSeconds, tuning.liftSpeed, tuning.horizontalSpeed, push);

            if (obj.ImpactStatus?.HasCrashed == true)
            {
                obj.ImpactStatus.HasCrashed = false;
                obj.ImpactStatus.ObjectName = string.Empty;
                obj.ImpactStatus.ImpactDirection = null;
                obj.ImpactStatus.CrashBoxName = null;
            }

            return true;
        }

        public static bool ApplyTerrainRecovery(I3dObject obj, float deltaSeconds)
        {
            if (obj == null || !RecoveryStates.TryGetValue(obj.ObjectId, out var state))
                return false;

            if (deltaSeconds <= 0f)
                deltaSeconds = GameState.GameplayBaselineDeltaTime;

            deltaSeconds = Math.Clamp(deltaSeconds, 0f, 0.1f);
            float intensity = state.DurationSeconds <= 0f
                ? 1f
                : Math.Clamp(state.RemainingSeconds / state.DurationSeconds, 0f, 1f);

            if (obj.ObjectOffsets != null)
            {
                obj.ObjectOffsets = new Vector3
                {
                    x = obj.ObjectOffsets.x,
                    y = obj.ObjectOffsets.y - state.LiftSpeed * intensity * deltaSeconds,
                    z = obj.ObjectOffsets.z
                };
            }

            if (obj.WorldPosition != null)
            {
                obj.WorldPosition = new Vector3
                {
                    x = obj.WorldPosition.x + state.HorizontalSpeedX * intensity * deltaSeconds,
                    y = obj.WorldPosition.y,
                    z = obj.WorldPosition.z + state.HorizontalSpeedZ * intensity * deltaSeconds
                };
            }

            state.RemainingSeconds -= deltaSeconds;
            if (state.RemainingSeconds <= 0f)
                RecoveryStates.Remove(obj.ObjectId);

            return true;
        }

        public static bool CanReactToTerrain(I3dObject obj, string? contactObjectName)
        {
            if (obj == null)
                return false;

            if (!TerrainAvoidanceSetup.IsAvoidanceCapableAi(obj.ObjectName))
                return false;

            if (!TerrainAvoidanceSetup.IsTerrainObstacle(contactObjectName))
                return false;

            var aiObjects = GameState.SurfaceState?.AiObjects;
            if (aiObjects == null)
                return false;

            for (int i = 0; i < aiObjects.Count; i++)
            {
                if (aiObjects[i].ObjectId == obj.ObjectId)
                    return true;
            }

            return false;
        }

        private static (float durationSeconds, float liftSpeed, float horizontalSpeed) GetTuning(string? objectName)
        {
            if (TerrainAvoidanceSetup.IsHeavyAvoidanceAi(objectName))
            {
                return (
                    TerrainAvoidanceSetup.HeavyRecoveryDurationSeconds,
                    TerrainAvoidanceSetup.HeavyLiftSpeed,
                    TerrainAvoidanceSetup.HeavyHorizontalSpeed);
            }

            return (
                TerrainAvoidanceSetup.DefaultRecoveryDurationSeconds,
                TerrainAvoidanceSetup.DefaultLiftSpeed,
                TerrainAvoidanceSetup.DefaultHorizontalSpeed);
        }

        private static void StartRecovery(
            I3dObject obj,
            float durationSeconds,
            float liftSpeed,
            float horizontalSpeed,
            Vector3 push)
        {
            if (!RecoveryStates.TryGetValue(obj.ObjectId, out var state))
            {
                state = new RecoveryState();
                RecoveryStates[obj.ObjectId] = state;
            }

            state.DurationSeconds = durationSeconds;
            state.RemainingSeconds = MathF.Max(state.RemainingSeconds, durationSeconds);
            state.LiftSpeed = MathF.Max(state.LiftSpeed, liftSpeed);
            state.HorizontalSpeedX = push.x * horizontalSpeed;
            state.HorizontalSpeedZ = push.z * horizontalSpeed;
        }

        private static Vector3 GetPushDirection(I3dObject obj)
        {
            float side = obj.ObjectId % 2 == 0 ? 1f : -1f;

            Vector3 push = obj.ImpactStatus?.ImpactDirection switch
            {
                ImpactDirection.Left => new Vector3 { x = -1f, y = 0f, z = 0.2f * side },
                ImpactDirection.Right => new Vector3 { x = 1f, y = 0f, z = 0.2f * side },
                ImpactDirection.Bottom => new Vector3 { x = 0.35f * side, y = 0f, z = -1f },
                _ => GetFallbackPushFromMap(obj, side)
            };

            float length = MathF.Sqrt(push.x * push.x + push.z * push.z);
            if (length <= 0.001f)
                return new Vector3 { x = side, y = 0f, z = 0f };

            return new Vector3
            {
                x = push.x / length,
                y = 0f,
                z = push.z / length
            };
        }

        private static Vector3 GetFallbackPushFromMap(I3dObject obj, float side)
        {
            var worldPosition = obj.WorldPosition;
            if (worldPosition == null)
                return new Vector3 { x = side, y = 0f, z = 0.35f * side };

            var mapPosition = GameState.SurfaceState.GlobalMapPosition;
            float dx = worldPosition.x - mapPosition.x;
            float dz = worldPosition.z - mapPosition.z;

            if (MathF.Abs(dx) < 1f && MathF.Abs(dz) < 1f)
                return new Vector3 { x = side, y = 0f, z = 0.35f * side };

            return new Vector3 { x = dx, y = 0f, z = dz };
        }

        private static Vector3 GetPushDirectionAwayFromObstacle(
            I3dObject obj,
            IVector3? aiCenter,
            IVector3? obstacleCenter)
        {
            if (aiCenter == null || obstacleCenter == null)
                return GetFallbackPushFromMap(obj, obj.ObjectId % 2 == 0 ? 1f : -1f);

            float awayX = aiCenter.x - obstacleCenter.x;
            float awayZ = aiCenter.z - obstacleCenter.z;
            float awayLength = MathF.Sqrt(awayX * awayX + awayZ * awayZ);

            if (awayLength <= 0.001f)
                return GetFallbackPushFromMap(obj, obj.ObjectId % 2 == 0 ? 1f : -1f);

            awayX /= awayLength;
            awayZ /= awayLength;

            float side = obj.ObjectId % 2 == 0 ? 1f : -1f;
            float sideX = -awayZ * side;
            float sideZ = awayX * side;

            var push = new Vector3
            {
                x = awayX * 0.7f + sideX * 0.3f,
                y = 0f,
                z = awayZ * 0.7f + sideZ * 0.3f
            };

            float length = MathF.Sqrt(push.x * push.x + push.z * push.z);
            if (length <= 0.001f)
                return new Vector3 { x = awayX, y = 0f, z = awayZ };

            return new Vector3
            {
                x = push.x / length,
                y = 0f,
                z = push.z / length
            };
        }
    }
}
