using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DisplayLift;

internal sealed record ForegroundProcessInfo(string ProcessName, IntPtr WindowHandle, Rectangle ScreenBounds);

internal static class ForegroundProcess
{
    public static ForegroundProcessInfo GetInfo()
    {
        var window = GetForegroundWindow();
        if (window == IntPtr.Zero)
        {
            return new ForegroundProcessInfo(string.Empty, IntPtr.Zero, Screen.PrimaryScreen?.Bounds ?? Rectangle.Empty);
        }

        _ = GetWindowThreadProcessId(window, out var processId);
        string processName;
        try
        {
            using var process = Process.GetProcessById((int)processId);
            processName = process.ProcessName;
        }
        catch
        {
            processName = string.Empty;
        }

        var bounds = Screen.FromHandle(window).Bounds;
        return new ForegroundProcessInfo(processName, window, bounds);
    }

    public static bool IsRustRunning()
    {
        var processes = Process.GetProcessesByName("RustClient");
        try { return processes.Length > 0; }
        finally { foreach (var process in processes) process.Dispose(); }
    }

    public static bool IsRust(string processName) =>
        string.Equals(processName, "RustClient", StringComparison.OrdinalIgnoreCase);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);
}
