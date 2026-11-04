using CashFlow.Consolidation.Application.Abstractions.Messaging;
using CashFlow.Consolidation.Application.Messages;
using CashFlow.Consolidation.Domain.Enums;
using CashFlow.Consolidation.Domain.Errors;
using CashFlow.Consolidation.Domain.Interfaces.Common;
using CashFlow.Consolidation.Domain.Interfaces.Repository;
using CashFlow.Consolidation.Domain.Models;
using CashFlow.Consolidation.Domain.Utils;
using Microsoft.Extensions.Logging;

namespace CashFlow.Consolidation.Application.Projections;

public sealed partial class TransactionRegisteredProjector
{
    public static readonly string[] SupportedCurrencies = ["BRL"];

    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;
    private readonly ILogger<TransactionRegisteredProjector> _logger;

    public TransactionRegisteredProjector(IUnitOfWork uow, TimeProvider clock, ILogger<TransactionRegisteredProjector> logger)
    {
        _uow = uow;
        _clock = clock;
        _logger = logger;
    }

    public Task<MessageOutcome> ProjectAsync(Guid messageId, TransactionRegisteredMessage message, CancellationToken cancellationToken)
    {
        var direction = Validate(message);

        return _uow.ExecuteInTransactionAsync(
            async ct =>
            {
                if (!await _uow.InboxRepository.TryRegisterAsync(messageId, TransactionRegisteredMessage.MessageType, ct))
                {
                    LogDuplicate(_logger, messageId);
                    return MessageOutcome.Duplicate;
                }

                var now = _clock.GetUtcNow();
                var entry = LedgerEntry.Create(message.TransactionId, message.AccountId, direction, message.Amount, message.OccurredAt, now);

                if (!await _uow.LedgerEntryRepository.TryAddAsync(entry, ct))
                {
                    LogDuplicate(_logger, messageId);
                    return MessageOutcome.Duplicate;
                }

                await _uow.DailyBalanceRepository.ApplyAsync(entry, now, ct);
                await _uow.ConsolidatedAccountRepository.UpsertAsync(entry.AccountId, entry.OccurredAt, entry.OccurredAt, ct);
                return MessageOutcome.Processed;
            },
            cancellationToken);
    }

    internal static EntryDirection Validate(TransactionRegisteredMessage message)
    {
        if (message.TransactionId == Guid.Empty || message.AccountId == Guid.Empty)
        {
            throw new PermanentMessageException("TransactionId/AccountId ausentes.");
        }

        if (message.Amount <= 0)
        {
            throw new PermanentMessageException($"Valor inválido: {message.Amount}.");
        }

        if (!SupportedCurrencies.Contains(message.Currency, StringComparer.Ordinal))
        {
            throw new PermanentMessageException($"Moeda não suportada: {message.Currency}.");
        }

        return message.Type switch
        {
            "credit" => EntryDirection.Credit,
            "debit" => EntryDirection.Debit,
            _ => throw new PermanentMessageException($"Tipo de lançamento desconhecido: {message.Type}."),
        };
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Mensagem {MessageId} já processada; ignorada (idempotência)")]
    private static partial void LogDuplicate(ILogger logger, Guid messageId);
}
