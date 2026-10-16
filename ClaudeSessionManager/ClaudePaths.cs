using System.Text.RegularExpressions;

namespace ClaudeSessionManager;

/// <summary>
/// Percorsi in cui Claude (app desktop + Claude Code) salva le sessioni.
///
///   %APPDATA%\Claude\claude-code-sessions\&lt;account&gt;\&lt;org&gt;\local_&lt;id&gt;.json   -> scheda della sessione (titolo, cwd, ...)
///     (app installata da Microsoft Store / MSIX: %LOCALAPPDATA%\Packages\Claude_*\LocalCache\Roaming\Claude\claude-code-sessions)
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
    /// Cartelle claude-code-sessions esistenti, prima quelle dei pacchetti MSIX.
    /// L'app installata da Microsoft Store scrive %APPDATA% dentro la cartella privata del pacchetto:
    /// solo i processi avviati da Claude la vedono come %APPDATA%, gli altri devono cercarla lì.
    /// </summary>
    public static IReadOnlyList<string> SessionsRoots
    {
        get
        {
            if (SessionsRootOverride is not null) return [SessionsRootOverride];

            var roots = new List<string>();
            var packages = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages");
            if (Directory.Exists(packages))
            {
                foreach (var pkg in Directory.EnumerateDirectories(packages, "Claude_*"))
                    roots.Add(Path.Combine(pkg, "LocalCache", "Roaming", "Claude", "claude-code-sessions"));
            }
            roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Claude", "claude-code-sessions"));
            return roots.Where(Directory.Exists).ToList();
        }
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

    public static bool IsScratchWorkspace(string cwd) =>
        cwd.Contains(@"\Claude\scratch-workspaces\", StringComparison.OrdinalIgnoreCase);
}
