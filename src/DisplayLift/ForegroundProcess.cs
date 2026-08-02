using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DisplayLift;

internal static class ForegroundProcess
{
    public static string GetName()
    {
        var window = GetForegroundWindow();
        if (window == IntPtr.Zero)
        {
            return string.Empty;
        }

        _ = GetWindowThreadProcessId(window, out var processId);
        if (processId == 0)
        {
            return string.Empty;
        }

        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);
}
