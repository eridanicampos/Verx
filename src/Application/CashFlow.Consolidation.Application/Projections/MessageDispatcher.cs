using System.Text.Json;
using CashFlow.Consolidation.Application.Messages;

namespace CashFlow.Consolidation.Application.Projections;

public interface IMessageDispatcher
{
    Task<MessageOutcome> DispatchAsync(Guid messageId, string? messageType, ReadOnlyMemory<byte> body, CancellationToken cancellationToken);
}

public sealed class MessageDispatcher : IMessageDispatcher
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly TransactionRegisteredProjector _transactions;
    private readonly AccountOpenedProjector _accounts;

    public MessageDispatcher(TransactionRegisteredProjector transactions, AccountOpenedProjector accounts)
    {
        _transactions = transactions;
        _accounts = accounts;
    }

    public Task<MessageOutcome> DispatchAsync(Guid messageId, string? messageType, ReadOnlyMemory<byte> body, CancellationToken cancellationToken) =>
        messageType switch
        {
            TransactionRegisteredMessage.MessageType =>
                _transactions.ProjectAsync(messageId, Deserialize<TransactionRegisteredMessage>(body), cancellationToken),
            AccountOpenedMessage.MessageType =>
                _accounts.ProjectAsync(messageId, Deserialize<AccountOpenedMessage>(body), cancellationToken),
            _ => throw new PermanentMessageException($"Tipo de mensagem não suportado: '{messageType}'."),
        };

    internal static T Deserialize<T>(ReadOnlyMemory<byte> body)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(body.Span, Json)
                ?? throw new PermanentMessageException("Payload vazio.");
        }
        catch (JsonException ex)
        {
            throw new PermanentMessageException("Payload JSON inválido.", ex);
        }
    }
}
