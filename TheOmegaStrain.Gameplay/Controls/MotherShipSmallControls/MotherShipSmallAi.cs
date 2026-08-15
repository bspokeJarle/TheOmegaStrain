using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonSetup;
using Domain;
using System;
using static Domain._3dSpecificsImplementations;

namespace GameAiAndControls.Controls.MotherShipSmallControls
{
    internal static class MotherShipSmallAi
    {
        // ============================
        // AI CONFIGURATION PARAMETERS
        // ============================
        // Ram cycle:
        private const float RamCycleTotalSeconds = 11.5f;
        private const float RamLockOnSecond = 5.0f;
        private const float RamWarningDurationSeconds = 1.2f;
        private const float RamChargeSpeed = 1450f;
        private const float AggressionChargeWindowBonusSeconds = 2.0f;
        // Flash pattern: on 0.0-0.25, off 0.25-0.55, on 0.55-0.80, off 0.80-1.2
        private const float Flash1Start = 0.0f;
        private const float Flash1End = 0.25f;
        private const float Flash2Start = 0.55f;
        private const float Flash2End = 0.80f;

        // -------------------------
        // AI State (per object)
        // -------------------------
        internal sealed class RamState
        {
            public DateTime RamCycleStart = DateTime.MinValue;
            public bool RamTargetLocked = false;
            public Vector3 RamTargetWorldPosition = new Vector3();
            public float RamTargetShipOffsetsY = 0f;
            public bool IsCharging = false;
        }

        internal static void UpdateRamCycle(RamState state, I3dObject theObject, DateTime now, float deltaSeconds)
        {
            if (state.RamCycleStart == DateTime.MinValue)
                state.RamCycleStart = now;

            float aggression = MotherShipDifficultySetup.GetAggression(GameState.GamePlayState.MotherShipSmallAggression);
            float elapsed = (float)(now - state.RamCycleStart).TotalSeconds * aggression;

            if (elapsed >= RamLockOnSecond && !state.RamTargetLocked)
            {
                state.RamTargetLocked = true;
                var gmp = GameState.SurfaceState.GlobalMapPosition;
                state.RamTargetWorldPosition = new Vector3 { x = gmp.x, y = gmp.y, z = gmp.z };
                state.RamTargetShipOffsetsY = GameState.ShipState?.ShipObjectOffsets?.y ?? (ScreenSetup.screenSizeY * 0.195f);
            }

            float warningStart = RamLockOnSecond;
            float warningEnd = RamLockOnSecond + RamWarningDurationSeconds;
            float chargeEnd = RamCycleTotalSeconds
                + MathF.Max(0f, aggression - 1f) * AggressionChargeWindowBonusSeconds;

            if (elapsed >= warningStart && elapsed < warningEnd && state.RamTargetLocked)
            {
                float warningElapsed = elapsed - warningStart;
                bool flashOn = (warningElapsed >= Flash1Start && warningElapsed < Flash1End)
                            || (warningElapsed >= Flash2Start && warningElapsed < Flash2End);

                GameState.GamePlayState.MotherShipRamWarningActive = flashOn;
                if (flashOn)
                    UpdateRamWarningScreenPosition(state);
            }
            else
            {
                GameState.GamePlayState.MotherShipRamWarningActive = false;
            }

            if (elapsed >= warningEnd && elapsed < chargeEnd && state.RamTargetLocked)
            {
                if (!state.IsCharging)
                    state.IsCharging = true;

                var wp = theObject.WorldPosition;
                if (wp != null)
                {
                    float dx = state.RamTargetWorldPosition.x - wp.x;
                    float dz = state.RamTargetWorldPosition.z - wp.z;
                    float dist = MathF.Sqrt(dx * dx + dz * dz);
                    float step = MotherShipDifficultySetup.ScaleTravelSpeed(RamChargeSpeed, aggression) * deltaSeconds;

                    if (dist > step)
                    {
                        float invDist = 1f / dist;
                        theObject.WorldPosition = new Vector3
                        {
                            x = wp.x + dx * invDist * step,
                            y = wp.y,
                            z = wp.z + dz * invDist * step
                        };
                    }
                    else
                    {
                        theObject.WorldPosition = new Vector3
                        {
                            x = state.RamTargetWorldPosition.x,
                            y = wp.y,
                            z = state.RamTargetWorldPosition.z
                        };
                    }
                }
            }

            if (elapsed >= chargeEnd)
            {
                state.RamCycleStart = now;
                state.RamTargetLocked = false;
                state.IsCharging = false;
                GameState.GamePlayState.MotherShipRamWarningActive = false;
            }
        }

        private static void UpdateRamWarningScreenPosition(RamState state)
        {
            var currentGmp = GameState.SurfaceState.GlobalMapPosition;
            float sx = ScreenSetup.screenSizeX / 2f
                     - (currentGmp.x - state.RamTargetWorldPosition.x);
            float sy = ScreenSetup.screenSizeY / 2f
                     - (currentGmp.y - state.RamTargetWorldPosition.y)
                     + state.RamTargetShipOffsetsY;

            GameState.GamePlayState.MotherShipRamWarningScreenX = sx;
            GameState.GamePlayState.MotherShipRamWarningScreenY = sy;
        }

        internal static void ResetState(RamState state)
        {
            state.RamCycleStart = DateTime.MinValue;
            state.RamTargetLocked = false;
            state.RamTargetWorldPosition = new Vector3();
            state.RamTargetShipOffsetsY = 0f;
            state.IsCharging = false;
            GameState.GamePlayState.MotherShipRamWarningActive = false;
        }
    }
}
