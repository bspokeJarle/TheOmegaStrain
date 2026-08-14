namespace _3DSpesificsUnitTests.Engine;

[TestClass]
public class EngineFrameRuntimeTests
{
    [TestMethod]
    public void FramePhaseTimer_MarksElapsedMillisecondsAndAdvances()
    {
        long ticks = 100;
        var timer = new FramePhaseTimer(() => ticks, frequency: 1000);

        timer.Restart(enabled: true);

        ticks = 116;
        Assert.AreEqual(16d, timer.Mark(), 0.001d);

        ticks = 125;
        Assert.AreEqual(9d, timer.Mark(), 0.001d);
    }

    [TestMethod]
    public void FramePhaseTimer_ReturnsZeroWhenDisabled()
    {
        long ticks = 100;
        var timer = new FramePhaseTimer(() => ticks, frequency: 1000);

        timer.Restart(enabled: false);
        ticks = 250;

        Assert.AreEqual(0d, timer.Mark(), 0.001d);
    }

    [TestMethod]
    public void FramePerformanceTracker_RecordCompletedFrame_UpdatesAveragesAndSummaryInterval()
    {
        var tracker = new FramePerformanceTracker();

        var first = tracker.RecordCompletedFrame(
            elapsedMs: 4d,
            targetFrameIntervalMs: 10d,
            logInterval: 2);

        Assert.AreEqual(4d, first.AverageFrameMs, 0.001d);
        Assert.AreEqual(6d, first.AverageHeadroomMs, 0.001d);
        Assert.AreEqual(60d, first.AverageHeadroomPct, 0.001d);
        Assert.IsFalse(first.ShouldLogSummary);

        var second = tracker.RecordCompletedFrame(
            elapsedMs: 8d,
            targetFrameIntervalMs: 10d,
            logInterval: 2);

        Assert.AreEqual(6d, second.AverageFrameMs, 0.001d);
        Assert.AreEqual(4d, second.AverageHeadroomMs, 0.001d);
        Assert.AreEqual(40d, second.AverageHeadroomPct, 0.001d);
        Assert.AreEqual(2, second.PerformanceFrameCount);
        Assert.IsTrue(second.ShouldLogSummary);
    }

    [TestMethod]
    public void ListCapacityHelper_EnsureCapacity_OnlyGrows()
    {
        var list = new List<int>(capacity: 8);

        ListCapacityHelper.EnsureCapacity(list, 4);
        Assert.AreEqual(8, list.Capacity);

        ListCapacityHelper.EnsureCapacity(list, 16);
        Assert.IsTrue(list.Capacity >= 16);
    }

    [TestMethod]
    public void FrameTimingMath_TicksToMilliseconds_UsesProvidedFrequency()
    {
        Assert.AreEqual(16d, FrameTimingMath.TicksToMilliseconds(16, 1000), 0.001d);
        Assert.AreEqual(0d, FrameTimingMath.TicksToMilliseconds(16, 0), 0.001d);
    }
}
