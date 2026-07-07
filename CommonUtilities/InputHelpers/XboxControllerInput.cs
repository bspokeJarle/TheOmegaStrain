using CommonUtilities.CommonGlobalState.States;
using SharpDX.DirectInput;
using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace GameAiAndControls.Input
{
    public readonly struct XboxControllerSnapshot
    {
        public XboxControllerSnapshot(
            ushort buttons,
            byte leftTrigger,
            byte rightTrigger,
            short leftThumbstickX,
            short leftThumbstickY,
            short rightThumbstickX,
            short rightThumbstickY)
        {
            Buttons = buttons;
            LeftTrigger = leftTrigger;
            RightTrigger = rightTrigger;
            LeftThumbstickX = leftThumbstickX;
            LeftThumbstickY = leftThumbstickY;
            RightThumbstickX = rightThumbstickX;
            RightThumbstickY = rightThumbstickY;
        }

        public ushort Buttons { get; }
        public byte LeftTrigger { get; }
        public byte RightTrigger { get; }
        public short LeftThumbstickX { get; }
        public short LeftThumbstickY { get; }
        public short RightThumbstickX { get; }
        public short RightThumbstickY { get; }
    }

    public static class XboxControllerInput
    {
        private const uint XInputSuccess = 0;
        private const byte TriggerThreshold = 30;
        private const short StickDeadZone = 7849;
        private const ushort DpadUp = 0x0001;
        private const ushort DpadDown = 0x0002;
        private const ushort DpadLeft = 0x0004;
        private const ushort DpadRight = 0x0008;
        private const ushort Start = 0x0010;
        private const ushort Back = 0x0020;
        private const ushort LeftThumb = 0x0040;
        private const ushort RightThumb = 0x0080;
        private const ushort LeftShoulder = 0x0100;
        private const ushort RightShoulder = 0x0200;
        private const ushort A = 0x1000;
        private const ushort B = 0x2000;
        private const ushort X = 0x4000;
        private const ushort Y = 0x8000;
        private static bool _xinput14Unavailable = false;
        private static bool _xinput910Unavailable = false;
        private static readonly object DirectInputLock = new();
        private static DirectInput? _directInput;
        private static Joystick? _directInputJoystick;
        private static DateTime _nextDirectInputScanUtc = DateTime.MinValue;

        public static bool TryGetState(int controllerIndex, out XboxControllerSnapshot snapshot)
        {
            if (TryGetRawState((uint)Math.Clamp(controllerIndex, 0, 3), out var state))
            {
                snapshot = new XboxControllerSnapshot(
                    state.Gamepad.Buttons,
                    state.Gamepad.LeftTrigger,
                    state.Gamepad.RightTrigger,
                    state.Gamepad.ThumbLX,
                    state.Gamepad.ThumbLY,
                    state.Gamepad.ThumbRX,
                    state.Gamepad.ThumbRY);
                return true;
            }

            return TryGetDirectInputState(out snapshot);
        }

        public static bool HasButtonInput(XboxControllerSnapshot state) =>
            state.Buttons != 0 ||
            state.LeftTrigger > TriggerThreshold ||
            state.RightTrigger > TriggerThreshold;

        public static float GetControlStrength(XboxControllerSnapshot state, XboxControlButton button)
        {
            return button switch
            {
                XboxControlButton.A => GetButtonStrength(state, A),
                XboxControlButton.B => GetButtonStrength(state, B),
                XboxControlButton.X => GetButtonStrength(state, X),
                XboxControlButton.Y => GetButtonStrength(state, Y),
                XboxControlButton.LeftShoulder => GetButtonStrength(state, LeftShoulder),
                XboxControlButton.RightShoulder => GetButtonStrength(state, RightShoulder),
                XboxControlButton.LeftTrigger => GetTriggerStrength(state.LeftTrigger),
                XboxControlButton.RightTrigger => GetTriggerStrength(state.RightTrigger),
                XboxControlButton.DPadUp => GetButtonStrength(state, DpadUp),
                XboxControlButton.DPadDown => GetButtonStrength(state, DpadDown),
                XboxControlButton.DPadLeft => GetButtonStrength(state, DpadLeft),
                XboxControlButton.DPadRight => GetButtonStrength(state, DpadRight),
                XboxControlButton.LeftStick => GetButtonStrength(state, LeftThumb),
                XboxControlButton.RightStick => GetButtonStrength(state, RightThumb),
                XboxControlButton.View => GetButtonStrength(state, Back),
                XboxControlButton.Menu => GetButtonStrength(state, Start),
                XboxControlButton.LeftStickUp => GetPositiveAxisStrength(state.LeftThumbstickY),
                XboxControlButton.LeftStickDown => GetNegativeAxisStrength(state.LeftThumbstickY),
                XboxControlButton.LeftStickLeft => GetNegativeAxisStrength(state.LeftThumbstickX),
                XboxControlButton.LeftStickRight => GetPositiveAxisStrength(state.LeftThumbstickX),
                XboxControlButton.RightStickUp => GetPositiveAxisStrength(state.RightThumbstickY),
                XboxControlButton.RightStickDown => GetNegativeAxisStrength(state.RightThumbstickY),
                XboxControlButton.RightStickLeft => GetNegativeAxisStrength(state.RightThumbstickX),
                XboxControlButton.RightStickRight => GetPositiveAxisStrength(state.RightThumbstickX),
                _ => 0f
            };
        }

        public static bool IsControlPressed(XboxControllerSnapshot state, XboxControlButton button)
        {
            return GetControlStrength(state, button) > 0f;
        }

        private static bool TryGetRawState(uint controllerIndex, out XInputState state)
        {
            if (!_xinput14Unavailable)
            {
                try
                {
                    return XInputGetState14(controllerIndex, out state) == XInputSuccess;
                }
                catch (DllNotFoundException)
                {
                    _xinput14Unavailable = true;
                }
                catch (EntryPointNotFoundException)
                {
                    _xinput14Unavailable = true;
                }
            }

            if (!_xinput910Unavailable)
            {
                try
                {
                    return XInputGetState910(controllerIndex, out state) == XInputSuccess;
                }
                catch (DllNotFoundException)
                {
                    _xinput910Unavailable = true;
                }
                catch (EntryPointNotFoundException)
                {
                    _xinput910Unavailable = true;
                }
            }

            state = default;
            return false;
        }

        private static bool TryGetDirectInputState(out XboxControllerSnapshot snapshot)
        {
            lock (DirectInputLock)
            {
                if (!TryEnsureDirectInputJoystick())
                {
                    snapshot = default;
                    return false;
                }

                try
                {
                    _directInputJoystick!.Poll();
                    var state = _directInputJoystick.GetCurrentState();
                    snapshot = ToSnapshot(state);
                    return true;
                }
                catch
                {
                    ResetDirectInput();
                    snapshot = default;
                    return false;
                }
            }
        }

        private static bool TryEnsureDirectInputJoystick()
        {
            if (_directInputJoystick != null)
                return true;

            var now = DateTime.UtcNow;
            if (now < _nextDirectInputScanUtc)
                return false;

            _nextDirectInputScanUtc = now.AddSeconds(2);

            try
            {
                _directInput ??= new DirectInput();
                var devices = _directInput.GetDevices(DeviceType.Gamepad, DeviceEnumerationFlags.AttachedOnly)
                    .Concat(_directInput.GetDevices(DeviceType.Joystick, DeviceEnumerationFlags.AttachedOnly))
                    .DistinctBy(device => device.InstanceGuid)
                    .OrderByDescending(IsLikelyXboxDevice)
                    .ToList();

                var device = devices.FirstOrDefault();
                if (device == null)
                    return false;

                _directInputJoystick = new Joystick(_directInput, device.InstanceGuid);
                _directInputJoystick.Properties.BufferSize = 16;
                _directInputJoystick.Acquire();
                return true;
            }
            catch
            {
                ResetDirectInput();
                return false;
            }
        }

        private static void ResetDirectInput()
        {
            try
            {
                _directInputJoystick?.Unacquire();
            }
            catch
            {
            }

            _directInputJoystick?.Dispose();
            _directInputJoystick = null;
        }

        private static bool IsLikelyXboxDevice(DeviceInstance device) =>
            ContainsIgnoreCase(device.ProductName, "xbox") ||
            ContainsIgnoreCase(device.ProductName, "xinput") ||
            device.ProductGuid.ToString().StartsWith("0b13045e", StringComparison.OrdinalIgnoreCase);

        private static XboxControllerSnapshot ToSnapshot(JoystickState state)
        {
            ushort buttons = 0;
            var directButtons = state.Buttons ?? Array.Empty<bool>();

            AddButtonMask(directButtons, 0, A, ref buttons);
            AddButtonMask(directButtons, 1, B, ref buttons);
            AddButtonMask(directButtons, 2, X, ref buttons);
            AddButtonMask(directButtons, 3, Y, ref buttons);
            AddButtonMask(directButtons, 4, LeftShoulder, ref buttons);
            AddButtonMask(directButtons, 5, RightShoulder, ref buttons);
            AddButtonMask(directButtons, 6, Back, ref buttons);
            AddButtonMask(directButtons, 7, Start, ref buttons);
            AddButtonMask(directButtons, 8, LeftThumb, ref buttons);
            AddButtonMask(directButtons, 9, RightThumb, ref buttons);

            AddPovMask(state.PointOfViewControllers?.FirstOrDefault() ?? -1, ref buttons);

            var leftTrigger = Math.Max(ToNegativeTrigger(state.Z), ToNegativeTrigger(state.RotationZ));
            var rightTrigger = Math.Max(ToPositiveTrigger(state.Z), ToPositiveTrigger(state.RotationZ));

            return new XboxControllerSnapshot(
                buttons,
                leftTrigger,
                rightTrigger,
                ToSignedAxis(state.X, invert: false),
                ToSignedAxis(state.Y, invert: true),
                HasAxisValue(state.RotationX) ? ToSignedAxis(state.RotationX, invert: false) : (short)0,
                HasAxisValue(state.RotationY) ? ToSignedAxis(state.RotationY, invert: true) : (short)0);
        }

        private static void AddButtonMask(bool[] buttons, int index, ushort mask, ref ushort output)
        {
            if (index >= 0 && index < buttons.Length && buttons[index])
                output |= mask;
        }

        private static void AddPovMask(int pov, ref ushort buttons)
        {
            if (pov < 0)
                return;

            if (pov >= 31500 || pov <= 4500)
                buttons |= DpadUp;
            if (pov >= 4500 && pov <= 13500)
                buttons |= DpadRight;
            if (pov >= 13500 && pov <= 22500)
                buttons |= DpadDown;
            if (pov >= 22500 && pov <= 31500)
                buttons |= DpadLeft;
        }

        private static short ToSignedAxis(int value, bool invert)
        {
            var centered = value - 32767;
            if (invert)
                centered = -centered;

            return (short)Math.Clamp(centered, short.MinValue, short.MaxValue);
        }

        private static bool HasAxisValue(int value) =>
            value > 0 && value < 65535;

        private static byte ToPositiveTrigger(int value)
        {
            var delta = Math.Max(0, value - 32767);
            return (byte)Math.Clamp(delta * 255 / 32768, 0, 255);
        }

        private static byte ToNegativeTrigger(int value)
        {
            var delta = Math.Max(0, 32767 - value);
            return (byte)Math.Clamp(delta * 255 / 32767, 0, 255);
        }

        private static bool ContainsIgnoreCase(string? value, string needle) =>
            !string.IsNullOrWhiteSpace(value) &&
            value.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;

        private static float GetButtonStrength(XboxControllerSnapshot state, ushort buttonMask) =>
            IsButtonDown(state, buttonMask) ? 1f : 0f;

        private static float GetTriggerStrength(byte triggerValue)
        {
            if (triggerValue <= TriggerThreshold)
                return 0f;

            return Math.Clamp((triggerValue - TriggerThreshold) / (float)(byte.MaxValue - TriggerThreshold), 0f, 1f);
        }

        private static float GetPositiveAxisStrength(short value)
        {
            if (value <= StickDeadZone)
                return 0f;

            return Math.Clamp((value - StickDeadZone) / (float)(short.MaxValue - StickDeadZone), 0f, 1f);
        }

        private static float GetNegativeAxisStrength(short value)
        {
            if (value >= -StickDeadZone)
                return 0f;

            return Math.Clamp((-value - StickDeadZone) / (float)(short.MaxValue - StickDeadZone), 0f, 1f);
        }

        private static bool IsButtonDown(XboxControllerSnapshot state, ushort buttonMask) =>
            (state.Buttons & buttonMask) != 0;

        [StructLayout(LayoutKind.Sequential)]
        private struct XInputState
        {
            public uint PacketNumber;
            public XInputGamepad Gamepad;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XInputGamepad
        {
            public ushort Buttons;
            public byte LeftTrigger;
            public byte RightTrigger;
            public short ThumbLX;
            public short ThumbLY;
            public short ThumbRX;
            public short ThumbRY;
        }

        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
        private static extern uint XInputGetState14(uint dwUserIndex, out XInputState pState);

        [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")]
        private static extern uint XInputGetState910(uint dwUserIndex, out XInputState pState);
    }
}
