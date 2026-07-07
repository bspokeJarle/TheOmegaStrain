using CommonUtilities.CommonGlobalState.States;
using GameAiAndControls.Input;

namespace _3DSpesificsUnitTests.Controls;

[TestClass]
public class XboxControllerInputTests
{
    [TestMethod]
    public void IsControlPressed_MapsButtonsAndTriggers()
    {
        var state = new XboxControllerSnapshot(
            buttons: 0x1000,
            leftTrigger: 0,
            rightTrigger: 80,
            leftThumbstickX: 0,
            leftThumbstickY: 0,
            rightThumbstickX: 0,
            rightThumbstickY: 0);

        Assert.IsTrue(XboxControllerInput.IsControlPressed(state, XboxControlButton.A));
        Assert.IsTrue(XboxControllerInput.IsControlPressed(state, XboxControlButton.RightTrigger));
        Assert.IsFalse(XboxControllerInput.IsControlPressed(state, XboxControlButton.B));
    }

    [TestMethod]
    public void IsControlPressed_MapsStickDirectionsWithDeadZone()
    {
        var state = new XboxControllerSnapshot(
            buttons: 0,
            leftTrigger: 0,
            rightTrigger: 0,
            leftThumbstickX: -12000,
            leftThumbstickY: 9000,
            rightThumbstickX: 0,
            rightThumbstickY: 0);

        Assert.IsTrue(XboxControllerInput.IsControlPressed(state, XboxControlButton.LeftStickLeft));
        Assert.IsTrue(XboxControllerInput.IsControlPressed(state, XboxControlButton.LeftStickUp));
        Assert.IsFalse(XboxControllerInput.IsControlPressed(state, XboxControlButton.LeftStickRight));
        Assert.IsFalse(XboxControllerInput.IsControlPressed(state, XboxControlButton.LeftStickDown));
    }

    [TestMethod]
    public void GetControlStrength_MapsStickDirectionsAnalogBeyondDeadZone()
    {
        var halfTilt = new XboxControllerSnapshot(
            buttons: 0,
            leftTrigger: 0,
            rightTrigger: 0,
            leftThumbstickX: 20000,
            leftThumbstickY: 0,
            rightThumbstickX: 0,
            rightThumbstickY: 0);

        var fullTilt = new XboxControllerSnapshot(
            buttons: 0,
            leftTrigger: 0,
            rightTrigger: 0,
            leftThumbstickX: short.MaxValue,
            leftThumbstickY: 0,
            rightThumbstickX: 0,
            rightThumbstickY: 0);

        float halfStrength = XboxControllerInput.GetControlStrength(halfTilt, XboxControlButton.LeftStickRight);
        float fullStrength = XboxControllerInput.GetControlStrength(fullTilt, XboxControlButton.LeftStickRight);

        Assert.IsTrue(halfStrength > 0f);
        Assert.IsTrue(halfStrength < 1f);
        Assert.AreEqual(1f, fullStrength, 0.001f);
        Assert.AreEqual(0f, XboxControllerInput.GetControlStrength(halfTilt, XboxControlButton.LeftStickLeft), 0.001f);
    }

    [TestMethod]
    public void GetControlStrength_IgnoresStickDirectionsInsideDeadZone()
    {
        var state = new XboxControllerSnapshot(
            buttons: 0,
            leftTrigger: 0,
            rightTrigger: 0,
            leftThumbstickX: 5000,
            leftThumbstickY: -5000,
            rightThumbstickX: 0,
            rightThumbstickY: 0);

        Assert.AreEqual(0f, XboxControllerInput.GetControlStrength(state, XboxControlButton.LeftStickRight), 0.001f);
        Assert.AreEqual(0f, XboxControllerInput.GetControlStrength(state, XboxControlButton.LeftStickDown), 0.001f);
    }

    [TestMethod]
    public void GetControlStrength_MapsButtonsAndTriggers()
    {
        var state = new XboxControllerSnapshot(
            buttons: 0x1000,
            leftTrigger: 0,
            rightTrigger: 80,
            leftThumbstickX: 0,
            leftThumbstickY: 0,
            rightThumbstickX: 0,
            rightThumbstickY: 0);

        Assert.AreEqual(1f, XboxControllerInput.GetControlStrength(state, XboxControlButton.A), 0.001f);
        Assert.IsTrue(XboxControllerInput.GetControlStrength(state, XboxControlButton.RightTrigger) > 0f);
        Assert.AreEqual(0f, XboxControllerInput.GetControlStrength(state, XboxControlButton.B), 0.001f);
    }

    [TestMethod]
    public void HasButtonInput_IgnoresStickDrift()
    {
        var state = new XboxControllerSnapshot(
            buttons: 0,
            leftTrigger: 0,
            rightTrigger: 0,
            leftThumbstickX: 20000,
            leftThumbstickY: 20000,
            rightThumbstickX: 0,
            rightThumbstickY: 0);

        Assert.IsFalse(XboxControllerInput.HasButtonInput(state));
    }
}
