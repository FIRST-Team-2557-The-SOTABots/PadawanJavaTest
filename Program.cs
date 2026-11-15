namespace FrcResearchTest;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        if (args.Any(arg => arg.Equals("--selftest", StringComparison.OrdinalIgnoreCase)))
        {
            var exitCode = SelfTest.Run(
                args.Any(arg => arg.Equals("--network", StringComparison.OrdinalIgnoreCase)));
            Environment.Exit(exitCode);
            return;
        }

        try
        {
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.ToString(),
                "FRC Research Test - fatal error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
