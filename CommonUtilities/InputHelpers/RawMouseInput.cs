using System;
using System.Runtime.InteropServices;

namespace GameAiAndControls.Input
{
    public static class RawMouseInput
    {
        public const int WmInput = 0x00FF;
        private const ushort GenericDesktopControls = 0x01;
        private const ushort MouseUsage = 0x02;
        private const uint RidInput = 0x10000003;
        private const uint RimTypeMouse = 0;
        private const ushort MouseMoveAbsolute = 0x01;

        public static bool IsRegistered { get; private set; }
        public static DateTime LastMouseDeltaUtc { get; private set; } = DateTime.MinValue;

        public static bool RegisterMouse(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
                return false;

            var devices = new[]
            {
                new RawInputDevice
                {
                    UsagePage = GenericDesktopControls,
                    Usage = MouseUsage,
                    Flags = 0,
                    Target = hwnd
                }
            };

            IsRegistered = RegisterRawInputDevices(
                devices,
                (uint)devices.Length,
                (uint)Marshal.SizeOf<RawInputDevice>());

            return IsRegistered;
        }

        public static void ClearRegistration()
        {
            IsRegistered = false;
            LastMouseDeltaUtc = DateTime.MinValue;
        }

        public static bool HasRecentMouseDelta(TimeSpan maxAge)
        {
            return IsRegistered &&
                   LastMouseDeltaUtc != DateTime.MinValue &&
                   DateTime.UtcNow - LastMouseDeltaUtc <= maxAge;
        }

        public static bool TryReadMouseDelta(IntPtr rawInputHandle, out int deltaX, out int deltaY)
        {
            deltaX = 0;
            deltaY = 0;

            uint size = 0;
            uint headerSize = (uint)Marshal.SizeOf<RawInputHeader>();
            uint sizeResult = GetRawInputData(rawInputHandle, RidInput, IntPtr.Zero, ref size, headerSize);
            if (sizeResult == uint.MaxValue || size == 0)
                return false;

            IntPtr buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                uint read = GetRawInputData(rawInputHandle, RidInput, buffer, ref size, headerSize);
                if (read == uint.MaxValue || read != size)
                    return false;

                uint type = (uint)Marshal.ReadInt32(buffer, 0);
                if (type != RimTypeMouse)
                    return false;

                int mouseOffset = Marshal.SizeOf<RawInputHeader>();
                ushort flags = (ushort)Marshal.ReadInt16(buffer, mouseOffset);
                if ((flags & MouseMoveAbsolute) != 0)
                    return false;

                deltaX = Marshal.ReadInt32(buffer, mouseOffset + 12);
                deltaY = Marshal.ReadInt32(buffer, mouseOffset + 16);
                bool hasDelta = deltaX != 0 || deltaY != 0;
                if (hasDelta)
                    LastMouseDeltaUtc = DateTime.UtcNow;

                return hasDelta;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterRawInputDevices(
            [In] RawInputDevice[] rawInputDevices,
            uint rawInputDeviceCount,
            uint rawInputDeviceSize);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetRawInputData(
            IntPtr rawInput,
            uint command,
            IntPtr data,
            ref uint size,
            uint headerSize);

        [StructLayout(LayoutKind.Sequential)]
        private struct RawInputDevice
        {
            public ushort UsagePage;
            public ushort Usage;
            public uint Flags;
            public IntPtr Target;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RawInputHeader
        {
            public uint Type;
            public uint Size;
            public IntPtr Device;
            public IntPtr WParam;
        }
    }
}
