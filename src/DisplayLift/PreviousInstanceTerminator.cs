using System.Diagnostics;

namespace DisplayLift;

internal static class PreviousInstanceTerminator
{
    public static int StopOtherInstances()
    {
        var currentProcessId = Environment.ProcessId;
        var stopped = 0;

        foreach (var process in Process.GetProcessesByName("DisplayLift"))
        {
            using (process)
            {
                if (process.Id == currentProcessId) continue;
                try
                {
                    if (process.CloseMainWindow() && process.WaitForExit(700))
                    {
                        stopped++;
                        continue;
                    }

                    process.Kill(entireProcessTree: true);
                    if (process.WaitForExit(1500)) stopped++;
                }
                catch
                {
                    // Startup recovery still resets display state even if an inaccessible process cannot be stopped.
                }
            }
        }

        return stopped;
    }
}
