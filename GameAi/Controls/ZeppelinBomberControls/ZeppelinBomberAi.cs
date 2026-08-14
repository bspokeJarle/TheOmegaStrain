using CommonUtilities.OmegaEngineAdapters;
using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonSetup;
using Domain;
using System;
using static Domain._3dSpecificsImplementations;

namespace GameAiAndControls.Controls.ZeppelinBomberControls
{
    internal static class ZeppelinBomberAi
    {
        // AI configuration
        private const float WanderSpeed = 120f;
        private const float ApproachSpeed = 200f;
        private const float ApproachRadius = 10_000f;
        private const float BombDropRadius = 3000f;
        private const float BombDropIntervalSeconds = 4f;
        private const float DirectionChangeCooldownSeconds = 5f;
        private const float RetargetCooldownSeconds = 20f;

        // Only one bomber may track the ship at a time
        private static BomberState? _activeTracker = null;
        private static readonly object _trackerLock = new();

        internal sealed class BomberState
        {
            public bool IsInitialized;
            public float DirectionX;
            public float DirectionZ;
            public float CurrentSpeed = WanderSpeed;
            public DateTime LastDirectionChange = DateTime.MinValue;
            public DateTime LastRetarget = DateTime.MinValue;
            public float BombTimer;
            public bool IsBombing;
            public bool IsTrackingShip;
        }

        internal static void Initialize(BomberState state)
        {
            if (state.IsInitialized) return;

            var rnd = new Random();
            float angle = (float)(rnd.NextDouble() * 2 * Math.PI);
            state.DirectionX = MathF.Cos(angle);
            state.DirectionZ = MathF.Sin(angle);
            state.CurrentSpeed = WanderSpeed;
            state.IsInitialized = true;
        }

        internal static void UpdateMovement(BomberState state, Vector3 position, float deltaSeconds)
        {
            if (deltaSeconds <= 0) return;

            var shipPos = GameState.ShipState?.ShipCrashCenterWorldPosition;
            float distanceToShip = float.MaxValue;

            if (shipPos != null)
            {
                float dx = position.x - shipPos.x;
                float dz = position.z - shipPos.z;
                distanceToShip = MathF.Sqrt(dx * dx + dz * dz);
            }

            // Check if this bomber can track the ship (only one at a time)
            bool canTrack = false;
            if (shipPos != null && distanceToShip < ApproachRadius)
            {
                lock (_trackerLock)
                {
                    if (state.IsTrackingShip)
                    {
                        canTrack = true;
                    }
                    else if (_activeTracker == null || !_activeTracker.IsTrackingShip)
                    {
                        _activeTracker = state;
                        state.IsTrackingShip = true;
                        canTrack = true;
                    }
                }
            }
            else if (state.IsTrackingShip)
            {
                ReleaseTracker(state);
            }

            if (canTrack)
            {
                state.CurrentSpeed = ApproachSpeed;

                // Retarget toward the ship every 20 seconds, not every frame
                var now = DateTime.Now;
                if (state.LastRetarget == DateTime.MinValue ||
                    (now - state.LastRetarget).TotalSeconds >= RetargetCooldownSeconds)
                {
                    float dx = shipPos.x - position.x;
                    float dz = shipPos.z - position.z;
                    float len = MathF.Sqrt(dx * dx + dz * dz);
                    if (len > 1f)
                    {
                        state.DirectionX = dx / len;
                        state.DirectionZ = dz / len;
                    }
                    state.LastRetarget = now;
                }

                // Bombing run when within drop radius
                state.IsBombing = distanceToShip < BombDropRadius;
            }
            else
            {
                // Random wander
                state.CurrentSpeed = WanderSpeed;
                state.IsBombing = false;

                var now = DateTime.Now;
                if ((now - state.LastDirectionChange).TotalSeconds > DirectionChangeCooldownSeconds)
                {
                    var rnd = new Random();
                    float angle = (float)(rnd.NextDouble() * 2 * Math.PI);
                    state.DirectionX = MathF.Cos(angle);
                    state.DirectionZ = MathF.Sin(angle);
                    state.LastDirectionChange = now;
                }
            }

            // Move position
            position.x += state.DirectionX * state.CurrentSpeed * deltaSeconds;
            position.z += state.DirectionZ * state.CurrentSpeed * deltaSeconds;
        }

        internal static bool ShouldDropBomb(BomberState state, float deltaSeconds)
        {
            if (!state.IsBombing) return false;

            state.BombTimer += deltaSeconds;
            if (state.BombTimer >= BombDropIntervalSeconds)
            {
                state.BombTimer = 0f;
                return true;
            }
            return false;
        }

        internal static void ReleaseTracker(BomberState state)
        {
            lock (_trackerLock)
            {
                state.IsTrackingShip = false;
                if (ReferenceEquals(_activeTracker, state))
                    _activeTracker = null;
            }
        }

        internal static void ResetState(BomberState state)
        {
            ReleaseTracker(state);
            state.IsInitialized = false;
            state.DirectionX = 0f;
            state.DirectionZ = 0f;
            state.CurrentSpeed = WanderSpeed;
            state.LastDirectionChange = DateTime.MinValue;
            state.LastRetarget = DateTime.MinValue;
            state.BombTimer = 0f;
            state.IsBombing = false;
        }
    }
}
