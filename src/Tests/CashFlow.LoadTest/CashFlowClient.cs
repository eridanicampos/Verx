using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace CashFlow.LoadTest;

internal sealed class CashFlowClient : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    private readonly LoadTestOptions _options;
    private readonly SemaphoreSlim _authLock = new(1, 1);
    private string _accessToken = string.Empty;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public CashFlowClient(HttpClient http, LoadTestOptions options)
    {
        _http = http;
        _options = options;
    }

    private sealed record TokenResponse(string AccessToken, int ExpiresIn);

    private sealed record IdResponse(Guid Id);

    public sealed record BalanceResponse(decimal Balance);

    public sealed record AccountResponse(Guid Id, decimal Balance);

    public HttpClient Http => _http;

    public async Task EnsureTokenAsync(CancellationToken ct)
    {
        if (DateTimeOffset.UtcNow.AddMinutes(1) < _expiresAt)
        {
            return;
        }

        await _authLock.WaitAsync(ct);
        try
        {
            if (DateTimeOffset.UtcNow.AddMinutes(1) < _expiresAt)
            {
                return;
            }

            var response = await _http.PostAsJsonAsync("/auth/token", new { username = _options.Username, password = _options.Password }, ct);
            response.EnsureSuccessStatusCode();
            var token = (await response.Content.ReadFromJsonAsync<TokenResponse>(Json, ct))!;
            _accessToken = token.AccessToken;
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn);
        }
        finally
        {
            _authLock.Release();
        }
    }

    public HttpRequestMessage Authorized(HttpMethod method, string path, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: Json);
        }

        return request;
    }

    public async Task<Guid> OpenAccountAsync(string document, CancellationToken ct)
    {
        using var request = Authorized(HttpMethod.Post, "/api/v1/accounts", new { holderName = "Carga " + document[^4..], holderDocument = document, overdraftLimit = 0 });
        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(Json, ct))!.Id;
    }

    public HttpRequestMessage Transaction(Guid accountId, string type, decimal amount, string idempotencyKey)
    {
        var request = Authorized(HttpMethod.Post, $"/api/v1/accounts/{accountId}/transactions", new { type, amount, description = "loadtest" });
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return request;
    }

    public async Task<decimal> TransactionalBalanceAsync(Guid accountId, CancellationToken ct)
    {
        using var request = Authorized(HttpMethod.Get, $"/api/v1/accounts/{accountId}");
        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AccountResponse>(Json, ct))!.Balance;
    }

    public async Task<decimal?> ConsolidatedBalanceAsync(Guid accountId, CancellationToken ct)
    {
        using var request = Authorized(HttpMethod.Get, $"/api/v1/accounts/{accountId}/balance");
        using var response = await _http.SendAsync(request, ct);
        return response.IsSuccessStatusCode ? (await response.Content.ReadFromJsonAsync<BalanceResponse>(Json, ct))!.Balance : null;
    }

    public void Dispose() => _authLock.Dispose();
}
