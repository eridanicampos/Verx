using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace CashFlow.LoadTest;

internal sealed class Metrics
{
    private readonly ConcurrentBag<double> _latenciesMs = [];
    private readonly ConcurrentDictionary<string, int> _outcomes = new(StringComparer.Ordinal);
    private int _ok;
    private int _failed;

    public Metrics(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public int Ok => _ok;

    public int Failed => _failed;

    public int Total => _ok + _failed;

    public double ErrorRate => Total == 0 ? 0 : (double)_failed / Total;

    public void Record(double ms, bool success, string outcome)
    {
        _latenciesMs.Add(ms);
        _outcomes.AddOrUpdate(outcome, 1, (_, c) => c + 1);
        if (success)
        {
            Interlocked.Increment(ref _ok);
        }
        else
        {
            Interlocked.Increment(ref _failed);
        }
    }

    public double Percentile(double p)
    {
        var sorted = _latenciesMs.OrderBy(x => x).ToArray();
        if (sorted.Length == 0)
        {
            return 0;
        }

        var rank = (int)Math.Ceiling(p / 100 * sorted.Length) - 1;
        return sorted[Math.Clamp(rank, 0, sorted.Length - 1)];
    }

    public double Max => _latenciesMs.IsEmpty ? 0 : _latenciesMs.Max();

    public IReadOnlyDictionary<string, int> Outcomes => _outcomes;

    public string ToMarkdownRow(TimeSpan duration) => string.Create(
        CultureInfo.InvariantCulture,
        $"| {Name} | {Total} | {Total / duration.TotalSeconds:F1} | {ErrorRate:P2} | {Percentile(50):F1} | {Percentile(95):F1} | {Percentile(99):F1} | {Max:F1} | {FormatOutcomes()} |");

    private string FormatOutcomes()
    {
        var sb = new StringBuilder();
        foreach (var (k, v) in _outcomes.OrderBy(o => o.Key, StringComparer.Ordinal))
        {
            sb.Append(CultureInfo.InvariantCulture, $"{k}:{v} ");
        }

        return sb.ToString().Trim();
    }
}
