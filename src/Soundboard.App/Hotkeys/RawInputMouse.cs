using System.Runtime.InteropServices;

namespace Soundboard.Hotkeys;

/// <summary>
/// Mouse side-button (XButton1 / XButton2) input via <c>RegisterRawInputDevices</c>
/// with <c>RIDEV_INPUTSINK</c>, so presses arrive while other apps are focused.
/// This does not consume the click (the foreground game still receives it).
/// Not a low-level mouse hook.
/// </summary>
internal static class RawInputMouse
{
    public static bool Register(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        NativeMethods.RawInputDevice[] devices =
        [
            new()
            {
                UsagePage = NativeMethods.HidUsagePageGeneric,
                Usage = NativeMethods.HidUsageMouse,
                Flags = NativeMethods.RidevInputSink,
                Target = hwnd
            }
        ];

        return NativeMethods.RegisterRawInputDevices(
            devices, (uint)devices.Length, (uint)Marshal.SizeOf<NativeMethods.RawInputDevice>());
    }

    public static void Unregister()
    {
        NativeMethods.RawInputDevice[] devices =
        [
            new()
            {
                UsagePage = NativeMethods.HidUsagePageGeneric,
                Usage = NativeMethods.HidUsageMouse,
                Flags = NativeMethods.RidevRemove,
                Target = IntPtr.Zero
            }
        ];

        NativeMethods.RegisterRawInputDevices(
            devices, (uint)devices.Length, (uint)Marshal.SizeOf<NativeMethods.RawInputDevice>());
    }

    /// <summary>
    /// Returns true when this WM_INPUT is an XButton1/XButton2 press (not release).
    /// <paramref name="xButton1"/> is true for 侧键1, false for 侧键2.
    /// </summary>
    public static bool TryReadXButtonDown(IntPtr hRawInput, out bool xButton1)
    {
        xButton1 = false;
        var headerSize = (uint)Marshal.SizeOf<NativeMethods.RawInputHeader>();
        uint size = 0;
        NativeMethods.GetRawInputData(
            hRawInput, NativeMethods.RidInput, IntPtr.Zero, ref size, headerSize);
        if (size == 0)
        {
            return false;
        }

        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            var copied = NativeMethods.GetRawInputData(
                hRawInput, NativeMethods.RidInput, buffer, ref size, headerSize);
            if (copied == 0xFFFFFFFF || copied == 0)
            {
                return false;
            }

            var header = Marshal.PtrToStructure<NativeMethods.RawInputHeader>(buffer);
            if (header.Type != NativeMethods.RimTypeMouse)
            {
                return false;
            }

            var mousePtr = IntPtr.Add(buffer, (int)headerSize);
            var mouse = Marshal.PtrToStructure<NativeMethods.RawMouse>(mousePtr);
            if ((mouse.ButtonFlags & NativeMethods.RiMouseButton4Down) != 0)
            {
                xButton1 = true;
                return true;
            }

            if ((mouse.ButtonFlags & NativeMethods.RiMouseButton5Down) != 0)
            {
                xButton1 = false;
                return true;
            }

            return false;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public static bool TryReadXButtonDownFromClientMessage(Message m, out bool xButton1)
    {
        xButton1 = false;
        if (m.Msg != NativeMethods.WmXButtonDown)
        {
            return false;
        }

        var hi = (int)((m.WParam.ToInt64() >> 16) & 0xFFFF);
        if (hi == NativeMethods.XButton1Hi)
        {
            xButton1 = true;
            return true;
        }

        if (hi == NativeMethods.XButton2Hi)
        {
            xButton1 = false;
            return true;
        }

        return false;
    }
}
