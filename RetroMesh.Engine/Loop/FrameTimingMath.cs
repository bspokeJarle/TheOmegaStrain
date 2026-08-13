using System;

namespace Domain
{
    public static class FrameTimingMath
    {
        public const float DefaultGameplayBaselineFps = 90f;
        public const float DefaultGameplayBaselineDeltaTime = 1f / DefaultGameplayBaselineFps;
        public const float DefaultMaxDeltaTime = 0.1f;

        public static float ClampDeltaTime(
            float deltaTime,
            float fallbackDeltaTime = DefaultGameplayBaselineDeltaTime,
            float maxDeltaTime = DefaultMaxDeltaTime)
        {
            return deltaTime > 0f
                ? Math.Clamp(deltaTime, 0f, maxDeltaTime)
                : fallbackDeltaTime;
        }

        public static float GetFrameScale(
            float deltaTime,
            float baselineFps = DefaultGameplayBaselineFps,
            float fallbackDeltaTime = DefaultGameplayBaselineDeltaTime,
            float maxDeltaTime = DefaultMaxDeltaTime)
        {
            return ClampDeltaTime(deltaTime, fallbackDeltaTime, maxDeltaTime) * baselineFps;
        }

        public static float ScaleDampingPerFrame(
            float perFrameDamping,
            float deltaTime,
            float baselineFps = DefaultGameplayBaselineFps,
            float fallbackDeltaTime = DefaultGameplayBaselineDeltaTime,
            float maxDeltaTime = DefaultMaxDeltaTime)
        {
            return MathF.Pow(
                perFrameDamping,
                GetFrameScale(deltaTime, baselineFps, fallbackDeltaTime, maxDeltaTime));
        }
    }
}
