using System.Text.RegularExpressions;

namespace ClaudeSessionManager;

/// <summary>
/// Percorsi in cui Claude (app desktop + Claude Code) salva le sessioni.
///
///   %APPDATA%\Claude\claude-code-sessions\&lt;account&gt;\&lt;org&gt;\local_&lt;id&gt;.json   -> scheda della sessione (titolo, cwd, ...)
///   %USERPROFILE%\.claude\projects\&lt;cwd codificata&gt;\&lt;cliId&gt;.jsonl          -> conversazione
///   %USERPROFILE%\.claude\projects\&lt;cwd codificata&gt;\&lt;cliId&gt;\              -> subagent, titolo, ecc.
///   %USERPROFILE%\.claude\projects\&lt;cwd codificata&gt;\memory\                  -> memoria del progetto
///   %USERPROFILE%\.claude\file-history\&lt;cliId&gt;\                            -> versioni dei file (rewind)
/// </summary>
public static class ClaudePaths
{
    /// <summary>Solo per i test: sostituisce la cartella dell'app desktop.</summary>
    public static string? SessionsRootOverride { get; set; }

    public static string SessionsRoot => SessionsRootOverride ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Claude", "claude-code-sessions");

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
