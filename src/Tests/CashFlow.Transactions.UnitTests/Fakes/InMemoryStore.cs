using CashFlow.Transactions.Application.Abstractions.Messaging;
using CashFlow.Transactions.Application.IntegrationEvents;
using CashFlow.Transactions.Application.Interfaces;
using CashFlow.Transactions.Domain.Enums;
using CashFlow.Transactions.Domain.Errors;
using CashFlow.Transactions.Domain.Exceptions;
using CashFlow.Transactions.Domain.Interfaces.Common;
using CashFlow.Transactions.Domain.Interfaces.Repository;
using CashFlow.Transactions.Domain.Models;

namespace CashFlow.Transactions.UnitTests.Fakes;

internal sealed class InMemoryStore : IAccountRepository, ITransactionRepository, IUnitOfWork, IOutbox
{
    private readonly List<Transaction> _pendingTransactions = [];
    private readonly List<object> _pendingEvents = [];

    public Dictionary<Guid, Account> Accounts { get; } = [];

    public List<Transaction> Transactions { get; } = [];

    public List<object> PublishedToOutbox { get; } = [];

    public Queue<Exception> FailuresOnSave { get; } = new();

    public Action? OnInjectedFailure { get; set; }

    public int SaveCalls { get; private set; }

    public IAccountRepository AccountRepository => this;

    public ITransactionRepository TransactionRepository => this;

    public Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Accounts.GetValueOrDefault(id));

    public Task<bool> ExistsByDocumentHashAsync(string documentHash, CancellationToken cancellationToken) =>
        Task.FromResult(Accounts.Values.Any(a => a.HolderDocumentHash == documentHash));

    public void Add(Account account) => Accounts[account.Id] = account;

    public Task<Transaction?> GetByIdempotencyKeyAsync(Guid accountId, string idempotencyKey, CancellationToken cancellationToken)
        => Task.FromResult(Transactions.FirstOrDefault(t => t.AccountId == accountId && t.IdempotencyKey == idempotencyKey));

    Task<Transaction?> ITransactionRepository.GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Transactions.FirstOrDefault(t => t.Id == id));

    public Task<IReadOnlyList<Transaction>> ListByAccountAsync(Guid accountId, DateTimeOffset? from, DateTimeOffset? to, int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Transaction>>(Transactions.Where(t => t.AccountId == accountId).Take(limit).ToList());

    public void Add(Transaction transaction) => _pendingTransactions.Add(transaction);

    public void Enqueue<TEvent>(TEvent integrationEvent)
        where TEvent : IIntegrationEvent => _pendingEvents.Add(integrationEvent);

    public Task CommitAsync(CancellationToken cancellationToken)
    {
        SaveCalls++;
        if (FailuresOnSave.TryDequeue(out var failure))
        {
            OnInjectedFailure?.Invoke();
            throw failure;
        }

        Transactions.AddRange(_pendingTransactions);
        PublishedToOutbox.AddRange(_pendingEvents);
        _pendingTransactions.Clear();
        _pendingEvents.Clear();
        return Task.CompletedTask;
    }

    public void Reset()
    {
        _pendingTransactions.Clear();
        _pendingEvents.Clear();
    }
}
