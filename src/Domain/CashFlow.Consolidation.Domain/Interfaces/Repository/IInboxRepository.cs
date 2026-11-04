namespace CashFlow.Consolidation.Domain.Interfaces.Repository;

public interface IInboxRepository
{
    Task<bool> TryRegisterAsync(Guid messageId, string messageType, CancellationToken cancellationToken);
}
