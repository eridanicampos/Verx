using System.Globalization;

namespace CashFlow.LoadTest;

internal enum Scenario
{
    Consolidated,
    Transactions,
    Mixed,
}

internal sealed record LoadTestOptions
{
    public Uri BaseUrl { get; init; } = new("http://localhost:8000");

    public string Username { get; init; } = "operador";

    public string Password { get; init; } = string.Empty;

    public Scenario Scenario { get; init; } = Scenario.Consolidated;

    public int Rps { get; init; } = 50;

    public int WriteRps { get; init; } = 20;

    public TimeSpan Duration { get; init; } = TimeSpan.FromSeconds(60);

    public TimeSpan Warmup { get; init; } = TimeSpan.FromSeconds(5);

    public int Accounts { get; init; } = 20;

    public double MaxErrorRate { get; init; } = 0.05;

    public double P95Ms { get; init; } = 500;

    public int MaxInFlight { get; init; } = 500;

    public string OutputDir { get; init; } = "loadtest-results";

    public static LoadTestOptions Parse(string[] args)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith("--", StringComparison.Ordinal) && i + 1 < args.Length)
            {
                map[args[i][2..]] = args[++i];
            }
        }

        string? Get(string key, string? env = null) =>
            map.TryGetValue(key, out var v) ? v : env is null ? null : Environment.GetEnvironmentVariable(env);

        var password = Get("password", "CASHFLOW_LOADTEST_PASSWORD");
        var passwordFile = Get("password-file");
        if (string.IsNullOrEmpty(password) && passwordFile is not null)
        {
            password = File.ReadAllText(passwordFile).Trim();
        }

        var o = new LoadTestOptions();
        return o with
        {
            BaseUrl = new Uri(Get("base-url", "CASHFLOW_BASE_URL") ?? o.BaseUrl.ToString()),
            Username = Get("username") ?? o.Username,
            Password = password ?? throw new ArgumentException("Informe --password, --password-file ou CASHFLOW_LOADTEST_PASSWORD."),
            Scenario = Enum.Parse<Scenario>(Get("scenario") ?? nameof(Scenario.Consolidated), ignoreCase: true),
            Rps = Int("rps", o.Rps),
            WriteRps = Int("write-rps", o.WriteRps),
            Duration = TimeSpan.FromSeconds(Int("duration", (int)o.Duration.TotalSeconds)),
            Warmup = TimeSpan.FromSeconds(Int("warmup", (int)o.Warmup.TotalSeconds)),
            Accounts = Int("accounts", o.Accounts),
            MaxErrorRate = Dbl("max-error-rate", o.MaxErrorRate),
            P95Ms = Dbl("p95-ms", o.P95Ms),
            MaxInFlight = Int("max-in-flight", o.MaxInFlight),
            OutputDir = Get("output") ?? o.OutputDir,
        };

        int Int(string key, int fallback) => Get(key) is { } v ? int.Parse(v, CultureInfo.InvariantCulture) : fallback;

        double Dbl(string key, double fallback) => Get(key) is { } v ? double.Parse(v, CultureInfo.InvariantCulture) : fallback;
    }
}
