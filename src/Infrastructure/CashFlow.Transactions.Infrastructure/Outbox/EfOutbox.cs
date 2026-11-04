using System.Diagnostics;
using System.Text.Json;
using CashFlow.Transactions.Application.Abstractions.Messaging;
using CashFlow.Transactions.Application.IntegrationEvents;
using CashFlow.Transactions.Application.Interfaces;
using CashFlow.Transactions.Domain.Exceptions;
using CashFlow.Transactions.Domain.Interfaces.Common;
using CashFlow.Transactions.Infrastructure.Context;

namespace CashFlow.Transactions.Infrastructure.Outbox;

internal sealed class EfOutbox : IOutbox
{
    private readonly TransactionsDbContext _db;
    private readonly TimeProvider _clock;

    public EfOutbox(TransactionsDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public void Enqueue<TEvent>(TEvent integrationEvent)
        where TEvent : IIntegrationEvent
    {
        var payload = JsonSerializer.Serialize(integrationEvent, IntegrationEventJson.Options);
        _db.OutboxMessages.Add(new OutboxMessage(
            integrationEvent.EventId,
            TEvent.EventType,
            payload,
            _clock.GetUtcNow(),
            Activity.Current?.Id));
    }
}

public static class IntegrationEventJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
