using System.Text.Json.Nodes;

namespace ClaudeSessionManager;

/// <summary>Cartella &lt;account&gt;\&lt;org&gt; dell'app desktop che contiene i file local_*.json.</summary>
public sealed class AccountDir
{
    public required string Path { get; init; }
    public int SessionCount { get; init; }
    public DateTime LastWrite { get; init; }

    public string ShortId
    {
        get
        {
            var org = System.IO.Path.GetFileName(Path);
            var acc = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(Path)) ?? "";
            return $"{Left(acc, 8)}/{Left(org, 8)}";
        }
    }

    public override string ToString() =>
        $"{ShortId}  —  {SessionCount} sessioni, ultima modifica {LastWrite:dd/MM/yyyy HH:mm}";

    private static string Left(string s, int n) => s.Length <= n ? s : s[..n];
}

public sealed class LocalSession
{
    public required string MetaPath { get; init; }
    public required AccountDir Account { get; init; }
    public required string LocalId { get; init; }
    public required string CliId { get; init; }
    public required string Title { get; init; }
    public required string Cwd { get; init; }
    public DateTime? LastActivity { get; init; }
    public bool Archived { get; init; }
    public string? TranscriptPath { get; init; }

    /// <summary>Cartella projects\&lt;cwd codificata&gt; che contiene il transcript.</summary>
    public string? ProjectDir => TranscriptPath is null ? null : Path.GetDirectoryName(TranscriptPath);

    /// <summary>Cartella projects\&lt;cwd codificata&gt;\&lt;cliId&gt;\ (subagent, custom-title, ...).</summary>
    public string? ExtraDir => ProjectDir is null ? null : Path.Combine(ProjectDir, CliId);

    public string FileHistoryDir => Path.Combine(ClaudePaths.FileHistoryDir, CliId);

    public long SizeBytes { get; init; }
}

public static class SessionStore
{
    public static List<AccountDir> FindAccounts()
    {
        var result = new List<AccountDir>();
        if (!Directory.Exists(ClaudePaths.SessionsRoot)) return result;

        foreach (var acc in Directory.EnumerateDirectories(ClaudePaths.SessionsRoot))
        foreach (var org in Directory.EnumerateDirectories(acc))
        {
            var files = Directory.GetFiles(org, "local_*.json");
            var last = files.Length > 0 ? files.Max(File.GetLastWriteTime) : Directory.GetLastWriteTime(org);
            result.Add(new AccountDir { Path = org, SessionCount = files.Length, LastWrite = last });
        }
        return result.OrderByDescending(a => a.LastWrite).ToList();
    }

    public static List<LocalSession> Scan()
    {
        var transcripts = IndexTranscripts();
        var sessions = new List<LocalSession>();

        foreach (var account in FindAccounts())
        foreach (var metaPath in Directory.EnumerateFiles(account.Path, "local_*.json"))
        {
            JsonObject? meta;
            try { meta = JsonNode.Parse(File.ReadAllText(metaPath)) as JsonObject; }
            catch { continue; }
            if (meta is null) continue;

            var cliId = (string?)meta["cliSessionId"];
            if (string.IsNullOrEmpty(cliId)) continue;

            transcripts.TryGetValue(cliId, out var transcript);
            long size = transcript is null ? 0 : new FileInfo(transcript).Length;

            sessions.Add(new LocalSession
            {
                MetaPath = metaPath,
                Account = account,
                LocalId = (string?)meta["sessionId"] ?? Path.GetFileNameWithoutExtension(metaPath),
                CliId = cliId,
                Title = (string?)meta["title"] ?? "(senza titolo)",
                Cwd = (string?)meta["cwd"] ?? "",
                LastActivity = ReadEpochMs(meta["lastActivityAt"]) ?? ReadEpochMs(meta["createdAt"]),
                Archived = (bool?)meta["isArchived"] ?? false,
                TranscriptPath = transcript,
                SizeBytes = size,
            });
        }
        return sessions;
    }

    /// <summary>cliId -> percorso del .jsonl, cercando in tutte le cartelle di progetto.</summary>
    private static Dictionary<string, string> IndexTranscripts()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(ClaudePaths.ProjectsDir)) return map;

        foreach (var dir in Directory.EnumerateDirectories(ClaudePaths.ProjectsDir))
        foreach (var file in Directory.EnumerateFiles(dir, "*.jsonl"))
        {
            var id = Path.GetFileNameWithoutExtension(file);
            if (!map.TryGetValue(id, out var existing) || new FileInfo(file).Length > new FileInfo(existing).Length)
                map[id] = file;
        }
        return map;
    }

    private static DateTime? ReadEpochMs(JsonNode? node)
    {
        try
        {
            if (node is null) return null;
            var ms = node.GetValue<double>();
            return DateTimeOffset.FromUnixTimeMilliseconds((long)ms).LocalDateTime;
        }
        catch { return null; }
    }
}
