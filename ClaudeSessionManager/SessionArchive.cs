using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ClaudeSessionManager;

/*
 Struttura dello zip esportato:

   manifest.json                              elenco sessioni + dati per reimportarle
   LEGGIMI.txt
   sessions/<localId>/meta.json               scheda dell'app desktop (local_<id>.json)
   sessions/<localId>/transcript.jsonl        conversazione (<cliId>.jsonl)
   sessions/<localId>/extra/...               cartella projects\<proj>\<cliId>\ (subagent, titolo, ...)
   sessions/<localId>/file-history/...        (opzionale) file-history\<cliId>\
   memory/<projectDir>/...                    (opzionale) projects\<proj>\memory\
*/

public sealed class ArchiveManifest
{
    public const string FormatName = "claude-session-archive";

    public string Format { get; set; } = FormatName;
    public int Version { get; set; } = 1;
    public DateTime ExportedAt { get; set; }
    public string Machine { get; set; } = "";
    public string User { get; set; } = "";
    public string SourceClaudeHome { get; set; } = "";
    public List<ArchivedSession> Sessions { get; set; } = [];
    public List<ArchivedMemory> Memories { get; set; } = [];
}

public sealed class ArchivedSession
{
    public string LocalId { get; set; } = "";
    public string CliId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Cwd { get; set; } = "";
    /// <summary>Nome della cartella sotto .claude\projects che conteneva il transcript.</summary>
    public string ProjectDir { get; set; } = "";
    public DateTime? LastActivity { get; set; }
    public bool Archived { get; set; }
    public long TranscriptBytes { get; set; }
    public bool HasExtra { get; set; }
    public bool HasFileHistory { get; set; }
}

public sealed class ArchivedMemory
{
    public string ProjectDir { get; set; } = "";
    public string Cwd { get; set; } = "";
}

public sealed record ExportOptions(bool IncludeFileHistory, bool IncludeMemory);

public sealed class ImportItem
{
    public required ArchivedSession Session { get; init; }
    public required string TargetCwd { get; set; }
}

public sealed record ImportResult(int Imported, List<string> Warnings);

