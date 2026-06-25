namespace OrderManagement.Domain.Interfaces;

public interface IUnitOfWork
{
    Task<int> CommitAsync(CancellationToken ct = default);

    /// <summary>
    /// Executes <paramref name="action"/> inside an explicit database transaction.
    /// SaveChanges is called automatically before commit.
    /// Use this whenever you need pessimistic locks (UPDLOCK) to span both the
    /// SELECT and the subsequent UPDATE in the same transaction.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken ct = default);
}
