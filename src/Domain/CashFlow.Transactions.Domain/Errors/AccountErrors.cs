using CashFlow.Transactions.Domain.Entities.Abstracts;
using CashFlow.Transactions.Domain.Helpers;

namespace CashFlow.Transactions.Domain.Errors;

public static class AccountErrors
{
    public static readonly Error NotFound =
        Error.NotFound("account.not_found", "Conta não encontrada.");

    public static readonly Error Inactive =
        Error.BusinessRule("account.inactive", "A conta não está ativa para movimentações.");

    public static readonly Error InsufficientFunds =
        Error.BusinessRule("account.insufficient_funds", "Saldo insuficiente para o débito solicitado.");

    public static readonly Error InvalidAmount =
        Error.Validation("transaction.invalid_amount", "O valor da movimentação deve ser maior que zero.");

    public static readonly Error DuplicateHolder =
        Error.Conflict("account.duplicate_holder", "Já existe conta para este documento.");

    public static readonly Error InvalidOverdraft =
        Error.Validation("account.invalid_overdraft", "O limite de cheque especial não pode ser negativo.");
}
