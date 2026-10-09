using System.Threading;
namespace LegacyX.Checker.Scanning;

/// <summary>What the scanners share: the rules, what has been found, and a way to tell the window how it is going.</summary>
public sealed class ScanContext
{
    public const int MaxFindings = 200;
    private readonly object _lock = new();
    private readonly HashSet<string> _seen = new();
    private readonly List<Finding> _findings = new();
    private long _files;

    public ScanContext(Rules rules) { Rules = rules; }

    public Rules Rules { get; }
    public HashSet<string> SteamIds { get; } = new();
    public List<SteamAccount> SteamAccounts { get; } = new();
    public Cs2Info? Cs2 { get; set; }
    public long FilesScanned => Interlocked.Read(ref _files);
    public int Detections { get { lock (_lock) return _findings.Count(f => f.Confidence == Finding.Detection); } }
    public int Suspicions { get { lock (_lock) return _findings.Count(f => f.Confidence == Finding.Suspicion); } }
    public IReadOnlyList<Finding> Findings { get { lock (_lock) return _findings.ToList(); } }
    public string Status { get; set; } = "";

    public void CountFile() => Interlocked.Increment(ref _files);

    public void Add(Finding finding)
    {
        var shortened = finding with
        {
            Name = Masking.Trim(finding.Name, 120),
            Path = finding.Path is null ? null : Masking.Path(finding.Path),
            Note = finding.Note is null ? null : Masking.Trim(finding.Note, 200),
        };
        lock (_lock)
        {
            if (_findings.Count >= MaxFindings) return;
            if (!_seen.Add($"{shortened.Kind}|{shortened.Name}|{shortened.Path}")) return;
            _findings.Add(shortened);
        }
    }
}
