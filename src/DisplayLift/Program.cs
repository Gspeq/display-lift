namespace DisplayLift;

internal static class Program
{
    private static int _finalRestoreStarted;

    [STAThread]
    private static void Main(string[] args)
    {
        var restoreOnly = args.Any(argument => string.Equals(argument, "--restore-only", StringComparison.OrdinalIgnoreCase));
        var stoppedInstances = PreviousInstanceTerminator.StopOtherInstances();
        var startupRecovery = DisplayRecovery.ResetToSystemDefaults();
        if (restoreOnly) return;

        using var instanceMutex = new Mutex(initiallyOwned: true, "Local\\DisplayLift.SingleInstance", out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show("DisplayLift is already running.", "DisplayLift", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        void FinalRestore()
        {
            if (Interlocked.Exchange(ref _finalRestoreStarted, 1) != 0) return;
            try { _ = DisplayRecovery.ResetToSystemDefaults(); }
            catch { }
        }

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ApplicationExit += (_, _) => FinalRestore();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => FinalRestore();
        AppDomain.CurrentDomain.UnhandledException += (_, _) => FinalRestore();
        Application.ThreadException += (_, eventArgs) =>
        {
            FinalRestore();
            MessageBox.Show(eventArgs.Exception.Message, "DisplayLift error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Application.Exit();
        };

        var startMinimized = args.Any(argument => string.Equals(argument, "--minimized", StringComparison.OrdinalIgnoreCase));
        var startupMessage = stoppedInstances > 0
            ? $"Closed {stoppedInstances} older DisplayLift instance(s). {startupRecovery.Message}"
            : startupRecovery.Message;

        try
        {
            Application.Run(new MainForm(startMinimized, startupMessage));
        }
        finally
        {
            FinalRestore();
        }
    }
}
