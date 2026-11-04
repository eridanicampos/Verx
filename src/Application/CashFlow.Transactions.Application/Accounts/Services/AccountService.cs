using CashFlow.Transactions.Application.IntegrationEvents;
using CashFlow.Transactions.Application.Interfaces;
using CashFlow.Transactions.Domain.Errors;
using CashFlow.Transactions.Domain.Exceptions;
using CashFlow.Transactions.Domain.Helpers;
using CashFlow.Transactions.Domain.Interfaces.Common;
using CashFlow.Transactions.Domain.Interfaces.Service;
using CashFlow.Transactions.Domain.Models;

namespace CashFlow.Transactions.Application.Accounts.Services;

public sealed class AccountService : IAccountService
{
    private readonly IUnitOfWork _uow;
    private readonly IOutbox _outbox;
    private readonly IPiiProtector _pii;
    private readonly TimeProvider _clock;

    public AccountService(IUnitOfWork uow, IOutbox outbox, IPiiProtector pii, TimeProvider clock)
    {
        _uow = uow;
        _outbox = outbox;
        _pii = pii;
        _clock = clock;
    }

    public async Task<Result<AccountDetails>> OpenAsync(
        string holderName,
        string holderDocument,
        decimal overdraftLimit,
        CancellationToken cancellationToken)
    {
        var normalizedDocument = new string(holderDocument.Where(char.IsDigit).ToArray());
        var documentHash = _pii.BlindIndex(normalizedDocument);

        if (await _uow.AccountRepository.ExistsByDocumentHashAsync(documentHash, cancellationToken))
        {
            return AccountErrors.DuplicateHolder;
        }

        var now = _clock.GetUtcNow();
        var opened = Account.Open(
            Guid.CreateVersion7(now),
            holderName,
            _pii.Encrypt(normalizedDocument),
            documentHash,
            overdraftLimit,
            now);

        if (opened.IsFailure)
        {
            return opened.Error!;
        }

        var account = opened.Value;
        _uow.AccountRepository.Add(account);
        _outbox.Enqueue(new AccountOpenedV1(Guid.CreateVersion7(now), account.Id, now));

        try
        {
            await _uow.CommitAsync(cancellationToken);
        }
        catch (UniqueConstraintException)
        {
            return AccountErrors.DuplicateHolder;
        }

        return new AccountDetails(account, normalizedDocument);
    }

    public async Task<Result<AccountDetails>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var account = await _uow.AccountRepository.GetByIdAsync(id, cancellationToken);
        return account is null
            ? AccountErrors.NotFound
            : new AccountDetails(account, _pii.Decrypt(account.HolderDocument));
    }
}
