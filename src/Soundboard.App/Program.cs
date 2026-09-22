using Soundboard.Ui;

namespace Soundboard;

internal static class Program
{
    private const string MutexName = @"Local\Soundboard.TrayApp.SingleInstance";

    [STAThread]
    private static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        using var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
        {
            ActivateExistingInstance();
            return;
        }

        var startMinimized = args.Any(a =>
            string.Equals(a, "--minimized", StringComparison.OrdinalIgnoreCase)
            || string.Equals(a, "-minimized", StringComparison.OrdinalIgnoreCase));

        Application.Run(new TrayAppContext(startMinimized));
        GC.KeepAlive(mutex);
    }

    private static void ActivateExistingInstance()
    {
        var msg = NativeMethods.RegisterWindowMessage(TrayAppContext.ShowMessageName);
        NativeMethods.PostMessage(new IntPtr(NativeMethods.HwndBroadcast), msg, IntPtr.Zero, IntPtr.Zero);

        var hwnd = NativeMethods.FindWindow(null, MainForm.WindowTitle);
        if (hwnd != IntPtr.Zero)
        {
            NativeMethods.ShowWindow(hwnd, NativeMethods.SwRestore);
            NativeMethods.SetForegroundWindow(hwnd);
        }
    }
}
