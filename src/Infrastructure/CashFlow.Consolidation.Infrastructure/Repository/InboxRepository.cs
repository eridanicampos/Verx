using CashFlow.Consolidation.Domain.Interfaces.Repository;
using CashFlow.Consolidation.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Consolidation.Infrastructure.Repository;

internal sealed class InboxRepository : IInboxRepository
{
    private readonly ConsolidationDbContext _db;

    public InboxRepository(ConsolidationDbContext db)
    {
        _db = db;
    }

    public async Task<bool> TryRegisterAsync(Guid messageId, string messageType, CancellationToken cancellationToken)
    {
        var rows = await _db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO consolidation.inbox_messages (message_id, message_type, processed_at)
            VALUES ({messageId}, {messageType}, now())
            ON CONFLICT (message_id) DO NOTHING
            """,
            cancellationToken);
        return rows == 1;
    }
}
