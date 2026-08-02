namespace DisplayLift;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        using var instanceMutex = new Mutex(initiallyOwned: true, "Local\\DisplayLift.ProfileManager", out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show("DisplayLift is already running. Check the system tray.", "DisplayLift", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, eventArgs) =>
            MessageBox.Show(eventArgs.Exception.Message, "DisplayLift error", MessageBoxButtons.OK, MessageBoxIcon.Error);

        var startMinimized = args.Any(argument => string.Equals(argument, "--minimized", StringComparison.OrdinalIgnoreCase));
        Application.Run(new MainForm(startMinimized));
    }
}
