using CashFlow.Consolidation.Application.Messages;
using CashFlow.Consolidation.Domain.Interfaces.Common;

namespace CashFlow.Consolidation.Application.Projections;

public sealed class AccountOpenedProjector
{
    private readonly IUnitOfWork _uow;

    public AccountOpenedProjector(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public Task<MessageOutcome> ProjectAsync(Guid messageId, AccountOpenedMessage message, CancellationToken cancellationToken)
    {
        if (message.AccountId == Guid.Empty)
        {
            throw new PermanentMessageException("AccountId ausente.");
        }

        return _uow.ExecuteInTransactionAsync(
            async ct =>
            {
                if (!await _uow.InboxRepository.TryRegisterAsync(messageId, AccountOpenedMessage.MessageType, ct))
                {
                    return MessageOutcome.Duplicate;
                }

                await _uow.ConsolidatedAccountRepository.UpsertAsync(message.AccountId, message.OccurredAt.ToUniversalTime(), null, ct);
                return MessageOutcome.Processed;
            },
            cancellationToken);
    }
}
