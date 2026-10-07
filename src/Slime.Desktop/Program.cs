namespace Slime.Desktop;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var instance = new Mutex(true, @"Local\Slime.Desktop.Pet", out var firstInstance);
        if (!firstInstance) return;
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new SlimeWindow());
    }
}
