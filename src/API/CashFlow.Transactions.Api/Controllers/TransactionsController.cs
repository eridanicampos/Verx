using CashFlow.Transactions.Api.Configuration;
using CashFlow.Transactions.Api.Contracts;
using CashFlow.Transactions.Application.Abstractions.Messaging;
using CashFlow.Transactions.Application.Contracts.Transactions;
using CashFlow.Transactions.Application.Transactions.Commands.RegisterTransaction;
using CashFlow.Transactions.Application.Transactions.Queries.ListTransactions;
using CashFlow.Transactions.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CashFlow.Transactions.Api.Controllers;

[Tags("Lançamentos")]
public sealed class TransactionsController : ApiControllerBase
{
    public const string IdempotencyHeader = "Idempotency-Key";
    public const string ReplayedHeader = "Idempotency-Replayed";

    [HttpPost("accounts/{accountId:guid}/transactions")]
    [Consumes("application/json")]
    [Authorize(Policy = AuthenticationSetup.WritePolicy)]
    [EnableRateLimiting(RateLimitingSetup.WritesPolicy)]
    [EndpointName("RegisterTransaction")]
    [EndpointSummary("Registra um crédito ou débito (idempotente via Idempotency-Key)")]
    [ProducesResponseType<TransactionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<TransactionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<IActionResult> RegisterAsync(
        Guid accountId,
        [FromHeader(Name = IdempotencyHeader)] string? idempotencyKey,
        [FromBody] RegisterTransactionRequest request,
        [FromServices] ICommandHandler<RegisterTransactionCommand, RegisterTransactionResponse> handler,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Problem(
                "idempotency.key_required",
                $"O header {IdempotencyHeader} é obrigatório para registrar lançamentos.",
                StatusCodes.Status400BadRequest);
        }

        if (!TryParseType(request.Type, out var type))
        {
            return Problem("transaction.invalid_type", "Type deve ser 'credit' ou 'debit'.", StatusCodes.Status400BadRequest);
        }

        var result = await handler.HandleAsync(
            new RegisterTransactionCommand(accountId, type, request.Amount, request.Description, idempotencyKey),
            cancellationToken);

        if (result.IsFailure)
        {
            return Problem(result.Error!);
        }

        var response = result.Value;
        Response.Headers[ReplayedHeader] = response.Replayed ? "true" : "false";
        return response.Replayed
            ? Ok(response.Transaction)
            : Created($"/api/v1/accounts/{accountId}/transactions/{response.Transaction.Id}", response.Transaction);
    }

    [HttpGet("accounts/{accountId:guid}/transactions")]
    [Authorize(Policy = AuthenticationSetup.ReadPolicy)]
    [EndpointName("ListTransactions")]
    [EndpointSummary("Extrato de lançamentos da conta")]
    [ProducesResponseType<IReadOnlyList<TransactionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> ListAsync(
        Guid accountId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int? limit,
        [FromServices] IQueryHandler<ListTransactionsQuery, IReadOnlyList<TransactionResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new ListTransactionsQuery(accountId, from, to, limit ?? 100), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error!);
    }

    private static bool TryParseType(string? value, out TransactionType type)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "credit":
                type = TransactionType.Credit;
                return true;
            case "debit":
                type = TransactionType.Debit;
                return true;
            default:
                type = default;
                return false;
        }
    }
}
