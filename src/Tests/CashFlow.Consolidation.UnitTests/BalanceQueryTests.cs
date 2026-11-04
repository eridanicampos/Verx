using CashFlow.Consolidation.Application.Abstractions.Messaging;
using CashFlow.Consolidation.Application.Balances.Queries.GetBalanceAt;
using CashFlow.Consolidation.Application.Balances.Queries.GetDailyBalances;
using CashFlow.Consolidation.Application.Balances.Services;
using CashFlow.Consolidation.Application.Contracts.Balances;
using CashFlow.Consolidation.Domain.Errors;
using CashFlow.Consolidation.Domain.Interfaces.Common;
using CashFlow.Consolidation.Domain.Interfaces.Repository;
using CashFlow.Consolidation.Domain.Models;
using CashFlow.Consolidation.Domain.Utils;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace CashFlow.Consolidation.UnitTests;

public sealed class BalanceQueryTests
{
    private static readonly Guid AccountId = Guid.NewGuid();
    private readonly IBalanceReadRepository _repo = Substitute.For<IBalanceReadRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    public BalanceQueryTests()
    {
        _repo.GetAccountAsync(AccountId, Arg.Any<CancellationToken>())
            .Returns(ConsolidatedAccount.Restore(AccountId, DateTimeOffset.UnixEpoch, DateTimeOffset.UtcNow));
        _unitOfWork.BalanceReadRepository.Returns(_repo);
    }

    [Fact]
    public async Task Balance_at_instant_combines_closed_days_and_intraday_entries()
    {
        var at = new DateTimeOffset(2026, 10, 29, 15, 0, 0, TimeSpan.Zero);
        _repo.SumDailyBeforeAsync(AccountId, new DateOnly(2026, 10, 29), Arg.Any<CancellationToken>()).Returns(new BalanceAggregate(1000m, 300m));
        _repo.SumEntriesOfDayUntilAsync(AccountId, new DateOnly(2026, 10, 29), at, Arg.Any<CancellationToken>()).Returns(new BalanceAggregate(50m, 20m));

        var result = await new GetBalanceAtQueryHandler(new BalanceService(_unitOfWork, new FakeTimeProvider())).HandleAsync(new GetBalanceAtQuery(AccountId, at), CancellationToken.None);

        result.Value.Balance.ShouldBe(730m);
        result.Value.TotalCredits.ShouldBe(1050m);
        result.Value.TotalDebits.ShouldBe(320m);
        result.Value.Consistency.ShouldBe("eventual");
    }

    [Fact]
    public async Task Instant_with_offset_is_normalized_to_utc_business_date()
    {
        var at = new DateTimeOffset(2026, 10, 29, 22, 0, 0, TimeSpan.FromHours(-3));
        _repo.SumDailyBeforeAsync(AccountId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(new BalanceAggregate(0, 0));
        _repo.SumEntriesOfDayUntilAsync(AccountId, Arg.Any<DateOnly>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(new BalanceAggregate(0, 0));

        var result = await new GetBalanceAtQueryHandler(new BalanceService(_unitOfWork, new FakeTimeProvider())).HandleAsync(new GetBalanceAtQuery(AccountId, at), CancellationToken.None);

        result.Value.At.Offset.ShouldBe(TimeSpan.Zero);
        await _repo.Received().SumDailyBeforeAsync(AccountId, new DateOnly(2026, 10, 30), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unknown_account_is_not_found()
    {
        var result = await new GetBalanceAtQueryHandler(new BalanceService(_unitOfWork, new FakeTimeProvider())).HandleAsync(new GetBalanceAtQuery(Guid.NewGuid(), null), CancellationToken.None);

        result.Error.ShouldBe(BalanceErrors.AccountNotFound);
    }

    [Fact]
    public async Task Daily_report_accumulates_closing_balance_from_opening_balance()
    {
        var from = new DateOnly(2026, 10, 1);
        var to = new DateOnly(2026, 10, 3);
        _repo.SumDailyBeforeAsync(AccountId, from, Arg.Any<CancellationToken>()).Returns(new BalanceAggregate(500m, 100m));
        _repo.ListDailyAsync(AccountId, from, to, Arg.Any<CancellationToken>()).Returns(
        [
            DailyBalance.Restore(AccountId, new DateOnly(2026, 10, 3), 0m, 50m, 1, DateTimeOffset.UtcNow),
            DailyBalance.Restore(AccountId, new DateOnly(2026, 10, 1), 100m, 0m, 2, DateTimeOffset.UtcNow),
        ]);

        var result = await new GetDailyBalancesQueryHandler(new BalanceService(_unitOfWork, new FakeTimeProvider())).HandleAsync(new GetDailyBalancesQuery(AccountId, from, to), CancellationToken.None);

        result.Value.Select(d => d.ClosingBalance).ShouldBe([500m, 450m]);
        result.Value[0].Date.ShouldBe(new DateOnly(2026, 10, 1));
    }

    [Theory]
    [InlineData("2026-10-05", "2026-10-01")]
    [InlineData("2025-01-01", "2026-10-01")]
    public async Task Invalid_ranges_are_rejected(string from, string to)
    {
        var result = await new GetDailyBalancesQueryHandler(new BalanceService(_unitOfWork, new FakeTimeProvider())).HandleAsync(
            new GetDailyBalancesQuery(AccountId, DateOnly.Parse(from, System.Globalization.CultureInfo.InvariantCulture), DateOnly.Parse(to, System.Globalization.CultureInfo.InvariantCulture)),
            CancellationToken.None);

        result.Error.ShouldBe(BalanceErrors.InvalidRange);
    }
}
