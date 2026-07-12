using SharpDX.DirectInput;
using GameAiAndControls.Input;
using Windows.Gaming.Input;

Console.WriteLine("The Omega Strain controller probe");
Console.WriteLine("Polling Windows.Gaming.Input, DirectInput and the game helper.");
Console.WriteLine("Press buttons, triggers and sticks on the Xbox controller now.");
Console.WriteLine();

const int ProbeSeconds = 8;

ProbeDirectInputDevices();
Console.WriteLine();
ProbeGameHelper();
Console.WriteLine();

var end = DateTime.UtcNow.AddSeconds(ProbeSeconds);
var sample = 0;
var seenAny = false;

while (DateTime.UtcNow < end)
{
    var pads = Gamepad.Gamepads.ToArray();
    if (pads.Length == 0)
    {
        if (sample % 10 == 0)
            Console.WriteLine($"[{sample,3}] no gamepads");
    }
    else
    {
        seenAny = true;
        for (var i = 0; i < pads.Length; i++)
        {
            var reading = pads[i].GetCurrentReading();
            Console.WriteLine(
                $"[{sample,3}] pad={i} buttons={reading.Buttons} " +
                $"lt={reading.LeftTrigger:0.000} rt={reading.RightTrigger:0.000} " +
                $"lx={reading.LeftThumbstickX:0.000} ly={reading.LeftThumbstickY:0.000} " +
                $"rx={reading.RightThumbstickX:0.000} ry={reading.RightThumbstickY:0.000}");
        }
    }

    sample++;
    Thread.Sleep(200);
}

Console.WriteLine();
Console.WriteLine(seenAny
    ? "Windows.Gaming.Input saw at least one gamepad."
    : "Windows.Gaming.Input did not see any gamepads.");

void ProbeGameHelper()
{
    Console.WriteLine($"Polling game helper XboxControllerInput for {ProbeSeconds} seconds.");
    var end = DateTime.UtcNow.AddSeconds(ProbeSeconds);
    var sample = 0;
    var seenAny = false;

    while (DateTime.UtcNow < end)
    {
        if (XboxControllerInput.TryGetState(0, out var state))
        {
            seenAny = true;
            Console.WriteLine(
                $"[GH {sample,3}] buttons=0x{state.Buttons:X4} " +
                $"lt={state.LeftTrigger} rt={state.RightTrigger} " +
                $"lx={state.LeftThumbstickX} ly={state.LeftThumbstickY} " +
                $"rx={state.RightThumbstickX} ry={state.RightThumbstickY}");
        }
        else if (sample % 10 == 0)
        {
            Console.WriteLine($"[GH {sample,3}] no controller");
        }

        sample++;
        Thread.Sleep(200);
    }

    Console.WriteLine(seenAny
        ? "Game helper saw controller input."
        : "Game helper did not see any controller input.");
}

void ProbeDirectInputDevices()
{
    using var directInput = new DirectInput();
    var deviceInstances = directInput.GetDevices(DeviceType.Gamepad, DeviceEnumerationFlags.AttachedOnly)
        .Concat(directInput.GetDevices(DeviceType.Joystick, DeviceEnumerationFlags.AttachedOnly))
        .DistinctBy(device => device.InstanceGuid)
        .ToList();

    Console.WriteLine($"DirectInput attached gamepad/joystick devices: {deviceInstances.Count}");
    foreach (var device in deviceInstances)
    {
        Console.WriteLine($"- {device.ProductName} instance={device.InstanceGuid} product={device.ProductGuid}");
    }

    if (deviceInstances.Count == 0)
    {
        Console.WriteLine("DirectInput did not enumerate any attached gamepad/joystick devices.");
        return;
    }

    var joysticks = new List<Joystick>();
    try
    {
        foreach (var device in deviceInstances)
        {
            try
            {
                var joystick = new Joystick(directInput, device.InstanceGuid);
                joystick.Properties.BufferSize = 128;
                joystick.Acquire();
                joysticks.Add(joystick);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not acquire {device.ProductName}: {ex.Message}");
            }
        }

        var end = DateTime.UtcNow.AddSeconds(ProbeSeconds);
        var sample = 0;
        while (DateTime.UtcNow < end)
        {
            for (var i = 0; i < joysticks.Count; i++)
            {
                var joystick = joysticks[i];
                try
                {
                    joystick.Poll();
                    var state = joystick.GetCurrentState();
                    var pressed = state.Buttons
                        .Select((pressed, index) => pressed ? index.ToString() : "")
                        .Where(value => value.Length > 0);
                    Console.WriteLine(
                        $"[DI {sample,3}] pad={i} x={state.X} y={state.Y} z={state.Z} " +
                        $"rx={state.RotationX} ry={state.RotationY} rz={state.RotationZ} " +
                        $"buttons=[{string.Join(",", pressed)}] pov=[{string.Join(",", state.PointOfViewControllers)}]");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DI {sample,3}] pad={i} read failed: {ex.Message}");
                }
            }

            sample++;
            Thread.Sleep(200);
        }
    }
    finally
    {
        foreach (var joystick in joysticks)
        {
            try
            {
                joystick.Unacquire();
            }
            catch
            {
            }

            joystick.Dispose();
        }
    }
}
