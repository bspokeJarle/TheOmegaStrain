using GameAiAndControls.Controls;

namespace _3DSpesificsUnitTests.Controls;

[TestClass]
public class ShipLoopTrackerTests
{
    [TestMethod]
    public void Update_WhenFullPitchRotationCompletesWithoutCollision_RecordsCleanLoop()
    {
        var tracker = new ShipLoopTracker();
        var status = tracker.Update(0f, isAirborne: true, thrustOn: true, deltaTime: 0.016f);

        for (int tilt = 10; tilt <= 360; tilt += 10)
        {
            status = tracker.Update(tilt, isAirborne: true, thrustOn: true, deltaTime: 0.016f);
        }

        Assert.IsTrue(status.Completed);
        Assert.IsFalse(status.HadCollision);
        Assert.AreEqual(1, status.CleanLoopCount);
        Assert.AreEqual(0, status.CollisionLoopCount);
    }

    [TestMethod]
    public void Update_WhenCollisionOccursDuringLoop_RecordsCollisionLoop()
    {
        var tracker = new ShipLoopTracker();
        var status = tracker.Update(0f, isAirborne: true, thrustOn: true, deltaTime: 0.016f);

        for (int tilt = 10; tilt <= 180; tilt += 10)
        {
            status = tracker.Update(tilt, isAirborne: true, thrustOn: true, deltaTime: 0.016f);
        }

        tracker.MarkCollision();

        for (int tilt = 190; tilt <= 360; tilt += 10)
        {
            status = tracker.Update(tilt, isAirborne: true, thrustOn: true, deltaTime: 0.016f);
        }

        Assert.IsTrue(status.Completed);
        Assert.IsTrue(status.HadCollision);
        Assert.AreEqual(0, status.CleanLoopCount);
        Assert.AreEqual(1, status.CollisionLoopCount);
    }

    [TestMethod]
    public void Update_WhenShipLandsBeforeCompletingLoop_CancelsActiveLoop()
    {
        var tracker = new ShipLoopTracker();

        tracker.Update(0f, isAirborne: true, thrustOn: true, deltaTime: 0.016f);
        tracker.Update(120f, isAirborne: true, thrustOn: true, deltaTime: 0.016f);
        var status = tracker.Update(120f, isAirborne: false, thrustOn: false, deltaTime: 0.016f);

        Assert.IsFalse(status.Completed);
        Assert.IsFalse(tracker.IsTracking);
        Assert.AreEqual(0, status.CleanLoopCount);
        Assert.AreEqual(0, status.CollisionLoopCount);
    }

    [TestMethod]
    public void Update_WhenRotationStartsWithoutThrust_DoesNotStartLoop()
    {
        var tracker = new ShipLoopTracker();

        tracker.Update(0f, isAirborne: true, thrustOn: false, deltaTime: 0.016f);
        var status = tracker.Update(90f, isAirborne: true, thrustOn: false, deltaTime: 0.016f);

        Assert.IsFalse(status.Completed);
        Assert.IsFalse(tracker.IsTracking);
        Assert.AreEqual(0, status.CleanLoopCount);
        Assert.AreEqual(0, status.CollisionLoopCount);
    }
}
