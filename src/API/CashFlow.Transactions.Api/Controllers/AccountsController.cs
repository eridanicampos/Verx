using CashFlow.Transactions.Api.Configuration;
using CashFlow.Transactions.Api.Contracts;
using CashFlow.Transactions.Application.Abstractions.Messaging;
using CashFlow.Transactions.Application.Accounts.Commands.OpenAccount;
using CashFlow.Transactions.Application.Accounts.Queries.GetAccountById;
using CashFlow.Transactions.Application.Contracts.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CashFlow.Transactions.Api.Controllers;

[Tags("Lançamentos")]
public sealed class AccountsController : ApiControllerBase
{
    [HttpPost("accounts")]
    [Consumes("application/json")]
    [Authorize(Policy = AuthenticationSetup.WritePolicy)]
    [EnableRateLimiting(RateLimitingSetup.WritesPolicy)]
    [EndpointName("OpenAccount")]
    [EndpointSummary("Abre uma conta para um titular")]
    [ProducesResponseType<AccountResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> OpenAsync(
        [FromBody] OpenAccountRequest request,
        [FromServices] ICommandHandler<OpenAccountCommand, AccountResponse> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new OpenAccountCommand(request.HolderName, request.HolderDocument, request.OverdraftLimit),
            cancellationToken);

        return result.IsSuccess
            ? Created($"/api/v1/accounts/{result.Value.Id}", result.Value)
            : Problem(result.Error!);
    }

    [HttpGet("accounts/{accountId:guid}")]
    [Authorize(Policy = AuthenticationSetup.ReadPolicy)]
    [EndpointName("GetAccount")]
    [EndpointSummary("Consulta a conta e o saldo transacional (fonte da verdade)")]
    [ProducesResponseType<AccountResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> GetByIdAsync(
        Guid accountId,
        [FromServices] IQueryHandler<GetAccountByIdQuery, AccountResponse> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetAccountByIdQuery(accountId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error!);
    }
}
