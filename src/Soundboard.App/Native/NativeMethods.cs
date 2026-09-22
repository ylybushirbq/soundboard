using System.Runtime.InteropServices;

namespace Soundboard;

/// <summary>
/// Ordinary user-mode Win32 APIs only.
/// Game mode: GetAsyncKeyState (non-consuming) + Raw Input for mouse side buttons.
/// Desktop mode: RegisterHotKey for keyboard (consumes the combo) on a
/// message-only HWND so WM_HOTKEY still arrives while the UI is in the tray;
/// mouse still Raw Input because RegisterHotKey cannot bind XButton1/2.
/// Intentionally omitted: WH_KEYBOARD_LL / WH_MOUSE_LL, SetWindowsHookEx,
/// ReadProcessMemory, WriteProcessMemory, CreateRemoteThread, VirtualAllocEx,
/// or any game-process / kernel-driver APIs.
/// </summary>
internal static class NativeMethods
{
    public const int WmHotkey = 0x0312;
    public const int WmInput = 0x00FF;
    public const int WmXButtonDown = 0x020B;
    public const int HwndBroadcast = 0xFFFF;
    public static readonly IntPtr HwndMessage = new(-3);
    public const int SwRestore = 9;

    public const uint ModAlt = 0x0001;
    public const uint ModControl = 0x0002;
    public const uint ModShift = 0x0004;
    public const uint ModWin = 0x0008;
    public const uint ModNoRepeat = 0x4000;

    public const int VkShift = 0x10;
    public const int VkControl = 0x11;
    public const int VkMenu = 0x12;
    public const int VkLwin = 0x5B;
    public const int VkRwin = 0x5C;
    public const int VkXButton1 = 0x05;
    public const int VkXButton2 = 0x06;

    public const uint RidInput = 0x10000003;
    public const int RimTypeMouse = 0;
    public const uint RidevInputSink = 0x00000100;
    public const uint RidevRemove = 0x00000001;
    public const ushort HidUsagePageGeneric = 0x01;
    public const ushort HidUsageMouse = 0x02;
    public const ushort RiMouseButton4Down = 0x0040;
    public const ushort RiMouseButton5Down = 0x0100;
    public const int XButton1Hi = 1;
    public const int XButton2Hi = 2;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    public static extern short GetKeyState(int nVirtKey);

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterRawInputDevices(
        [In] RawInputDevice[] pRawInputDevices,
        uint uiNumDevices,
        uint cbSize);

    [DllImport("user32.dll")]
    public static extern uint GetRawInputData(
        IntPtr hRawInput,
        uint uiCommand,
        IntPtr pData,
        ref uint pcbSize,
        uint cbSizeHeader);

    public static bool IsKeyDown(int vk)
    {
        const int pressed = 0x8000;
        return (GetKeyState(vk) & pressed) != 0;
    }

    public static bool IsAsyncKeyDown(int vk)
    {
        const int pressed = 0x8000;
        return (GetAsyncKeyState(vk) & pressed) != 0;
    }

    public static bool IsWinDown() => IsKeyDown(VkLwin) || IsKeyDown(VkRwin);

    public static void ReadModifiers(out bool control, out bool alt, out bool shift, out bool win)
    {
        control = IsKeyDown(VkControl);
        alt = IsKeyDown(VkMenu);
        shift = IsKeyDown(VkShift);
        win = IsWinDown();
    }

    public static void ReadAsyncModifiers(out bool control, out bool alt, out bool shift, out bool win)
    {
        control = IsAsyncKeyDown(VkControl);
        alt = IsAsyncKeyDown(VkMenu);
        shift = IsAsyncKeyDown(VkShift);
        win = IsAsyncKeyDown(VkLwin) || IsAsyncKeyDown(VkRwin);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RawInputDevice
    {
        public ushort UsagePage;
        public ushort Usage;
        public uint Flags;
        public IntPtr Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RawInputHeader
    {
        public int Type;
        public int Size;
        public IntPtr Device;
        public IntPtr WParam;
    }

    /// <summary>
    /// RAWMOUSE with the ULONG/USHORT union laid out the way x64 Windows pads it:
    /// usFlags at 0, then 2 bytes pad, usButtonFlags at 4.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    internal struct RawMouse
    {
        [FieldOffset(0)] public ushort Flags;
        [FieldOffset(4)] public ushort ButtonFlags;
        [FieldOffset(6)] public ushort ButtonData;
        [FieldOffset(8)] public uint RawButtons;
        [FieldOffset(12)] public int LastX;
        [FieldOffset(16)] public int LastY;
        [FieldOffset(20)] public uint ExtraInformation;
    }
}
