using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace ClaudeSessionManager;

public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

/// <summary>Riga della griglia Esporta.</summary>
public sealed class ExportRow(LocalSession session) : Observable
{
    private bool _isSelected;

    public LocalSession Session { get; } = session;
    public bool CanExport => Session.TranscriptPath is not null;
    public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value && CanExport); }

    public string Title => Session.Title;
    public string Cwd => Session.Cwd;
    public DateTime? LastActivity => Session.LastActivity;
    public double SizeMb => Session.SizeBytes / 1024d / 1024d;
    public string Account => Session.Account.ShortId;
    public string State => !CanExport ? "Transcript mancante" : Session.Archived ? "Archiviata" : "";
}

/// <summary>Riga della griglia Importa.</summary>
public sealed class ImportRow(ArchivedSession session) : Observable
{
    private bool _isSelected = true;
    private string _targetCwd = session.Cwd;
    private string _state = "";
    private Brush _stateBrush = Brushes.Black;

    public ArchivedSession Session { get; } = session;
    public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }
    public string TargetCwd { get => _targetCwd; set => Set(ref _targetCwd, value); }
    public string State { get => _state; set => Set(ref _state, value); }
    public Brush StateBrush { get => _stateBrush; set => Set(ref _stateBrush, value); }

    public string Title => Session.Title;
    public string SourceCwd => Session.Cwd;
    public DateTime? LastActivity => Session.LastActivity;

    public bool HasValidTarget => TargetCwd.Trim().Length > 0 && Path.IsPathFullyQualified(TargetCwd.Trim());
}
