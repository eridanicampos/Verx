using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text;
using CashFlow.LoadTest;

var options = LoadTestOptions.Parse(args);
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
var ct = cts.Token;

using var handler = new SocketsHttpHandler
{
    MaxConnectionsPerServer = 256,
    PooledConnectionLifetime = TimeSpan.FromMinutes(2),
    AutomaticDecompression = DecompressionMethods.All,
};
using var http = new HttpClient(handler) { BaseAddress = options.BaseUrl, Timeout = TimeSpan.FromSeconds(10) };
using var client = new CashFlowClient(http, options);

Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"CashFlow.LoadTest — cenário {options.Scenario}, leitura {options.Rps} rps, escrita {options.WriteRps} rps, {options.Duration.TotalSeconds}s contra {options.BaseUrl}"));

await client.EnsureTokenAsync(ct);
var runId = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
var accounts = new List<Guid>();
for (var i = 0; i < options.Accounts; i++)
{
    var document = (90_000_000_000L + (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 1_000_000_000L) + i).ToString(CultureInfo.InvariantCulture)[..11];
    accounts.Add(await client.OpenAccountAsync(document, ct));
}

var expectedCredits = new ConcurrentDictionary<Guid, decimal>();
foreach (var account in accounts)
{
    using var seed = client.Transaction(account, "credit", 1000m, $"seed-{runId}-{account:N}");
    using var response = await http.SendAsync(seed, ct);
    response.EnsureSuccessStatusCode();
}

Console.WriteLine($"Preparação: {accounts.Count} contas criadas. Aquecimento {options.Warmup.TotalSeconds}s...");

var runner = new LoadRunner(options.MaxInFlight);
var reads = new Metrics("GET /balance (consolidado)");
var writes = new Metrics("POST /transactions");

async Task<(bool, string)> ReadAsync(long n, CancellationToken token)
{
    await client.EnsureTokenAsync(token);
    using var request = client.Authorized(HttpMethod.Get, $"/api/v1/accounts/{accounts[(int)(n % accounts.Count)]}/balance");
    using var response = await http.SendAsync(request, token);
    return (response.IsSuccessStatusCode, ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture));
}

async Task<(bool, string)> WriteAsync(long n, CancellationToken token)
{
    await client.EnsureTokenAsync(token);
    var account = accounts[(int)(n % accounts.Count)];
    var amount = 1m + (n % 50);
    using var request = client.Transaction(account, "credit", amount, $"lt-{runId}-{n}");
    using var response = await http.SendAsync(request, token);
    if (response.StatusCode == HttpStatusCode.Created)
    {
        expectedCredits.AddOrUpdate(account, amount, (_, v) => v + amount);
    }

    return (response.IsSuccessStatusCode, ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture));
}

await runner.RunAsync(Math.Max(1, options.Rps / 2), options.Warmup, new Metrics("warmup"), ReadAsync, ct);

var started = DateTimeOffset.UtcNow;
var tasks = new List<Task>();
if (options.Scenario is Scenario.Consolidated or Scenario.Mixed)
{
    tasks.Add(runner.RunAsync(options.Rps, options.Duration, reads, ReadAsync, ct));
}

if (options.Scenario is Scenario.Transactions or Scenario.Mixed)
{
    tasks.Add(runner.RunAsync(options.WriteRps, options.Duration, writes, WriteAsync, ct));
}

await Task.WhenAll(tasks);
var elapsed = DateTimeOffset.UtcNow - started;

var consistency = "não aplicável";
var consistent = true;
if (writes.Total > 0)
{
    var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
    var mismatches = accounts.Count;
    var convergedAfter = TimeSpan.Zero;
    var check = DateTimeOffset.UtcNow;
    while (DateTimeOffset.UtcNow < deadline)
    {
        mismatches = 0;
        foreach (var account in accounts)
        {
            var transactional = await client.TransactionalBalanceAsync(account, ct);
            var consolidated = await client.ConsolidatedBalanceAsync(account, ct);
            if (consolidated != transactional || transactional != 1000m + expectedCredits.GetValueOrDefault(account))
            {
                mismatches++;
            }
        }

        if (mismatches == 0)
        {
            convergedAfter = DateTimeOffset.UtcNow - check;
            break;
        }

        await Task.Delay(500, ct);
    }

    consistent = mismatches == 0;
    consistency = consistent
        ? $"OK — consolidado convergiu para o saldo transacional em {convergedAfter.TotalSeconds:F1}s"
        : $"FALHA — {mismatches} conta(s) divergentes após 30s";
}

var thresholds = new List<string>();
var passed = consistent;
foreach (var m in new[] { reads, writes }.Where(m => m.Total > 0))
{
    var okRate = m.ErrorRate <= options.MaxErrorRate;
    var okP95 = m.Percentile(95) <= options.P95Ms;
    passed &= okRate && okP95;
    thresholds.Add(string.Create(CultureInfo.InvariantCulture, $"- {m.Name}: erro {m.ErrorRate:P2} (limite {options.MaxErrorRate:P0}) {(okRate ? "✅" : "❌")} · p95 {m.Percentile(95):F1} ms (limite {options.P95Ms} ms) {(okP95 ? "✅" : "❌")}"));
}

var report = new StringBuilder()
    .AppendLine(CultureInfo.InvariantCulture, $"# Resultado do teste de carga — {started:yyyy-MM-dd HH:mm} UTC")
    .AppendLine()
    .AppendLine(CultureInfo.InvariantCulture, $"Cenário **{options.Scenario}**, alvo {options.BaseUrl}, duração {elapsed.TotalSeconds:F0}s, {accounts.Count} contas.")
    .AppendLine()
    .AppendLine("| Operação | Total | RPS obtido | Erros | p50 ms | p95 ms | p99 ms | máx ms | Status HTTP |")
    .AppendLine("|---|---|---|---|---|---|---|---|---|");
foreach (var m in new[] { reads, writes }.Where(m => m.Total > 0))
{
    report.AppendLine(m.ToMarkdownRow(elapsed));
}

report.AppendLine()
    .AppendLine("## Critérios")
    .AppendLine(string.Join(Environment.NewLine, thresholds))
    .AppendLine(CultureInfo.InvariantCulture, $"- Consistência eventual: {consistency} {(consistent ? "✅" : "❌")}")
    .AppendLine()
    .AppendLine(CultureInfo.InvariantCulture, $"**Resultado: {(passed ? "APROVADO" : "REPROVADO")}**");

Console.WriteLine();
Console.WriteLine(report.ToString());
Directory.CreateDirectory(options.OutputDir);
var file = Path.Combine(options.OutputDir, $"loadtest-{options.Scenario.ToString().ToLowerInvariant()}-{started:yyyyMMddHHmmss}.md");
await File.WriteAllTextAsync(file, report.ToString(), ct);
Console.WriteLine($"Relatório salvo em {file}");

return passed ? 0 : 1;
