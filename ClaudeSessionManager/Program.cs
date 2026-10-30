using Avalonia;

namespace ClaudeSessionManager;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // Senza finestra: ClaudeSessionManager --list
        // Elenca le sessioni trovate (usato anche dai test su GitHub Actions).
        if (args is ["--list", ..])
            return RunListCli();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();

    private static int RunListCli()
    {
        try
        {
            foreach (var root in ClaudePaths.SessionsRoots) Console.WriteLine($"Cartella app: {root}");
            Console.WriteLine($"Cartella .claude: {ClaudePaths.ClaudeHome}");
            var sessions = SessionStore.Scan();
            foreach (var s in sessions.OrderByDescending(s => s.LastActivity))
                Console.WriteLine($"{s.LastActivity:yyyy-MM-dd HH:mm}\t{s.Title}\t{s.Cwd}\t{(s.TranscriptPath is null ? "senza transcript" : $"{s.SizeBytes} B")}");
            Console.WriteLine($"{sessions.Count} sessioni trovate.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
