using CashFlow.Consolidation.Api.Configuration;
using CashFlow.Consolidation.Application.Abstractions.Messaging;
using CashFlow.Consolidation.Application.Balances.Queries.GetBalanceAt;
using CashFlow.Consolidation.Application.Balances.Queries.GetDailyBalances;
using CashFlow.Consolidation.Application.Contracts.Balances;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;

namespace CashFlow.Consolidation.Api.Controllers;

[Tags("Consolidado")]
[Authorize(Policy = AuthenticationSetup.ReadPolicy)]
[EnableRateLimiting(RateLimitingSetup.ReadsPolicy)]
public sealed class BalancesController : ApiControllerBase
{
    [HttpGet("accounts/{accountId:guid}/balance")]
    [EndpointName("GetBalanceAt")]
    [EndpointSummary("Posição consolidada (saldo) da conta em um instante (padrão: agora)")]
    [ProducesResponseType<BalanceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> GetBalanceAtAsync(
        Guid accountId,
        [FromQuery] DateTimeOffset? at,
        [FromServices] IQueryHandler<GetBalanceAtQuery, BalanceResponse> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetBalanceAtQuery(accountId, at), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error!);
    }

    [HttpGet("accounts/{accountId:guid}/daily-balances")]
    [EndpointName("GetDailyBalances")]
    [EndpointSummary("Relatório de consolidado diário com saldo de fechamento")]
    [ProducesResponseType<IReadOnlyList<DailyBalanceResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> GetDailyBalancesAsync(
        Guid accountId,
        [FromQuery, BindRequired] DateOnly from,
        [FromQuery, BindRequired] DateOnly to,
        [FromServices] IQueryHandler<GetDailyBalancesQuery, IReadOnlyList<DailyBalanceResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetDailyBalancesQuery(accountId, from, to), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error!);
    }
}
