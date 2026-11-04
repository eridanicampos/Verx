using CashFlow.Transactions.Domain.Helpers;

namespace CashFlow.Transactions.Domain.Errors;

public static class TransactionErrors
{
    public static readonly Error IdempotencyKeyReused = Error.Conflict(
        "idempotency.key_reused",
        "A Idempotency-Key já foi utilizada com um payload diferente.");

    public static readonly Error ConcurrencyExhausted = Error.Conflict(
        "transaction.concurrency_conflict",
        "Alta contenção na conta. Tente novamente com a mesma Idempotency-Key.");
}
