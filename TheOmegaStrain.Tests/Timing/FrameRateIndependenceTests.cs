using CommonUtilities.CommonGlobalState;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace _3DSpesificsUnitTests.Timing;

[TestClass]
public class FrameRateIndependenceTests
{
    [TestCleanup]
    public void Cleanup()
    {
        GameState.DeltaTime = GameState.GameplayBaselineDeltaTime;
    }

    [TestMethod]
    public void PerFrameMovement_CoversSameDistanceAt60And90Fps()
    {
        float distanceAt60 = SimulatePerFrameMovement(60, 4.5f);
        float distanceAt90 = SimulatePerFrameMovement(90, 4.5f);

        Assert.AreEqual(distanceAt90, distanceAt60, 0.001f);
    }

    [TestMethod]
    public void ShipHorizontalThrust_CoversSameDistanceAt60And90Fps()
    {
        float distanceAt60 = SimulateShipHorizontalThrust(60);
        float distanceAt90 = SimulateShipHorizontalThrust(90);
        float tolerance = MathF.Max(0.01f, MathF.Abs(distanceAt90) * 0.02f);

        Assert.AreEqual(distanceAt90, distanceAt60, tolerance);
    }

    private static float SimulatePerFrameMovement(int fps, float unitsPerFrameAt90Fps)
    {
        GameState.DeltaTime = 1f / fps;
        float position = 0f;

        for (int frame = 0; frame < fps; frame++)
            position += unitsPerFrameAt90Fps * GameState.FrameScale90;

        return position;
    }

    private static float SimulateShipHorizontalThrust(int fps)
    {
        float deltaTime = 1f / fps;
        GameState.DeltaTime = deltaTime;
        var physics = new GameAiAndControls.Physics.Physics();
        float position = 0f;

        for (int frame = 0; frame < fps; frame++)
        {
            physics.CalculateThrustForces(10f, 90f, 90f, deltaTime);
            position += physics.InertiaX * GameState.FrameScale90;
        }

        return position;
    }
}
