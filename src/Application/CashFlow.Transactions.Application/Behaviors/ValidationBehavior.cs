using CashFlow.Transactions.Application.Abstractions.Messaging;
using CashFlow.Transactions.Domain.Helpers;
using FluentValidation;

namespace CashFlow.Transactions.Application.Behaviors;

public sealed class ValidationBehavior<TCommand, TResponse> : ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    private readonly ICommandHandler<TCommand, TResponse> _inner;
    private readonly IEnumerable<IValidator<TCommand>> _validators;

    public ValidationBehavior(
        ICommandHandler<TCommand, TResponse> inner,
        IEnumerable<IValidator<TCommand>> validators)
    {
        _inner = inner;
        _validators = validators;
    }

    public async Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken cancellationToken)
    {
        var failures = new List<FluentValidation.Results.ValidationFailure>();
        foreach (var validator in _validators)
        {
            var result = await validator.ValidateAsync(command, cancellationToken);
            failures.AddRange(result.Errors);
        }

        if (failures.Count == 0)
        {
            return await _inner.HandleAsync(command, cancellationToken);
        }

        var details = failures
            .GroupBy(f => f.PropertyName, StringComparer.Ordinal)
            .Select(g => $"{g.Key}: {string.Join(", ", g.Select(f => f.ErrorMessage))}");
        return Error.Validation("validation.failed", string.Join("; ", details));
    }
}
