using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using LegacyX.Checker.Scanning;

namespace LegacyX.Checker;

public partial class MainWindow : Window
{
    private readonly CheckApi _api = new(App.ApiUrl());
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private CancellationTokenSource? _cancel;
    private ScanContext? _context;
    private CheckReport? _report;
    private string _code = "";
    private Stopwatch _clock = new();

    public MainWindow()
    {
        InitializeComponent();
        Footer.Text = $"LEGACY-X Checker {App.Version}. Nothing is installed and nothing is changed on this PC.";
        WhatItDoes.Text =
            "• Looks at file names on your drives and compares them with a list of known cheats.\n" +
            "• Looks at the programs running now, and at what ran recently (Windows Prefetch and the Recent list).\n" +
            "• Reads which Steam accounts have signed in on this PC.";
        WhatIsSent.Text =
            "• The names of anything that looks like a cheat, and a shortened path (your user name is hidden).\n" +
            "• The Steam IDs found on this PC, how many files were looked at, and how long it took.\n" +
            "• A short summary also saved on your Desktop as a text file, so you can read exactly what was found.";
        NotSent.Text = "Never sent: the contents of your files, screenshots, passwords, browser data or anything you type.";
        _timer.Tick += (_, _) => UpdateProgress();
        CodeBox.Focus();
    }

    private void ShowPanel(UIElement panel)
    {
        foreach (var candidate in new UIElement[] { CodePanel, ConsentPanel, ScanPanel, DonePanel }) candidate.Visibility = candidate == panel ? Visibility.Visible : Visibility.Collapsed;
    }

    // --- 1. The code ---

    private void CodeBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) CheckCode_Click(sender, e);
    }

    private async void CheckCode_Click(object sender, RoutedEventArgs e)
    {
        CodeError.Text = "";
        var typed = new string(CodeBox.Text.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (typed.Length != 8)
        {
            CodeError.Text = "A code has 8 letters and numbers, like K7F2-9QX4.";
            return;
        }
        _code = typed;
        CheckCodeButton.IsEnabled = false;
        try
        {
            var info = await _api.GetCodeAsync(_code);
            RequestedBy.Text = $"{info.RequestedBy} of LEGACY-X asked you to run this check.";
            ShowPanel(ConsentPanel);
        }
        catch (CheckApiException problem)
        {
            CodeError.Text = problem.Message;
        }
        finally
        {
            CheckCodeButton.IsEnabled = true;
        }
    }

    // --- 2. Consent ---

    private void Consent_Changed(object sender, RoutedEventArgs e) => StartButton.IsEnabled = ConsentBox.IsChecked == true;

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    // --- 3. The scan ---

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (ConsentBox.IsChecked != true) return;
        _context = new ScanContext(Rules.Load());
        _cancel = new CancellationTokenSource();
        _clock = Stopwatch.StartNew();
        ShowPanel(ScanPanel);
        _timer.Start();
        var cancel = _cancel.Token;
        var stopped = false;
        try
        {
            await Task.Run(() =>
            {
                var context = _context;
                context.Status = "Steam accounts…";
                SteamScanner.Scan(context);
                context.Status = "Running programs…";
                ProcessScanner.Scan(context);
                context.Status = "What ran recently…";
                TraceScanner.Scan(context);
                context.Status = "Files…";
                FileScanner.Scan(context, cancel);
            }, cancel);
        }
        catch (OperationCanceledException)
        {
            stopped = true;
        }
        _timer.Stop();
        _clock.Stop();
        UpdateProgress();
        if (stopped)
        {
            ShowDone("Scan stopped.", "Nothing was sent. You can close this window.", false);
            return;
        }
        _report = new CheckReport
        {
            Consent = true,
            SteamIds = _context.SteamIds.ToList(),
            FilesScanned = _context.FilesScanned,
            DurationSeconds = (int)_clock.Elapsed.TotalSeconds,
            Findings = _context.Findings.ToList(),
        };
        SaveToDesktop(_report);
        await SendAsync();
    }

    private void UpdateProgress()
    {
        if (_context is null) return;
        FilesText.Text = _context.FilesScanned.ToString("N0");
        DetectionsText.Text = _context.Detections.ToString();
        SuspicionsText.Text = _context.Suspicions.ToString();
        ElapsedText.Text = _clock.Elapsed.ToString(@"mm\:ss");
        ScanStatus.Text = _context.Status;
    }

    private void Stop_Click(object sender, RoutedEventArgs e) => _cancel?.Cancel();

    // --- 4. Send and finish ---

    private async Task SendAsync()
    {
        if (_report is null) return;
        try
        {
            await _api.SendReportAsync(_code, _report);
            var found = _report.Findings.Count;
            ShowDone(
                "The result was sent.",
                found == 0
                    ? "Nothing suspicious was found. The staff member will read the result. You can close this window."
                    : $"{found} {(found == 1 ? "thing was" : "things were")} found. A result is not a verdict: the staff member reads it and decides. A copy is on your Desktop.",
                false);
        }
        catch (CheckApiException problem)
        {
            ShowDone("The result was not sent.", "", true, problem.Message);
        }
    }

    private async void Retry_Click(object sender, RoutedEventArgs e)
    {
        RetryButton.IsEnabled = false;
        await SendAsync();
        RetryButton.IsEnabled = true;
    }

    private void ShowDone(string title, string text, bool canRetry, string error = "")
    {
        DoneTitle.Text = title;
        DoneText.Text = text;
        DoneError.Text = error;
        RetryButton.Visibility = canRetry ? Visibility.Visible : Visibility.Collapsed;
        ShowPanel(DonePanel);
    }

    /// <summary>The player can read exactly what was found: the same list that goes to the server.</summary>
    private static void SaveToDesktop(CheckReport report)
    {
        try
        {
            var builder = new StringBuilder();
            builder.AppendLine($"LEGACY-X Checker {App.Version} - {DateTime.Now:yyyy-MM-dd HH:mm}");
            builder.AppendLine($"Files looked at: {report.FilesScanned:N0}   Time: {report.DurationSeconds} s");
            builder.AppendLine($"Steam IDs found on this PC: {(report.SteamIds.Count == 0 ? "none" : string.Join(", ", report.SteamIds))}");
            builder.AppendLine();
            if (report.Findings.Count == 0) builder.AppendLine("Nothing was found.");
            foreach (var finding in report.Findings)
            {
                builder.AppendLine($"[{finding.Confidence.ToUpperInvariant()}] {finding.Name} ({finding.Kind})");
                if (finding.Path is not null) builder.AppendLine($"    {finding.Path}");
                if (finding.Note is not null) builder.AppendLine($"    {finding.Note}");
            }
            File.WriteAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "LegacyX-Checker-report.txt"), builder.ToString());
        }
        catch
        {
            // The Desktop can be read-only; the result is still sent.
        }
    }
}
