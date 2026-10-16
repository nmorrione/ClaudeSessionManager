using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace ClaudeSessionManager;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<ExportRow> _exportRows = [];
    private readonly ObservableCollection<ImportRow> _importRows = [];
    private readonly ICollectionView _exportView;

    private string? _zipPath;
    private ArchiveManifest? _manifest;

    private static readonly Brush Ok = Brushes.DarkGreen;
    private static readonly Brush Warn = Brushes.DarkOrange;
    private static readonly Brush Bad = Brushes.Firebrick;

    public MainWindow()
    {
        InitializeComponent();

        var version = typeof(MainWindow).Assembly.GetName().Version;
        if (version is not null) Title += $" {version.Major}.{version.Minor}.{version.Build}";

        _exportView = CollectionViewSource.GetDefaultView(_exportRows);
        _exportView.Filter = FilterExportRow;
        _exportView.SortDescriptions.Add(new SortDescription(nameof(ExportRow.LastActivity), ListSortDirection.Descending));
        ExportGrid.ItemsSource = _exportView;
        ImportGrid.ItemsSource = _importRows;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e) => RefreshSessions();

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
            SetStatus($"{_exportRows.Count} sessioni trovate.");
        }
        catch (Exception ex)
        {
            ShowError("Errore durante la lettura delle sessioni", ex);
        }
    }

    private bool FilterExportRow(object o)
    {
        if (o is not ExportRow r) return false;
        if (ShowArchived.IsChecked != true && r.Session.Archived) return false;
        if (ShowScratch.IsChecked != true && ClaudePaths.IsScratchWorkspace(r.Cwd)) return false;

        var f = FilterBox.Text.Trim();
        return f.Length == 0 ||
               r.Title.Contains(f, StringComparison.OrdinalIgnoreCase) ||
               r.Cwd.Contains(f, StringComparison.OrdinalIgnoreCase);
    }

    private void Filter_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        _exportView.Refresh();
        UpdateSelectionText();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshSessions();

    private IEnumerable<ExportRow> VisibleExportRows => _exportView.Cast<ExportRow>();

    private void ExportSelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var r in VisibleExportRows) r.IsSelected = true;
        UpdateSelectionText();
    }

    private void ExportSelectNone_Click(object sender, RoutedEventArgs e)
    {
        foreach (var r in _exportRows) r.IsSelected = false;
        UpdateSelectionText();
    }

    private void ExportCheck_Click(object sender, RoutedEventArgs e) => UpdateSelectionText();

    private void UpdateSelectionText()
    {
        var sel = _exportRows.Where(r => r.IsSelected).ToList();
        SelectionText.Text = $"{sel.Count} selezionate ({sel.Sum(r => r.SizeMb):N1} MB non compressi)";
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        var selected = _exportRows.Where(r => r.IsSelected).Select(r => r.Session).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show(this, "Seleziona almeno una sessione.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new SaveFileDialog
        {
            Filter = "Archivio sessioni Claude (*.zip)|*.zip",
            FileName = $"claude-sessions-{DateTime.Now:yyyyMMdd-HHmm}.zip",
        };
        if (dlg.ShowDialog(this) != true) return;

        var options = new ExportOptions(IncludeHistory.IsChecked == true, IncludeMemory.IsChecked == true);
        if (await RunBusy(p => SessionArchive.Export(selected, dlg.FileName, options, p)))
            SetStatus($"Esportate {selected.Select(s => s.LocalId).Distinct().Count()} sessioni in {dlg.FileName}");
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

    private void OpenArchive_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Archivio sessioni Claude (*.zip)|*.zip" };
        if (dlg.ShowDialog(this) != true) return;

        try
        {
            _manifest = SessionArchive.ReadManifest(dlg.FileName);
            _zipPath = dlg.FileName;
        }
        catch (Exception ex)
        {
            ShowError("Archivio non valido", ex);
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

    private void Account_Changed(object sender, SelectionChangedEventArgs e) => UpdateImportStates();

    private void ImportGrid_CellEditEnding(object? sender, DataGridCellEditEndingEventArgs e) =>
        // Il binding si aggiorna dopo questo evento.
        Dispatcher.BeginInvoke(UpdateImportStates);

    private void ImportSelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var r in _importRows) r.IsSelected = true;
    }

    private void ImportSelectNone_Click(object sender, RoutedEventArgs e)
    {
        foreach (var r in _importRows) r.IsSelected = false;
    }

    private void BrowseTarget_Click(object sender, RoutedEventArgs e)
    {
        var rows = ImportGrid.SelectedItems.OfType<ImportRow>().ToList();
        if (rows.Count == 0)
        {
            MessageBox.Show(this, "Evidenzia prima una o più righe (clic, Ctrl+clic, Maiusc+clic).", Title,
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new OpenFolderDialog { Title = "Cartella del progetto su questo PC" };
        if (Directory.Exists(rows[0].TargetCwd)) dlg.InitialDirectory = rows[0].TargetCwd;
        if (dlg.ShowDialog(this) != true) return;

        foreach (var r in rows) r.TargetCwd = dlg.FolderName;
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

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        if (_zipPath is null || _manifest is null) return;
        if (AccountBox.SelectedItem is not AccountDir account)
        {
            MessageBox.Show(this, "Non trovo la cartella dell'app desktop su questo PC.\nApri almeno una volta la scheda Code dell'app Claude e riprova.",
                Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ImportGrid.CommitEdit(DataGridEditingUnit.Row, true);
        var rows = _importRows.Where(r => r.IsSelected).ToList();
        if (rows.Count == 0) return;

        var invalid = rows.FirstOrDefault(r => !r.HasValidTarget);
        if (invalid is not null)
        {
            MessageBox.Show(this, $"La cartella di destinazione di \"{invalid.Title}\" non è valida.", Title,
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var items = rows.Select(r => new ImportItem { Session = r.Session, TargetCwd = r.TargetCwd.Trim() }).ToList();
        int overwrite = items.Count(i => SessionArchive.Exists(i.Session, account.Path, i.TargetCwd));
        var msg = $"Importare {items.Count} sessioni nell'account {account.ShortId}?";
        if (overwrite > 0) msg += $"\n\n{overwrite} sono già presenti e verranno sovrascritte (chiudile nell'app prima di procedere).";
        if (MessageBox.Show(this, msg, Title, MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;

        ImportResult? result = null;
        var zip = _zipPath;
        var manifest = _manifest;
        if (!await RunBusy(p => result = SessionArchive.Import(zip, manifest, items, account.Path, p)) || result is null) return;

        var text = $"Importate {result.Imported} sessioni.\n\nSe non compaiono subito nell'app Claude, chiudila e riaprila.";
        if (result.Warnings.Count > 0) text += "\n\nAvvisi:\n- " + string.Join("\n- ", result.Warnings);
        MessageBox.Show(this, text, Title, MessageBoxButton.OK, MessageBoxImage.Information);

        RefreshSessions();
        UpdateImportStates();
    }

    // ================================================================== HELPER

    private async Task<bool> RunBusy(Action<IProgress<(int Percent, string Message)>> work)
    {
        IsEnabled = false;
        Mouse.OverrideCursor = Cursors.Wait;
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
            ShowError("Operazione non riuscita", ex);
            return false;
        }
        finally
        {
            Mouse.OverrideCursor = null;
            IsEnabled = true;
            Progress.Value = 0;
        }
    }

    private void SetStatus(string text) => StatusText.Text = text;

    private void ShowError(string title, Exception ex)
    {
        SetStatus(title + ": " + ex.Message);
        MessageBox.Show(this, ex.Message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
