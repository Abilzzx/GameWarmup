using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using GameWarmup.Models;
using GameWarmup.Services;
using Forms = System.Windows.Forms;

namespace GameWarmup;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<GameEntry> _games = new();
    private CancellationTokenSource? _cts;
    private bool _closeAfterCancel;
    private WarmupProgress? _lastProgress;
    private WarmupResult? _lastResult;

    public MainWindow()
    {
        InitializeComponent();
        foreach (var game in LibraryStore.Load()) _games.Add(game);
        GamesList.ItemsSource = _games;
        if (_games.Count > 0) GamesList.SelectedIndex = 0;

        I18n.SetLanguage(SettingsStore.LoadLanguage());
        LanguageCombo.SelectedIndex = I18n.Language == "en" ? 1 : 0;
        LanguageCombo.SelectionChanged += LanguageChanged;
        ApplyLanguage();
    }

    private void LanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        var language = LanguageCombo.SelectedIndex == 1 ? "en" : "ru";
        I18n.SetLanguage(language);
        SettingsStore.SaveLanguage(language);
        ApplyLanguage();
    }

    private void ApplyLanguage()
    {
        Title = "GameWarmup Alpha 0.4";
        TaglineText.Text = I18n.T("tagline");
        LanguageLabel.Text = I18n.T("language");
        LibraryTitle.Text = I18n.T("library");
        GamePathLabel.Text = I18n.T("game_path");
        AddPathButton.Content = I18n.T("add");
        BrowseButton.Content = I18n.T("browse");
        SteamButton.Content = I18n.T("find_steam");
        GamesLabel.Text = I18n.T("games");
        SelectedFolderLabel.Text = I18n.T("selected_folder");
        RemoveButton.Content = I18n.T("remove");
        WarmupTitle.Text = I18n.T("warmup");
        ModeLabel.Text = I18n.T("mode");
        BudgetLabel.Text = I18n.T("budget");
        FocusLabel.Text = I18n.T("focus");
        SkipCachesCheck.Content = I18n.T("skip");
        WorkersCheck.Content = I18n.T("workers");
        SequentialHintCheck.Content = I18n.T("hint");
        WarningText.Text = I18n.T("warning");
        ProgressLabel.Text = I18n.T("progress");
        StartButton.Content = I18n.T("start");
        CancelButton.Content = I18n.T("cancel");
        FooterText.Text = I18n.T("footer");
        GamePathInput.ToolTip = I18n.T("hint_path");
        FocusTermsBox.ToolTip = I18n.T("hint_focus");
        FileText.ToolTip = I18n.T("hint_current_file");
        string[] modes = { "mode_original", "mode_fast", "mode_smart", "mode_full", "mode_match" };
        for (var i = 0; i < modes.Length; i++)
            ((ComboBoxItem)ModeCombo.Items[i]).Content = I18n.T(modes[i]);
        ((ComboBoxItem)BudgetCombo.Items[0]).Content = I18n.T("unlimited");
        int[] budgets = { 2, 4, 8, 16, 32 };
        for (var i = 0; i < budgets.Length; i++)
            ((ComboBoxItem)BudgetCombo.Items[i + 1]).Content = I18n.F("budget_gib", budgets[i]);

        SelectedPath.Text = GamesList.SelectedItem is GameEntry game ? game.Path : I18n.T("select_game");
        if (_lastResult is not null) RenderResult(_lastResult);
        else if (_lastProgress is not null) RenderProgress(_lastProgress);
        else RenderIdle();
    }

    private void RenderIdle()
    {
        ProgressText.Text = I18n.T("ready");
        FilesText.Text = I18n.T("files_idle");
        SpeedText.Text = I18n.T("speed_idle");
        TimeText.Text = I18n.T("elapsed_idle");
        ErrorsText.Text = I18n.T("errors_idle");
        RunHintText.Text = I18n.T("hint_default");
    }

    private void AddGame_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = I18n.T("game_path"),
            UseDescriptionForTitle = true
        };
        if (dialog.ShowDialog() == Forms.DialogResult.OK) AddGame(dialog.SelectedPath);
    }

    private void AddPath_Click(object sender, RoutedEventArgs e) => AddGame(GamePathInput.Text);

    private void AddGame(string rawPath)
    {
        if (_cts is not null) return;
        if (string.IsNullOrWhiteSpace(rawPath))
        { System.Windows.MessageBox.Show(I18n.T("message_path_required")); return; }
        try
        {
            var path = Path.GetFullPath(rawPath.Trim().Trim('"'));
            if (!Directory.Exists(path))
            { System.Windows.MessageBox.Show(I18n.T("message_missing")); return; }
            if (string.Equals(path.TrimEnd('\\'), Path.GetPathRoot(path)?.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            { System.Windows.MessageBox.Show(I18n.T("message_disk")); return; }
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            { System.Windows.MessageBox.Show(I18n.T("message_link")); return; }
            if (_games.Any(g => string.Equals(g.Path.TrimEnd('\\'), path.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)))
            { System.Windows.MessageBox.Show(I18n.T("message_duplicate")); return; }
            var game = new GameEntry { Name = Path.GetFileName(path.TrimEnd('\\')), Path = path };
            _games.Add(game);
            LibraryStore.Save(_games);
            GamesList.SelectedItem = game;
            GamePathInput.Text = path;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException or UnauthorizedAccessException)
        { System.Windows.MessageBox.Show(I18n.F("message_path_error", ex.Message)); }
    }

    private async void SteamScan_Click(object sender, RoutedEventArgs e)
    {
        if (_cts is not null) return;
        SteamButton.IsEnabled = false;
        try
        {
            var found = await Task.Run(SteamScanner.FindInstalledGames);
            int added = 0;
            foreach (var game in found)
            {
                if (_games.Any(g => string.Equals(g.Path, game.Path, StringComparison.OrdinalIgnoreCase))) continue;
                _games.Add(game);
                added++;
            }
            if (added != 0) LibraryStore.Save(_games);
            if (GamesList.SelectedItem is null && _games.Count != 0) GamesList.SelectedIndex = 0;
            System.Windows.MessageBox.Show(I18n.F("message_found", found.Count, added), "GameWarmup");
        }
        catch (Exception ex) { System.Windows.MessageBox.Show(I18n.F("message_steam_error", ex.Message)); }
        finally { SteamButton.IsEnabled = true; }
    }

    private void RemoveGame_Click(object sender, RoutedEventArgs e)
    {
        if (_cts is not null) return;
        if (GamesList.SelectedItem is GameEntry game)
        {
            _games.Remove(game);
            LibraryStore.Save(_games);
        }
    }

    private void GamesList_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        SelectedPath.Text = GamesList.SelectedItem is GameEntry game ? game.Path : I18n.T("select_game");

    private static string FormatTime(TimeSpan value) => value.ToString(@"hh\:mm\:ss");

    private void RenderProgress(WarmupProgress p)
    {
        if (p.Scanning)
        {
            ProgressText.Text = I18n.T("scanning_progress");
            RunHintText.Text = I18n.T("mode_tip");
            return;
        }
        ReadProgress.Value = p.TotalBytes == 0 ? 100 : Math.Clamp(p.BytesCompleted * 100d / p.TotalBytes, 0, 100);
        ProgressText.Text = I18n.F("read_progress", I18n.Gib(p.BytesRead), I18n.Gib(p.TotalBytes), ReadProgress.Value);
        FilesText.Text = I18n.F("files_progress", p.FilesCompleted, p.TotalFiles, p.SkippedFiles);
        SpeedText.Text = I18n.F("speed_progress", p.RecentMbps, p.AverageMbps);
        var remaining = p.AverageMbps > 0.01
            ? TimeSpan.FromSeconds(Math.Min(359999, Math.Max(0, p.TotalBytes - p.BytesCompleted) / 1048576d / p.AverageMbps))
            : (TimeSpan?)null;
        TimeText.Text = I18n.F("time_progress", FormatTime(p.Elapsed), remaining is null ? "—" : FormatTime(remaining.Value));
        ErrorsText.Text = I18n.F("errors_progress", p.ReadErrors);
        FileText.Text = p.CurrentFile;
    }

    private void RenderResult(WarmupResult result)
    {
        ReadProgress.Value = result.TotalBytes == 0 ? 100
            : Math.Clamp(result.BytesCompleted * 100d / result.TotalBytes, 0, 100);
        ProgressText.Text = I18n.F("finishing", I18n.T(result.Cancelled ? "cancelled" : "finished"), I18n.Gib(result.BytesRead));
        FilesText.Text = I18n.F("files_final", result.FilesCompleted, result.FilesRead, result.SkippedFiles);
        double throughput = result.Elapsed.TotalSeconds > 0
            ? result.BytesRead / 1048576d / result.Elapsed.TotalSeconds : 0;
        SpeedText.Text = I18n.F("speed_final", throughput);
        TimeText.Text = I18n.F("time_final", FormatTime(result.Elapsed));
        ErrorsText.Text = I18n.F("errors_progress", result.ReadErrors);
        FileText.Text = "";
        RunHintText.Text = I18n.T("hint_final");
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (_cts is not null) return;
        if (GamesList.SelectedItem is not GameEntry game)
        { System.Windows.MessageBox.Show(I18n.T("message_select")); return; }
        if (!Directory.Exists(game.Path))
        { System.Windows.MessageBox.Show(I18n.T("message_missing")); return; }

        var gib = long.Parse(((ComboBoxItem)BudgetCombo.SelectedItem).Tag.ToString()!);
        var mode = (WarmupMode)ModeCombo.SelectedIndex;
        var options = new WarmupOptions(mode, gib == 0 ? null : gib * 1024L * 1024 * 1024,
            SkipCachesCheck.IsChecked == true, FocusTermsBox.Text,
            WorkersCheck.IsChecked == true ? 2 : 1, SequentialHintCheck.IsChecked == true);
        var target = game.Path;
        _cts = new CancellationTokenSource();
        var cts = _cts;
        _lastResult = null;
        _lastProgress = null;
        SetBusy(true);
        bool acceptsProgress = true;
        ReadProgress.Value = 0;
        RenderIdle();
        ProgressText.Text = I18n.T("scanning");
        FileText.Text = "";
        RunHintText.Text = I18n.F("starting", game.Name);

        var progress = new Progress<WarmupProgress>(p =>
        {
            if (!acceptsProgress) return;
            _lastProgress = p;
            RenderProgress(p);
        });
        try
        {
            var result = await WarmupService.RunAsync(target, options, progress, cts.Token);
            acceptsProgress = false;
            _lastResult = result;
            RenderResult(result);
            HistoryStore.Append(game, options, result);
        }
        catch (OperationCanceledException)
        {
            acceptsProgress = false;
            _lastProgress = null;
            ProgressText.Text = I18n.T("cancelled");
        }
        catch (Exception ex)
        {
            acceptsProgress = false;
            _lastProgress = null;
            ProgressText.Text = I18n.T("failed");
            System.Windows.MessageBox.Show(I18n.F("message_read_error", ex.Message), "GameWarmup");
        }
        finally
        {
            acceptsProgress = false;
            cts.Dispose();
            _cts = null;
            SetBusy(false);
            if (_closeAfterCancel) Dispatcher.BeginInvoke(new Action(Close));
        }
    }

    private void SetBusy(bool busy)
    {
        StartButton.IsEnabled = !busy;
        CancelButton.IsEnabled = busy;
        SteamButton.IsEnabled = !busy;
        BrowseButton.IsEnabled = !busy;
        AddPathButton.IsEnabled = !busy;
        RemoveButton.IsEnabled = !busy;
        // List, language, and options stay visible and readable while a run is active.
        // Options are snapshots and affect only the next warmup.
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (_cts is not null)
        {
            ProgressText.Text = I18n.T("stop");
            _cts.Cancel();
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_cts is not null)
        {
            e.Cancel = true;
            _closeAfterCancel = true;
            _cts.Cancel();
            return;
        }
        base.OnClosing(e);
    }
}
