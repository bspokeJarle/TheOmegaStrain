using System;
using System.Diagnostics;

namespace RetroMesh.Engine
{
    public sealed class FramePhaseTimer
    {
        private readonly Func<long> timestampProvider;
        private readonly long frequency;
        private bool enabled;
        private long lastTicks;

        public FramePhaseTimer()
            : this(Stopwatch.GetTimestamp, Stopwatch.Frequency)
        {
        }

        public FramePhaseTimer(Func<long> timestampProvider, long frequency)
        {
            this.timestampProvider = timestampProvider ?? throw new ArgumentNullException(nameof(timestampProvider));
            this.frequency = frequency;
        }

        public void Restart(bool enabled)
        {
            this.enabled = enabled;
            lastTicks = enabled ? timestampProvider() : 0;
        }

        public double Mark()
        {
            if (!enabled)
                return 0d;

            long now = timestampProvider();
            double elapsedMs = FrameTimingMath.TicksToMilliseconds(now - lastTicks, frequency);
            lastTicks = now;
            return elapsedMs;
        }
    }
}
