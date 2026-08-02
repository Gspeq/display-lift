namespace DisplayLift;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, args) =>
            MessageBox.Show(args.Exception.Message, "DisplayLift error", MessageBoxButtons.OK, MessageBoxIcon.Error);

        Application.Run(new MainForm());
    }
}
