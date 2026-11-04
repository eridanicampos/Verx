using System.Net.Http.Json;
using System.Text.Json;

namespace CashFlow.Transactions.IntegrationTests.Infrastructure;

public sealed record AccountDto(Guid Id, string HolderName, string HolderDocumentMasked, decimal Balance, decimal OverdraftLimit, string Status);

public sealed record TransactionDto(Guid Id, Guid AccountId, string Type, decimal Amount, string? Description, decimal BalanceAfter, DateTimeOffset OccurredAt);

public static class ApiClientExtensions
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static int _document = 10_000_000;

    public static async Task<AccountDto> OpenAccountAsync(this HttpClient client, decimal overdraft = 0)
    {
        var document = "123" + Interlocked.Increment(ref _document).ToString("D8", System.Globalization.CultureInfo.InvariantCulture);
        var response = await client.PostAsJsonAsync("/api/v1/accounts", new { holderName = "Cliente Teste", holderDocument = document, overdraftLimit = overdraft });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AccountDto>(Json))!;
    }

    public static Task<HttpResponseMessage> PostTransactionAsync(
        this HttpClient client,
        Guid accountId,
        string type,
        decimal amount,
        string? idempotencyKey,
        string? description = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/accounts/{accountId}/transactions")
        {
            Content = JsonContent.Create(new { type, amount, description }),
        };
        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        return client.SendAsync(request);
    }
}

public static class OutboxQueries
{
    public static async Task<int> CountOutboxAsync(this CashFlow.Transactions.Infrastructure.Context.TransactionsDbContext db, Guid accountId, string type)
    {
        var payloads = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            db.OutboxMessages.Where(m => m.Type == type).Select(m => m.Payload));
        return payloads.Count(p => p.Contains(accountId.ToString(), StringComparison.OrdinalIgnoreCase));
    }
}