public static class SessionArchive
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    // ------------------------------------------------------------------ EXPORT

    public static void Export(IEnumerable<LocalSession> selection, string zipPath, ExportOptions options,
        IProgress<(int Percent, string Message)> progress)
    {
        // La stessa sessione può comparire in più account: ne teniamo una sola.
        var sessions = selection.Where(s => s.TranscriptPath is not null)
                                .GroupBy(s => s.LocalId)
                                .Select(g => g.OrderByDescending(s => s.LastActivity).First())
                                .ToList();

        long totalBytes = Math.Max(1, sessions.Sum(s => s.SizeBytes));
        long doneBytes = 0;

        var manifest = new ArchiveManifest
        {
            ExportedAt = DateTime.Now,
            Machine = Environment.MachineName,
            User = Environment.UserName,
            SourceClaudeHome = ClaudePaths.ClaudeHome,
        };

        var tmp = zipPath + ".tmp";
        using (var zip = ZipFile.Open(tmp, ZipArchiveMode.Create))
        {
            foreach (var s in sessions)
            {
                progress.Report(((int)(doneBytes * 100 / totalBytes), $"Esporto \"{s.Title}\"..."));
                var root = $"sessions/{s.LocalId}/";

                zip.CreateEntryFromFile(s.MetaPath, root + "meta.json", CompressionLevel.Optimal);
                zip.CreateEntryFromFile(s.TranscriptPath!, root + "transcript.jsonl", CompressionLevel.Optimal);

                bool hasExtra = s.ExtraDir is not null && Directory.Exists(s.ExtraDir);
                if (hasExtra) AddDirectory(zip, s.ExtraDir!, root + "extra/");

                bool hasHistory = options.IncludeFileHistory && Directory.Exists(s.FileHistoryDir);
                if (hasHistory) AddDirectory(zip, s.FileHistoryDir, root + "file-history/");

                manifest.Sessions.Add(new ArchivedSession
                {
                    LocalId = s.LocalId,
                    CliId = s.CliId,
                    Title = s.Title,
                    Cwd = s.Cwd,
                    ProjectDir = Path.GetFileName(s.ProjectDir!),
                    LastActivity = s.LastActivity,
                    Archived = s.Archived,
                    TranscriptBytes = s.SizeBytes,
                    HasExtra = hasExtra,
                    HasFileHistory = hasHistory,
                });
                doneBytes += s.SizeBytes;
            }

            if (options.IncludeMemory)
            {
                foreach (var g in sessions.GroupBy(s => s.ProjectDir!, StringComparer.OrdinalIgnoreCase))
                {
                    var memDir = Path.Combine(g.Key, "memory");
                    if (!Directory.Exists(memDir)) continue;
                    var name = Path.GetFileName(g.Key);
                    AddDirectory(zip, memDir, $"memory/{name}/");
                    manifest.Memories.Add(new ArchivedMemory { ProjectDir = name, Cwd = g.First().Cwd });
                }
            }

            progress.Report((99, "Scrivo il manifest..."));
            WriteText(zip, "manifest.json", JsonSerializer.Serialize(manifest, JsonOpts));
            WriteText(zip, "LEGGIMI.txt", BuildReadme(manifest));
        }

        File.Move(tmp, zipPath, overwrite: true);
        progress.Report((100, $"Esportate {sessions.Count} sessioni in {Path.GetFileName(zipPath)}"));
    }

    private static void AddDirectory(ZipArchive zip, string dir, string prefix)
    {
        foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(dir, file).Replace('\\', '/');
            zip.CreateEntryFromFile(file, prefix + rel, CompressionLevel.Optimal);
        }
    }

    private static void WriteText(ZipArchive zip, string name, string text)
    {
        using var w = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
        w.Write(text);
    }

    private static string BuildReadme(ArchiveManifest m)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Archivio di sessioni Claude Code (app desktop).");
        sb.AppendLine($"Esportato il {m.ExportedAt:dd/MM/yyyy HH:mm} da {m.User} su {m.Machine}.");
        sb.AppendLine("Per reimportarlo usa Claude Session Manager -> scheda Importa.");
        sb.AppendLine();
        foreach (var s in m.Sessions.OrderBy(s => s.Cwd).ThenBy(s => s.Title))
            sb.AppendLine($"- {s.Title}  [{s.Cwd}]  ({s.LastActivity:dd/MM/yyyy})");
        return sb.ToString();
    }

    // ------------------------------------------------------------------ IMPORT

    public static ArchiveManifest ReadManifest(string zipPath)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        var entry = zip.GetEntry("manifest.json")
                    ?? throw new InvalidDataException("Lo zip non contiene manifest.json: non è un archivio esportato da questo programma.");
        using var r = new StreamReader(entry.Open());
        var m = JsonSerializer.Deserialize<ArchiveManifest>(r.ReadToEnd())
                ?? throw new InvalidDataException("manifest.json non valido.");
        if (m.Format != ArchiveManifest.FormatName)
            throw new InvalidDataException($"Formato sconosciuto: {m.Format}");
        return m;
    }

    /// <summary>True se la sessione esiste già su questo PC (scheda o transcript).</summary>
    public static bool Exists(ArchivedSession s, string accountDir, string targetCwd) =>
        File.Exists(Path.Combine(accountDir, s.LocalId + ".json")) ||
        File.Exists(Path.Combine(ClaudePaths.ProjectsDir, TargetProjectDir(s, targetCwd), s.CliId + ".jsonl"));

    private static string TargetProjectDir(ArchivedSession s, string targetCwd) =>
        SamePath(s.Cwd, targetCwd) && !string.IsNullOrEmpty(s.ProjectDir)
            ? s.ProjectDir
            : ClaudePaths.EncodeProjectDir(targetCwd);

    public static ImportResult Import(string zipPath, ArchiveManifest manifest, IReadOnlyList<ImportItem> items,
        string accountDir, IProgress<(int Percent, string Message)> progress)
    {
        var warnings = new List<string>();
        long total = Math.Max(1, items.Sum(i => i.Session.TranscriptBytes));
        long done = 0;
        int imported = 0;

        Directory.CreateDirectory(accountDir);
        using var zip = ZipFile.OpenRead(zipPath);

        foreach (var item in items)
        {
            var s = item.Session;
            progress.Report(((int)(done * 100 / total), $"Importo \"{s.Title}\"..."));

            var rewriter = new PathRewriter(s, item.TargetCwd, manifest.SourceClaudeHome);
            var projectDir = Path.Combine(ClaudePaths.ProjectsDir, TargetProjectDir(s, item.TargetCwd));
            Directory.CreateDirectory(projectDir);

            var root = $"sessions/{s.LocalId}/";

            // Conversazione
            var transcript = zip.GetEntry(root + "transcript.jsonl");
            if (transcript is null) { warnings.Add($"{s.Title}: transcript mancante nello zip, saltata."); continue; }
            ExtractText(transcript, Path.Combine(projectDir, s.CliId + ".jsonl"), rewriter);

            // Subagent e altri file della sessione
            ExtractFolder(zip, root + "extra/", Path.Combine(projectDir, s.CliId), rewriter, overwrite: true);

            // Cronologia file (rewind)
            ExtractFolder(zip, root + "file-history/", Path.Combine(ClaudePaths.FileHistoryDir, s.CliId), null, overwrite: false);

            // Scheda per l'app desktop: va scritta per ultima, così la sessione appare solo quando è completa.
            var metaEntry = zip.GetEntry(root + "meta.json");
            if (metaEntry is null) { warnings.Add($"{s.Title}: meta.json mancante, la sessione non apparirà nell'app."); continue; }
            WriteMeta(metaEntry, Path.Combine(accountDir, s.LocalId + ".json"), rewriter);

            imported++;
            done += s.TranscriptBytes;
        }

        // Memoria di progetto: si aggiunge, senza sovrascrivere quella già presente.
        foreach (var mem in manifest.Memories)
        {
            var item = items.FirstOrDefault(i => string.Equals(i.Session.ProjectDir, mem.ProjectDir, StringComparison.OrdinalIgnoreCase));
            if (item is null) continue;
            var target = Path.Combine(ClaudePaths.ProjectsDir, TargetProjectDir(item.Session, item.TargetCwd), "memory");
            int skipped = ExtractFolder(zip, $"memory/{mem.ProjectDir}/", target, new PathRewriter(item.Session, item.TargetCwd, manifest.SourceClaudeHome), overwrite: false);
            if (skipped > 0) warnings.Add($"Memoria {mem.ProjectDir}: {skipped} file già presenti non sovrascritti.");
        }

        progress.Report((100, $"Importate {imported} sessioni."));
        return new ImportResult(imported, warnings);
    }

    private static void WriteMeta(ZipArchiveEntry entry, string targetPath, PathRewriter rewriter)
    {
        string text;
        using (var r = new StreamReader(entry.Open())) text = rewriter.Rewrite(r.ReadToEnd());

        if (JsonNode.Parse(text) is JsonObject meta)
        {
            // Riferimenti a sessioni collegate che sull'altro PC non esistono.
            meta.Remove("pendingSystemReminder");
            meta.Remove("spawnedFrom");
            text = meta.ToJsonString();
        }
        File.WriteAllText(targetPath, text, new UTF8Encoding(false));
    }

    /// <summary>Estrae tutti i file sotto un prefisso. Ritorna quanti file esistenti sono stati saltati.</summary>
    private static int ExtractFolder(ZipArchive zip, string prefix, string targetDir, PathRewriter? rewriter, bool overwrite)
    {
        int skipped = 0;
        var fullTarget = Path.GetFullPath(targetDir) + Path.DirectorySeparatorChar;

        foreach (var e in zip.Entries.Where(e => e.FullName.StartsWith(prefix, StringComparison.Ordinal) && e.Name.Length > 0))
        {
            var dest = Path.GetFullPath(Path.Combine(targetDir, e.FullName[prefix.Length..]));
            if (!dest.StartsWith(fullTarget, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Percorso non valido nello zip: {e.FullName}");
            if (!overwrite && File.Exists(dest)) { skipped++; continue; }

            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            var ext = Path.GetExtension(dest).ToLowerInvariant();
            if (rewriter is not null && ext is ".jsonl" or ".json" or ".md")
                ExtractText(e, dest, rewriter);
            else
                e.ExtractToFile(dest, overwrite: true);
        }
        return skipped;
    }

    /// <summary>Copia un file di testo riga per riga, riscrivendo i percorsi se la cartella è cambiata.</summary>
    private static void ExtractText(ZipArchiveEntry entry, string dest, PathRewriter rewriter)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        if (!rewriter.IsActive)
        {
            entry.ExtractToFile(dest, overwrite: true);
            return;
        }

        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        using var writer = new StreamWriter(dest, false, new UTF8Encoding(false)) { NewLine = "\n" };
        string? line;
        while ((line = reader.ReadLine()) is not null)
            writer.WriteLine(rewriter.Rewrite(line));
    }

    private static bool SamePath(string a, string b) =>
        string.Equals(a.TrimEnd('\\', '/'), b.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Sostituisce, nel testo JSON, i percorsi del PC di origine con quelli del PC di destinazione:
    /// cartella del progetto, cartella .claude dell'utente e nome codificato della cartella di progetto.
    /// </summary>
    private sealed class PathRewriter
    {
        private readonly List<(string From, string To)> _pairs = [];

        public PathRewriter(ArchivedSession s, string targetCwd, string sourceClaudeHome)
        {
            AddPath(s.Cwd, targetCwd);
            AddPath(sourceClaudeHome, ClaudePaths.ClaudeHome);
            var newDir = TargetProjectDir(s, targetCwd);
            if (!string.IsNullOrEmpty(s.ProjectDir) && !string.Equals(s.ProjectDir, newDir, StringComparison.Ordinal))
                _pairs.Add((s.ProjectDir, newDir));
            _pairs.Sort((a, b) => b.From.Length.CompareTo(a.From.Length));
        }

        public bool IsActive => _pairs.Count > 0;

        private void AddPath(string from, string to)
        {
            if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to) || SamePath(from, to)) return;
            from = from.TrimEnd('\\', '/');
            to = to.TrimEnd('\\', '/');
            _pairs.Add((from.Replace("\\", "\\\\"), to.Replace("\\", "\\\\")));   // forma JSON  C:\\a\\b
            _pairs.Add((from.Replace('\\', '/'), to.Replace('\\', '/')));         // forma       C:/a/b
        }

        public string Rewrite(string text)
        {
            foreach (var (from, to) in _pairs)
                text = text.Replace(from, to, StringComparison.OrdinalIgnoreCase);
            return text;
        }
    }
}
