using CashFlow.Consolidation.Domain.Errors;
using CashFlow.Consolidation.Domain.Helpers;
using CashFlow.Consolidation.Domain.Interfaces.Common;
using CashFlow.Consolidation.Domain.Interfaces.Service;
using CashFlow.Consolidation.Domain.Models;
using CashFlow.Consolidation.Domain.Utils;

namespace CashFlow.Consolidation.Application.Balances.Services;

public sealed class BalanceService : IBalanceService
{
    public const int MaxRangeDays = 366;

    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public BalanceService(IUnitOfWork uow, TimeProvider clock)
    {
        _uow = uow;
        _clock = clock;
    }

    public async Task<Result<AccountBalance>> GetBalanceAtAsync(Guid accountId, DateTimeOffset? at, CancellationToken cancellationToken)
    {
        var account = await _uow.BalanceReadRepository.GetAccountAsync(accountId, cancellationToken);
        if (account is null)
        {
            return BalanceErrors.AccountNotFound;
        }

        var instant = (at ?? _clock.GetUtcNow()).ToUniversalTime();
        var day = BusinessCalendar.DateOf(instant);

        var before = await _uow.BalanceReadRepository.SumDailyBeforeAsync(accountId, day, cancellationToken);
        var sameDay = await _uow.BalanceReadRepository.SumEntriesOfDayUntilAsync(accountId, day, instant, cancellationToken);

        return new AccountBalance(
            accountId,
            instant,
            before.Credits + sameDay.Credits,
            before.Debits + sameDay.Debits,
            account.LastEntryAt);
    }

    public async Task<Result<IReadOnlyList<DailyBalanceStatement>>> GetDailyBalancesAsync(
        Guid accountId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        if (from > to || to.DayNumber - from.DayNumber > MaxRangeDays)
        {
            return BalanceErrors.InvalidRange;
        }

        if (await _uow.BalanceReadRepository.GetAccountAsync(accountId, cancellationToken) is null)
        {
            return BalanceErrors.AccountNotFound;
        }

        var opening = await _uow.BalanceReadRepository.SumDailyBeforeAsync(accountId, from, cancellationToken);
        var running = opening.Credits - opening.Debits;
        var days = await _uow.BalanceReadRepository.ListDailyAsync(accountId, from, to, cancellationToken);

        var report = new List<DailyBalanceStatement>(days.Count);
        foreach (var day in days.OrderBy(d => d.Date))
        {
            running += day.Net;
            report.Add(new DailyBalanceStatement(day, running));
        }

        return report;
    }
}
