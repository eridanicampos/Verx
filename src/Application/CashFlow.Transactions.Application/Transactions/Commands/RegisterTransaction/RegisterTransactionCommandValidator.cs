using FluentValidation;

namespace CashFlow.Transactions.Application.Transactions.Commands.RegisterTransaction;

public sealed class RegisterTransactionCommandValidator : AbstractValidator<RegisterTransactionCommand>
{
    public const decimal MaxAmount = 10_000_000m;

    public RegisterTransactionCommandValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty();
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .LessThanOrEqualTo(MaxAmount)
            .Must(a => decimal.Round(a, 2) == a)
            .WithMessage("O valor deve ter no máximo 2 casas decimais.");
        RuleFor(x => x.Description).MaximumLength(250);
        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .MaximumLength(100)
            .Matches("^[A-Za-z0-9_\\-:.]+$")
            .WithMessage("Idempotency-Key deve conter apenas caracteres alfanuméricos, '-', '_', ':' ou '.'.");
    }
}
