using System;
using System.Diagnostics;

namespace RetroMesh.Engine
{
    public readonly record struct AdaptiveGcOptions(
        bool Enabled,
        int MinFrameInterval,
        int Gen1EveryAttempts,
        double MinHeadroomMs,
        double MinHeadroomPct,
        long MinAllocatedBytes)
    {
        public const int DefaultGen1EveryAttempts = 6;
        public const double DefaultMinHeadroomMs = 5.0;
        public const double DefaultMinHeadroomPct = 45.0;
        public const long DefaultMinAllocatedBytes = 24L * 1024L * 1024L;

        public static AdaptiveGcOptions CreateDefault(bool enabled, int minFrameInterval)
        {
            return new AdaptiveGcOptions(
                enabled,
                minFrameInterval,
                DefaultGen1EveryAttempts,
                DefaultMinHeadroomMs,
                DefaultMinHeadroomPct,
                DefaultMinAllocatedBytes);
        }
    }

    public readonly record struct FramePerformanceOptions(
        bool LogFrameTiming,
        double TargetFrameIntervalMs,
        int LogInterval,
        AdaptiveGcOptions AdaptiveGc);

    public readonly record struct AdaptiveGcResult(
        int Generation,
        double ElapsedMs,
        double AllocatedSinceLastMb,
        int Gen0Collections,
        int Gen1Collections);

    public readonly record struct FramePerformanceResult(
        bool ShouldLogFrameTiming,
        double ElapsedMs,
        double HeadroomMs,
        double HeadroomPct,
        double AverageFrameMs,
        double AverageHeadroomMs,
        double AverageHeadroomPct,
        long PerformanceFrameCount,
        bool ShouldLogSummary,
        AdaptiveGcResult? AdaptiveGc,
        double PreGcHeadroomPct);

    public sealed class FramePerformanceTracker
    {
        private readonly Stopwatch frameTimer = new();
        private long performanceFrameCount;
        private double averageFrameMs;
        private double averageHeadroomMs;
        private long lastAdaptiveGcFrame = long.MinValue / 2;
        private long lastAdaptiveGcAllocatedBytes = GC.GetTotalAllocatedBytes(precise: false);
        private long adaptiveGcAttempts;

        public void RestartFrame()
        {
            frameTimer.Restart();
        }

        public FramePerformanceResult? CompleteFrame(int frameIndex, FramePerformanceOptions options)
        {
            if (!frameTimer.IsRunning)
                return null;

            if (!options.LogFrameTiming && !options.AdaptiveGc.Enabled)
            {
                frameTimer.Stop();
                return null;
            }

            double preGcElapsedMs = frameTimer.Elapsed.TotalMilliseconds;
            double preGcHeadroomMs = options.TargetFrameIntervalMs - preGcElapsedMs;
            double preGcHeadroomPct = CalculateHeadroomPct(preGcHeadroomMs, options.TargetFrameIntervalMs);
            var adaptiveGc = TryRunAdaptiveGc(frameIndex, preGcHeadroomMs, preGcHeadroomPct, options.AdaptiveGc);

            frameTimer.Stop();
            double elapsedMs = frameTimer.Elapsed.TotalMilliseconds;

            if (!options.LogFrameTiming)
            {
                double headroomMs = options.TargetFrameIntervalMs - elapsedMs;
                double headroomPct = CalculateHeadroomPct(headroomMs, options.TargetFrameIntervalMs);
                return new FramePerformanceResult(
                    false,
                    elapsedMs,
                    headroomMs,
                    headroomPct,
                    averageFrameMs,
                    averageHeadroomMs,
                    CalculateHeadroomPct(averageHeadroomMs, options.TargetFrameIntervalMs),
                    performanceFrameCount,
                    false,
                    adaptiveGc,
                    preGcHeadroomPct);
            }

            return RecordCompletedFrame(
                elapsedMs,
                options.TargetFrameIntervalMs,
                options.LogInterval,
                adaptiveGc,
                preGcHeadroomPct);
        }

        public FramePerformanceResult RecordCompletedFrame(
            double elapsedMs,
            double targetFrameIntervalMs,
            int logInterval,
            AdaptiveGcResult? adaptiveGc = null,
            double? preGcHeadroomPct = null)
        {
            double headroomMs = targetFrameIntervalMs - elapsedMs;
            double headroomPct = CalculateHeadroomPct(headroomMs, targetFrameIntervalMs);

            performanceFrameCount++;
            averageFrameMs += (elapsedMs - averageFrameMs) / performanceFrameCount;
            averageHeadroomMs += (headroomMs - averageHeadroomMs) / performanceFrameCount;

            return new FramePerformanceResult(
                true,
                elapsedMs,
                headroomMs,
                headroomPct,
                averageFrameMs,
                averageHeadroomMs,
                CalculateHeadroomPct(averageHeadroomMs, targetFrameIntervalMs),
                performanceFrameCount,
                logInterval > 0 && performanceFrameCount % logInterval == 0,
                adaptiveGc,
                preGcHeadroomPct ?? headroomPct);
        }

        private AdaptiveGcResult? TryRunAdaptiveGc(
            int frameIndex,
            double headroomMs,
            double headroomPct,
            AdaptiveGcOptions options)
        {
            if (!options.Enabled)
                return null;

            if (headroomMs < options.MinHeadroomMs || headroomPct < options.MinHeadroomPct)
                return null;

            if (options.MinFrameInterval > 0 && frameIndex - lastAdaptiveGcFrame < options.MinFrameInterval)
                return null;

            long allocatedBytes = GC.GetTotalAllocatedBytes(precise: false);
            long allocatedSinceLast = allocatedBytes - lastAdaptiveGcAllocatedBytes;
            if (allocatedSinceLast < options.MinAllocatedBytes)
                return null;

            int gen1EveryAttempts = Math.Max(1, options.Gen1EveryAttempts);
            int generation = ((adaptiveGcAttempts + 1) % gen1EveryAttempts == 0)
                ? 1
                : 0;

            int gen0Before = GC.CollectionCount(0);
            int gen1Before = GC.CollectionCount(1);
            long startTicks = Stopwatch.GetTimestamp();

            GC.Collect(generation, GCCollectionMode.Optimized, blocking: false, compacting: false);

            double elapsedMs = FrameTimingMath.TicksToMilliseconds(
                Stopwatch.GetTimestamp() - startTicks,
                Stopwatch.Frequency);
            int gen0Collections = GC.CollectionCount(0) - gen0Before;
            int gen1Collections = GC.CollectionCount(1) - gen1Before;

            lastAdaptiveGcFrame = frameIndex;
            lastAdaptiveGcAllocatedBytes = allocatedBytes;
            adaptiveGcAttempts++;

            return new AdaptiveGcResult(
                generation,
                elapsedMs,
                allocatedSinceLast / (1024.0 * 1024.0),
                gen0Collections,
                gen1Collections);
        }

        private static double CalculateHeadroomPct(double headroomMs, double targetFrameIntervalMs)
        {
            return targetFrameIntervalMs > 0d
                ? headroomMs / targetFrameIntervalMs * 100.0
                : 0d;
        }
    }
}
