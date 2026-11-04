using CashFlow.Consolidation.Domain.Helpers;

namespace CashFlow.Consolidation.Domain.Errors;

public static class BalanceErrors
{
    public static readonly Error AccountNotFound =
        Error.NotFound("balance.account_not_found", "Conta ainda não conhecida pela consolidação.");

    public static readonly Error InvalidRange =
        Error.Validation("balance.invalid_range", "Período inválido: 'from' deve ser menor ou igual a 'to' e o intervalo máximo é de 366 dias.");
}
