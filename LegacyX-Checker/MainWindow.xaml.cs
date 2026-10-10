using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using LegacyX.Checker.Scanning;

namespace LegacyX.Checker;

public partial class MainWindow : Window
{
    private readonly CheckApi _api = new(App.ApiUrl());
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private CancellationTokenSource? _cancel;
    private ScanContext? _context;
    private CheckReport? _report;
    private string _code = "";
    private string? _reportPath;
    private Stopwatch _clock = new();
    private bool _formatting;
    private bool _sent;

    private static readonly SolidColorBrush White = new(Color.FromRgb(0xFA, 0xFA, 0xFA));
    private static readonly SolidColorBrush Dark = new(Color.FromRgb(0x0A, 0x0A, 0x0A));
    private static readonly SolidColorBrush Line = new(Color.FromRgb(0x3A, 0x3A, 0x3A));
    private static readonly SolidColorBrush Dim = new(Color.FromRgb(0x73, 0x73, 0x73));
    private static readonly SolidColorBrush Green = new(Color.FromRgb(0x22, 0xC5, 0x5E));
    private static readonly SolidColorBrush Red = new(Color.FromRgb(0xEF, 0x44, 0x44));

    public MainWindow()
    {
        InitializeComponent();
        Footer.Text = $"LEGACY-X Checker {App.Version}. It installs nothing and is meant for one check: it removes itself after sending its result.";
        WhatItDoes.Text =
            "• File and folder names on your drives, compared with a list of known cheats.\n" +
            "• The programs running right now, and what ran recently (Windows Prefetch and the Recent list).\n" +
            "• Which Steam accounts have signed in on this PC.";
        WhatIsSent.Text =
            "• The names of anything that looks like a cheat, with a shortened path (your user name is hidden).\n" +
            "• The Steam IDs found on this PC, how many files were looked at, and how long it took.\n" +
            "• The same list is saved on your Desktop, so you can read exactly what was found.\n" +
            "• When the result has been sent, this program deletes itself from your PC. Your Desktop report stays.";
        NotSent.Text = "The contents of your files, screenshots, passwords, browser data, or anything you type.";
        _timer.Tick += (_, _) => UpdateProgress();
        SetStep(1);
        Loaded += (_, _) =>
        {
            FadeIn(CodePanel, 10);
            Typewriter(CodeTitle, "> enter your check code");
            CodeCaret.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(520)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
            // One slow scan line drifting down the window.
            ScanLineMove.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-90, 700, TimeSpan.FromSeconds(5.5)) { RepeatBehavior = RepeatBehavior.Forever });
            CodeBox.Focus();
            // A personal download has the code in check.json: fill it in and check it, so the player only has to read and agree.
            if (App.CodeFromFile() is { Length: > 0 } fileCode)
            {
                CodeBox.Text = fileCode;
                CodeError.Text = "";
                CheckCode_Click(this, new RoutedEventArgs());
            }
        };
    }

    // Windows 11: round the corners, and let the window be frosted glass (Acrylic blur of what is behind it).
    // Older Windows gets a plain dark window instead of a half-working effect.
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var glass = false;
        try
        {
            var handle = new WindowInteropHelper(this).Handle;
            var corners = 2; // DWMWCP_ROUND
            DwmSetWindowAttribute(handle, 33, ref corners, sizeof(int));
            if (Environment.OSVersion.Version.Build >= 22621 && HwndSource.FromHwnd(handle) is { } source)
            {
                source.CompositionTarget.BackgroundColor = Colors.Transparent;
                var dark = 1; // DWMWA_USE_IMMERSIVE_DARK_MODE
                DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
                var acrylic = 3; // DWMWA_SYSTEMBACKDROP_TYPE: transient window = Acrylic
                glass = DwmSetWindowAttribute(handle, 38, ref acrylic, sizeof(int)) == 0;
            }
        }
        catch
        {
            // Not supported here: the plain dark window below is used.
        }
        if (!glass)
        {
            Background = new SolidColorBrush(Color.FromRgb(0x0A, 0x0A, 0x0A));
            Shell.Background = new SolidColorBrush(Color.FromRgb(0x0A, 0x0A, 0x0A));
        }
    }

    /// <summary>Text that types itself out, like a terminal.</summary>
    private void Typewriter(System.Windows.Controls.TextBlock block, string text)
    {
        var shown = 0;
        block.Text = "";
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(22) };
        timer.Tick += (_, _) =>
        {
            shown += 1;
            block.Text = text[..Math.Min(shown, text.Length)];
            if (shown >= text.Length) timer.Stop();
        };
        timer.Start();
    }

    /// <summary>A panel eases in: it fades up and slides a few pixels, instead of snapping into place.</summary>
    private static void FadeIn(UIElement element, double fromY = 14)
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var move = new TranslateTransform(0, fromY);
        element.RenderTransform = move;
        element.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(280)) { EasingFunction = ease });
        move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(fromY, 0, TimeSpan.FromMilliseconds(340)) { EasingFunction = ease });
    }

    /// <summary>A number that counts up to its value instead of appearing at once.</summary>
    private static void CountUp(System.Windows.Controls.TextBlock block, long target, bool grouped = false)
    {
        if (target <= 0) { block.Text = "0"; return; }
        var clock = Stopwatch.StartNew();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        timer.Tick += (_, _) =>
        {
            var progress = Math.Min(1.0, clock.Elapsed.TotalMilliseconds / 800);
            var value = (long)Math.Round(target * (1 - Math.Pow(1 - progress, 3)));
            block.Text = grouped ? value.ToString("N0") : value.ToString();
            if (progress >= 1) timer.Stop();
        };
        timer.Start();
    }

    /// <summary>When the window closes after the result was sent, the program removes itself: it was made for this one check.</summary>
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (_sent) App.RemoveSelf();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    // --- Steps and panels ---

    private void SetStep(int step)
    {
        var dots = new[] { Dot1, Dot2, Dot3, Dot4 };
        var numbers = new[] { Num1, Num2, Num3, Num4 };
        var labels = new[] { Lbl1, Lbl2, Lbl3, Lbl4 };
        var bars = new[] { Bar1, Bar2, Bar3 };
        for (var index = 0; index < 4; index += 1)
        {
            var done = index + 1 < step;
            var current = index + 1 == step;
            dots[index].Background = done || current ? White : Brushes.Transparent;
            dots[index].BorderBrush = done || current ? White : Line;
            numbers[index].Foreground = done || current ? Dark : Dim;
            numbers[index].Text = done ? "✓" : (index + 1).ToString();
            labels[index].Foreground = current ? White : Dim;
            labels[index].FontWeight = current ? FontWeights.SemiBold : FontWeights.Normal;
        }
        for (var index = 0; index < 3; index += 1) bars[index].Background = index + 1 < step ? White : Line;
    }

    private void ShowPanel(UIElement panel, int step)
    {
        foreach (var candidate in new UIElement[] { CodePanel, ConsentPanel, ScanPanel, DonePanel }) candidate.Visibility = candidate == panel ? Visibility.Visible : Visibility.Collapsed;
        SetStep(step);
        FadeIn(panel);
    }

    // --- 1. The code ---

    private void CodeBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_formatting) return;
        // K7F29QX4 becomes K7F2-9QX4 as it is typed or pasted.
        var clean = new string(CodeBox.Text.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (clean.Length > 8) clean = clean[..8];
        var formatted = clean.Length > 4 ? clean[..4] + "-" + clean[4..] : clean;
        if (formatted == CodeBox.Text) return;
        _formatting = true;
        CodeBox.Text = formatted;
        CodeBox.CaretIndex = formatted.Length;
        _formatting = false;
    }

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
        CheckCodeButton.Content = "Checking…";
        try
        {
            var info = await _api.GetCodeAsync(_code);
            RequestedBy.Text = $"{info.RequestedBy} of LEGACY-X asked you to run this check.";
            ShowPanel(ConsentPanel, 2);
        }
        catch (CheckApiException problem)
        {
            CodeError.Text = problem.Message;
        }
        finally
        {
            CheckCodeButton.IsEnabled = true;
            CheckCodeButton.Content = "Continue";
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
        ShowPanel(ScanPanel, 3);
        Typewriter(ScanTitle, "> scanning this pc");
        LogText.Text = "";
        // A bar that slides across while the scan runs (the total number of files is not known in advance).
        Bar.BeginAnimation(MarginProperty, new ThicknessAnimation(new Thickness(-180, 0, 0, 0), new Thickness(660, 0, 0, 0), TimeSpan.FromSeconds(1.6)) { RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
        _timer.Start();
        var cancel = _cancel.Token;
        var context = _context;
        var stopped = false;
        try
        {
            // A thread of its own, and the file scan uses all but one of the CPU's cores, so the window stays smooth while thousands of files are read.
            await Task.Factory.StartNew(() =>
            {
                context.Status = "Looking at Steam accounts…";
                SteamScanner.Scan(context);
                context.Status = "Looking at running programs…";
                ProcessScanner.Scan(context);
                context.Status = "Looking at what ran recently…";
                TraceScanner.Scan(context);
                context.Status = "Looking at files…";
                FileScanner.Scan(context, cancel);
            }, cancel, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }
        catch (OperationCanceledException)
        {
            stopped = true;
        }
        _timer.Stop();
        _clock.Stop();
        Bar.BeginAnimation(MarginProperty, null);
        UpdateProgress();
        if (stopped)
        {
            ShowDone(Outcome.Neutral, "Scan stopped", "Nothing was sent. You can close this window.", false);
            return;
        }
        _report = new CheckReport
        {
            Consent = true,
            SteamIds = context.SteamIds.ToList(),
            SteamAccounts = context.SteamAccounts.ToList(),
            Cs2 = context.Cs2,
            FilesScanned = context.FilesScanned,
            DurationSeconds = (int)_clock.Elapsed.TotalSeconds,
            Findings = context.Findings.ToList(),
        };
        _reportPath = SaveToDesktop(_report);
        await SendAsync();
    }

    private void UpdateProgress()
    {
        if (_context is null) return;
        FilesText.Text = _context.FilesScanned.ToString("N0");
        DetectionsText.Text = _context.Detections.ToString();
        DetectionsText.Foreground = _context.Detections > 0 ? Red : White;
        SuspicionsText.Text = _context.Suspicions.ToString();
        ElapsedText.Text = _clock.Elapsed.ToString(@"mm\:ss");
        ScanStatus.Text = _context.Status;
        // The last lines of what the scan did, then the folder it is reading right now.
        var lines = _context.RecentLog(7).ToList();
        var current = _context.CurrentPath;
        if (current.Length > 0) lines.Add("▸ " + Masking.Path(current));
        LogText.Text = string.Join("\n", lines);
    }

    private void Stop_Click(object sender, RoutedEventArgs e) => _cancel?.Cancel();

    // --- 4. Send and finish ---

    private async Task SendAsync()
    {
        if (_report is null) return;
        try
        {
            await _api.SendReportAsync(_code, _report);
            App.DeleteCheckFile();
            App.MarkUsed();
            _sent = true;
            var found = _report.Findings.Count;
            ShowDone(
                found == 0 ? Outcome.Good : Outcome.Warning,
                "The result was sent",
                found == 0
                    ? "Nothing suspicious was found. The staff member will read the result. You can close this window."
                    : $"{found} {(found == 1 ? "thing was" : "things were")} found. A result is not a verdict: the staff member reads it and decides.",
                false);
        }
        catch (CheckApiException problem)
        {
            ShowDone(Outcome.Bad, "The result was not sent", "Your report is saved on your Desktop. Try again, or give it to the staff member.", true, problem.Message);
        }
    }

    private async void Retry_Click(object sender, RoutedEventArgs e)
    {
        RetryButton.IsEnabled = false;
        await SendAsync();
        RetryButton.IsEnabled = true;
    }

    private void OpenReport_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_reportPath is not null) Process.Start(new ProcessStartInfo(_reportPath) { UseShellExecute = true });
        }
        catch
        {
            // No program to open a text file: the file is still on the Desktop.
        }
    }

    private enum Outcome { Good, Warning, Bad, Neutral }

    private void ShowDone(Outcome outcome, string title, string text, bool canRetry, string error = "")
    {
        Typewriter(DoneTitle, "> " + title.ToLowerInvariant());
        DoneText.Text = text;
        DoneError.Text = error;
        // A tick when all is well, an exclamation mark when something was found or went wrong.
        var good = outcome == Outcome.Good;
        DoneIcon.Data = Geometry.Parse(good ? "M0,11 L9,20 L24,1" : outcome == Outcome.Neutral ? "M0,0 L16,0" : "M0,0 L0,16 M0,22 L0,22.5");
        var color = outcome switch { Outcome.Good => Green, Outcome.Warning => White, Outcome.Bad => Red, _ => Dim };
        DoneIcon.Stroke = color;
        DoneBadge.BorderBrush = color;
        DoneStats.Visibility = _report is null ? Visibility.Collapsed : Visibility.Visible;
        if (_report is not null)
        {
            CountUp(DoneDetections, _report.Findings.Count(finding => finding.Confidence == Finding.Detection));
            CountUp(DoneSuspicions, _report.Findings.Count(finding => finding.Confidence == Finding.Suspicion));
            CountUp(DoneFiles, _report.FilesScanned, true);
        }
        RetryButton.Visibility = canRetry ? Visibility.Visible : Visibility.Collapsed;
        OpenReportButton.Visibility = _reportPath is null ? Visibility.Collapsed : Visibility.Visible;
        RetryButton.Margin = new Thickness(0, 0, 10, 0);
        ShowPanel(DonePanel, 4);
    }

    /// <summary>The player can read exactly what was found: the same list that goes to the server. Returns where it was saved.</summary>
    private static string? SaveToDesktop(CheckReport report)
    {
        try
        {
            var builder = new StringBuilder();
            builder.AppendLine($"LEGACY-X Checker {App.Version} - {DateTime.Now:yyyy-MM-dd HH:mm}");
            builder.AppendLine($"Files looked at: {report.FilesScanned:N0}   Time: {report.DurationSeconds} s");
            builder.AppendLine($"Steam IDs found on this PC: {(report.SteamIds.Count == 0 ? "none" : string.Join(", ", report.SteamIds))}");
            foreach (var account in report.SteamAccounts)
            {
                builder.AppendLine($"  {account.PersonaName} ({account.AccountName}) {account.SteamId}{(account.MostRecent == true ? " [last used]" : "")}");
                builder.AppendLine($"    last sign-in: {account.LastLogin ?? "unknown"}   CS2 last played: {account.Cs2LastPlayed ?? "unknown"}   CS2 hours: {(account.Cs2Hours is null ? "unknown" : account.Cs2Hours.ToString())}");
                if (!string.IsNullOrEmpty(account.LaunchOptions)) builder.AppendLine($"    CS2 launch options: {account.LaunchOptions}");
            }
            builder.AppendLine($"CS2 installed: {(report.Cs2 is null ? "unknown" : report.Cs2.Installed ? "yes" : "no")}");
            builder.AppendLine();
            if (report.Findings.Count == 0) builder.AppendLine("Nothing was found.");
            foreach (var finding in report.Findings)
            {
                builder.AppendLine($"[{finding.Confidence.ToUpperInvariant()}] {finding.Name} ({finding.Kind})");
                if (finding.Path is not null) builder.AppendLine($"    {finding.Path}");
                if (finding.Note is not null) builder.AppendLine($"    {finding.Note}");
            }
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "LegacyX-Checker-report.txt");
            File.WriteAllText(path, builder.ToString());
            return path;
        }
        catch
        {
            // The Desktop can be read-only; the result is still sent.
            return null;
        }
    }
}
