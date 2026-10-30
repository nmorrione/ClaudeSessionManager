using System.Text.RegularExpressions;

namespace ClaudeSessionManager;

/// <summary>
/// Percorsi in cui Claude (app desktop + Claude Code) salva le sessioni.
///
///   %APPDATA%\Claude\claude-code-sessions\&lt;account&gt;\&lt;org&gt;\local_&lt;id&gt;.json   -> scheda della sessione (titolo, cwd, ...)
///     (versioni recenti dell'app su Windows: %LOCALAPPDATA%\Claude-Data\claude_*\claude-code-sessions)
///     (app installata da Microsoft Store / MSIX: %LOCALAPPDATA%\Packages\Claude_*\LocalCache\Roaming\Claude\claude-code-sessions)
///     (macOS: ~/Library/Application Support/Claude/claude-code-sessions)
///   La cartella .claude è la stessa su tutti i sistemi: %USERPROFILE%\.claude su Windows, ~/.claude su macOS.
///   %USERPROFILE%\.claude\projects\&lt;cwd codificata&gt;\&lt;cliId&gt;.jsonl          -> conversazione
///   %USERPROFILE%\.claude\projects\&lt;cwd codificata&gt;\&lt;cliId&gt;\              -> subagent, titolo, ecc.
///   %USERPROFILE%\.claude\projects\&lt;cwd codificata&gt;\memory\                  -> memoria del progetto
///   %USERPROFILE%\.claude\file-history\&lt;cliId&gt;\                            -> versioni dei file (rewind)
/// </summary>
public static class ClaudePaths
{
    /// <summary>Solo per i test: sostituisce la cartella dell'app desktop.</summary>
    public static string? SessionsRootOverride { get; set; }

    /// <summary>
    /// Cartelle claude-code-sessions esistenti, dalla più recente: le versioni dell'app hanno cambiato posto più volte.
    /// - Claude-Data: dove le versioni recenti dell'app salvano i dati.
    /// - Pacchetto MSIX: l'app installata da Microsoft Store scriveva %APPDATA% dentro la cartella privata
    ///   del pacchetto; solo i processi avviati da Claude la vedono come %APPDATA%, gli altri devono cercarla lì.
    /// - %APPDATA%\Claude (o Application Support su macOS): installazione classica.
    /// </summary>
    public static IReadOnlyList<string> SessionsRoots
    {
        get
        {
            if (SessionsRootOverride is not null) return [SessionsRootOverride];

            var roots = new List<string>();
            if (OperatingSystem.IsMacOS())
            {
                // .NET su macOS fa puntare ApplicationData a ~/.config: l'app Claude (Electron) usa invece Application Support.
                var support = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support");
                AddDataRoots(roots, Path.Combine(support, "Claude-Data"));
                roots.Add(Path.Combine(support, "Claude", "claude-code-sessions"));
            }
            else if (OperatingSystem.IsWindows())
            {
                var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                AddDataRoots(roots, Path.Combine(local, "Claude-Data"));
                var packages = Path.Combine(local, "Packages");
                if (Directory.Exists(packages))
                {
                    foreach (var pkg in Directory.EnumerateDirectories(packages, "Claude_*"))
                        roots.Add(Path.Combine(pkg, "LocalCache", "Roaming", "Claude", "claude-code-sessions"));
                }
            }
            roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Claude", "claude-code-sessions"));
            return roots.Where(Directory.Exists).Distinct().ToList();
        }
    }

    /// <summary>Claude-Data\&lt;istanza&gt;\claude-code-sessions, una per ogni istanza dell'app.</summary>
    private static void AddDataRoots(List<string> roots, string dataDir)
    {
        if (!Directory.Exists(dataDir)) return;
        foreach (var instance in Directory.EnumerateDirectories(dataDir).OrderByDescending(Directory.GetLastWriteTime))
            roots.Add(Path.Combine(instance, "claude-code-sessions"));
    }

    public static string ClaudeHome
    {
        get
        {
            var custom = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
            return !string.IsNullOrWhiteSpace(custom)
                ? custom
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
        }
    }

    public static string ProjectsDir => Path.Combine(ClaudeHome, "projects");

    public static string FileHistoryDir => Path.Combine(ClaudeHome, "file-history");

    /// <summary>Stessa regola di Claude Code: ogni carattere non alfanumerico diventa '-'.</summary>
    public static string EncodeProjectDir(string cwd) =>
        Regex.Replace(cwd.TrimEnd('\\', '/'), "[^a-zA-Z0-9]", "-");

    /// <summary>Cartelle temporanee che l'app crea per le sessioni senza progetto (…/Claude/… o …/Claude-Data/…/scratch-workspaces/…).</summary>
    public static bool IsScratchWorkspace(string cwd)
    {
        var p = cwd.Replace('\\', '/');
        return p.Contains("/scratch-workspaces/", StringComparison.OrdinalIgnoreCase) &&
               p.Contains("/Claude", StringComparison.OrdinalIgnoreCase);
    }
}
