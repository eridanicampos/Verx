using FluentValidation;

namespace CashFlow.Transactions.Application.Accounts.Commands.OpenAccount;

public sealed class OpenAccountCommandValidator : AbstractValidator<OpenAccountCommand>
{
    public OpenAccountCommandValidator()
    {
        RuleFor(x => x.HolderName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.HolderDocument)
            .NotEmpty()
            .Must(d => d.Count(char.IsDigit) is 11 or 14)
            .WithMessage("Documento deve ser um CPF (11 dígitos) ou CNPJ (14 dígitos).");
        RuleFor(x => x.OverdraftLimit).GreaterThanOrEqualTo(0).LessThanOrEqualTo(1_000_000);
    }
}
