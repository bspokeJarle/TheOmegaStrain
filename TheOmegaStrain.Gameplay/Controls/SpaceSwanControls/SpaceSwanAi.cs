using CommonUtilities.CommonGlobalState;
using Domain;
using System;
using static Domain._3dSpecificsImplementations;

namespace GameAiAndControls.Controls.SpaceSwanControls
{
    internal static class SpaceSwanAi
    {
        internal const float BaseFlySpeed = 150f;
        private const float SpookedFlySpeed = 300f;
        private const float ShipSpookRadius = 700f;
        private const float SpookCooldownSec = 3f;
        private const float SpeedDecayRate = 30f;

        internal sealed class SwanState
        {
            public bool IsInitialized = false;
            public float DirectionX = 0f;
            public float DirectionZ = 0f;
            public float CurrentSpeed = BaseFlySpeed;
            public DateTime LastSpookTime = DateTime.MinValue;
        }

        internal static void Initialize(SwanState state, I3dObject theObject)
        {
            if (state.IsInitialized) return;

            var rnd = new Random();
            float angle = (float)(rnd.NextDouble() * 2 * Math.PI);
            state.DirectionX = MathF.Cos(angle);
            state.DirectionZ = MathF.Sin(angle);
            state.CurrentSpeed = BaseFlySpeed;
            state.IsInitialized = true;
        }

        internal static void UpdateMovement(SwanState state, Vector3 position, float deltaSeconds)
        {
            if (deltaSeconds <= 0) return;

            // Check ship proximity for spooking
            var shipPos = GameState.ShipState?.ShipCrashCenterWorldPosition;
            if (shipPos != null)
            {
                float dx = position.x - shipPos.x;
                float dz = position.z - shipPos.z;
                float distSq = dx * dx + dz * dz;

                if (distSq < ShipSpookRadius * ShipSpookRadius)
                {
                    var now = DateTime.Now;
                    if ((now - state.LastSpookTime).TotalSeconds > SpookCooldownSec)
                    {
                        var rnd = new Random();
                        float angle = (float)(rnd.NextDouble() * 2 * Math.PI);
                        state.DirectionX = MathF.Cos(angle);
                        state.DirectionZ = MathF.Sin(angle);
                        state.CurrentSpeed = SpookedFlySpeed;
                        state.LastSpookTime = now;
                    }
                }
            }

            // Decay speed back to base
            if (state.CurrentSpeed > BaseFlySpeed)
            {
                state.CurrentSpeed -= SpeedDecayRate * deltaSeconds;
                if (state.CurrentSpeed < BaseFlySpeed)
                    state.CurrentSpeed = BaseFlySpeed;
            }

            // Move position
            position.x += state.DirectionX * state.CurrentSpeed * deltaSeconds;
            position.z += state.DirectionZ * state.CurrentSpeed * deltaSeconds;
        }

        internal static void ResetState(SwanState state)
        {
            state.IsInitialized = false;
            state.DirectionX = 0f;
            state.DirectionZ = 0f;
            state.CurrentSpeed = BaseFlySpeed;
            state.LastSpookTime = DateTime.MinValue;
        }
    }
}
