using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace ClaudeSessionManager;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<ExportRow> _exportRows = [];
    private readonly ObservableCollection<ImportRow> _importRows = [];
    private readonly DataGridCollectionView _exportView;

    private string? _zipPath;
    private ArchiveManifest? _manifest;

    private static readonly IBrush Ok = Brushes.DarkGreen;
    private static readonly IBrush Warn = Brushes.DarkOrange;
    private static readonly IBrush Bad = Brushes.Firebrick;

    private static readonly FilePickerFileType ZipType = new("Archivio sessioni Claude") { Patterns = ["*.zip"] };

    public MainWindow()
    {
        InitializeComponent();

        var version = typeof(MainWindow).Assembly.GetName().Version;
        if (version is not null) Title += $" {version.Major}.{version.Minor}.{version.Build}";

        _exportView = new DataGridCollectionView(_exportRows) { Filter = FilterExportRow };
        _exportView.SortDescriptions.Add(DataGridSortDescription.FromPath(nameof(ExportRow.LastActivity), ListSortDirection.Descending));
        ExportGrid.ItemsSource = _exportView;
        ImportGrid.ItemsSource = _importRows;

        FilterBox.TextChanged += (_, _) => ApplyFilter();
        ShowArchived.IsCheckedChanged += (_, _) => ApplyFilter();
        ShowScratch.IsCheckedChanged += (_, _) => ApplyFilter();
        AccountBox.SelectionChanged += (_, _) => UpdateImportStates();
        // Il binding si aggiorna dopo la fine della modifica.
        ImportGrid.CellEditEnded += (_, _) => Dispatcher.UIThread.Post(UpdateImportStates);

        Opened += (_, _) => RefreshSessions();
    }

    // ================================================================== ESPORTA

    private void RefreshSessions()
    {
        try
        {
            var selected = _exportRows.Where(r => r.IsSelected).Select(r => r.Session.MetaPath).ToHashSet();
            _exportRows.Clear();
            foreach (var s in SessionStore.Scan())
                _exportRows.Add(new ExportRow(s) { IsSelected = selected.Contains(s.MetaPath) });

            FillAccounts();
            UpdateSelectionText();
            SetStatus(ClaudePaths.SessionsRoots.Count == 0
                ? "Non trovo la cartella delle sessioni dell'app Claude su questo computer."
                : $"{_exportRows.Count} sessioni trovate.");
        }
        catch (Exception ex)
        {
            _ = ShowErrorAsync("Errore durante la lettura delle sessioni", ex);
        }
    }

    private bool FilterExportRow(object o)
    {
        if (o is not ExportRow r) return false;
        if (ShowArchived.IsChecked != true && r.Session.Archived) return false;
        if (ShowScratch.IsChecked != true && ClaudePaths.IsScratchWorkspace(r.Cwd)) return false;

        var f = (FilterBox.Text ?? "").Trim();
        return f.Length == 0 ||
               r.Title.Contains(f, StringComparison.OrdinalIgnoreCase) ||
               r.Cwd.Contains(f, StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyFilter()
    {
        _exportView.Refresh();
        UpdateSelectionText();
    }

    private void Refresh_Click(object? sender, RoutedEventArgs e) => RefreshSessions();

    private void ExportSelectAll_Click(object? sender, RoutedEventArgs e)
    {
        foreach (var r in _exportView.OfType<ExportRow>()) r.IsSelected = true;
        UpdateSelectionText();
    }

    private void ExportSelectNone_Click(object? sender, RoutedEventArgs e)
    {
        foreach (var r in _exportRows) r.IsSelected = false;
        UpdateSelectionText();
    }

    private void ExportCheck_Click(object? sender, RoutedEventArgs e) => UpdateSelectionText();

    private void UpdateSelectionText()
    {
        var sel = _exportRows.Where(r => r.IsSelected).ToList();
        SelectionText.Text = $"{sel.Count} selezionate ({sel.Sum(r => r.SizeMb):N1} MB non compressi)";
    }

    private async void Export_Click(object? sender, RoutedEventArgs e)
    {
        var selected = _exportRows.Where(r => r.IsSelected).Select(r => r.Session).ToList();
        if (selected.Count == 0)
        {
            await MessageAsync("Seleziona almeno una sessione.");
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Esporta sessioni",
            SuggestedFileName = $"claude-sessions-{DateTime.Now:yyyyMMdd-HHmm}.zip",
            DefaultExtension = "zip",
            FileTypeChoices = [ZipType],
        });
        if (file?.TryGetLocalPath() is not { } path) return;

        var options = new ExportOptions(IncludeHistory.IsChecked == true, IncludeMemory.IsChecked == true);
        if (await RunBusy(p => SessionArchive.Export(selected, path, options, p)))
            SetStatus($"Esportate {selected.Select(s => s.LocalId).Distinct().Count()} sessioni in {path}");
    }

    // ================================================================== IMPORTA

    private void FillAccounts()
    {
        var previous = (AccountBox.SelectedItem as AccountDir)?.Path;
        var accounts = SessionStore.FindAccounts();   // il più recente è il primo
        AccountBox.ItemsSource = accounts;
        if (accounts.Count == 0) return;
        var idx = accounts.FindIndex(a => a.Path == previous);
        AccountBox.SelectedIndex = idx >= 0 ? idx : 0;
    }

    private async void OpenArchive_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Apri archivio di sessioni",
            AllowMultiple = false,
            FileTypeFilter = [ZipType],
        });
        if (files is not [var file] || file.TryGetLocalPath() is not { } path) return;

        try
        {
            _manifest = SessionArchive.ReadManifest(path);
            _zipPath = path;
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Archivio non valido", ex);
            return;
        }

        ZipText.Text = $"{Path.GetFileName(_zipPath)}  —  {_manifest.Sessions.Count} sessioni, esportate da {_manifest.User} su {_manifest.Machine} il {_manifest.ExportedAt:dd/MM/yyyy HH:mm}";
        _importRows.Clear();
        foreach (var s in _manifest.Sessions.OrderBy(s => s.Cwd).ThenByDescending(s => s.LastActivity))
            _importRows.Add(new ImportRow(s));

        FillAccounts();
        UpdateImportStates();
        ImportButton.IsEnabled = _importRows.Count > 0;
    }

    private void ImportSelectAll_Click(object? sender, RoutedEventArgs e)
    {
        foreach (var r in _importRows) r.IsSelected = true;
    }

    private void ImportSelectNone_Click(object? sender, RoutedEventArgs e)
    {
        foreach (var r in _importRows) r.IsSelected = false;
    }

    private async void BrowseTarget_Click(object? sender, RoutedEventArgs e)
    {
        var rows = ImportGrid.SelectedItems.OfType<ImportRow>().ToList();
        if (rows.Count == 0)
        {
            await MessageAsync("Evidenzia prima una o più righe (clic, Ctrl/Cmd+clic, Maiusc+clic).");
            return;
        }

        var start = Directory.Exists(rows[0].TargetCwd) ? await StorageProvider.TryGetFolderFromPathAsync(rows[0].TargetCwd) : null;
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Cartella del progetto su questo computer",
            AllowMultiple = false,
            SuggestedStartLocation = start,
        });
        if (folders is not [var folder] || folder.TryGetLocalPath() is not { } path) return;

        foreach (var r in rows) r.TargetCwd = path.TrimEnd('/', '\\');
        UpdateImportStates();
    }

    private void UpdateImportStates()
    {
        if (AccountBox.SelectedItem is not AccountDir account) return;
        foreach (var r in _importRows)
        {
            var target = r.TargetCwd.Trim();
            (r.State, r.StateBrush) =
                !r.HasValidTarget ? ("Cartella non valida", Bad) :
                SessionArchive.Exists(r.Session, account.Path, target) ? ("Già presente (sovrascrive)", Warn) :
                !Directory.Exists(target) ? ("Cartella inesistente", Warn) :
                ("Nuova", Ok);
        }
    }

    private async void Import_Click(object? sender, RoutedEventArgs e)
    {
        if (_zipPath is null || _manifest is null) return;
        if (AccountBox.SelectedItem is not AccountDir account)
        {
            await MessageAsync("Non trovo la cartella dell'app Claude su questo computer.\nApri almeno una volta la scheda Code dell'app Claude e riprova.");
            return;
        }

        ImportGrid.CommitEdit();
        var rows = _importRows.Where(r => r.IsSelected).ToList();
        if (rows.Count == 0) return;

        var invalid = rows.FirstOrDefault(r => !r.HasValidTarget);
        if (invalid is not null)
        {
            await MessageAsync($"La cartella di destinazione di \"{invalid.Title}\" non è valida.");
            return;
        }

        var items = rows.Select(r => new ImportItem { Session = r.Session, TargetCwd = r.TargetCwd.Trim() }).ToList();
        int overwrite = items.Count(i => SessionArchive.Exists(i.Session, account.Path, i.TargetCwd));
        var msg = $"Importare {items.Count} sessioni nell'account {account.ShortId}?";
        if (overwrite > 0) msg += $"\n\n{overwrite} sono già presenti e verranno sovrascritte (chiudile nell'app prima di procedere).";
        if (!await ConfirmAsync(msg, "Importa", "Annulla")) return;

        ImportResult? result = null;
        var zip = _zipPath;
        var manifest = _manifest;
        if (!await RunBusy(p => result = SessionArchive.Import(zip, manifest, items, account.Path, p)) || result is null) return;

        var text = $"Importate {result.Imported} sessioni.\n\nSe non compaiono subito nell'app Claude, chiudila e riaprila.";
        if (result.Warnings.Count > 0) text += "\n\nAvvisi:\n- " + string.Join("\n- ", result.Warnings);
        await MessageAsync(text);

        RefreshSessions();
        UpdateImportStates();
    }

    // ================================================================== HELPER

    private async Task<bool> RunBusy(Action<IProgress<(int Percent, string Message)>> work)
    {
        IsEnabled = false;
        Cursor = new Cursor(StandardCursorType.Wait);
        var progress = new Progress<(int Percent, string Message)>(p =>
        {
            Progress.Value = Math.Clamp(p.Percent, 0, 100);
            SetStatus(p.Message);
        });
        try
        {
            await Task.Run(() => work(progress));
            return true;
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Operazione non riuscita", ex);
            return false;
        }
        finally
        {
            Cursor = Cursor.Default;
            IsEnabled = true;
            Progress.Value = 0;
        }
    }

    private void SetStatus(string text) => StatusText.Text = text;

    private async Task ShowErrorAsync(string title, Exception ex)
    {
        SetStatus(title + ": " + ex.Message);
        await MessageAsync($"{title}\n\n{ex.Message}");
    }

    private Task MessageAsync(string message) => ConfirmAsync(message, "OK", null);

    /// <summary>Finestrella con un messaggio e uno o due pulsanti; true se si sceglie il primo.</summary>
    private async Task<bool> ConfirmAsync(string message, string yes, string? no)
    {
        bool result = false;
        var dialog = new Window
        {
            Title = Title,
            Icon = Icon,
            Width = 480,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        var btnYes = new Button { Content = yes, MinWidth = 100, Classes = { "accent" } };
        btnYes.Click += (_, _) => { result = true; dialog.Close(); };
        buttons.Children.Add(btnYes);
        if (no is not null)
        {
            var btnNo = new Button { Content = no, MinWidth = 100 };
            btnNo.Click += (_, _) => dialog.Close();
            buttons.Children.Add(btnNo);
        }
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 16,
            Children = { new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap }, buttons },
        };
        await dialog.ShowDialog(this);
        return result;
    }
}
