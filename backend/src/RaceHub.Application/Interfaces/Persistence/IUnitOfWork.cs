namespace RaceHub.Application.Interfaces.Persistence;

/// <summary>
/// Commits changes made through repositories in a single transaction.
/// Repositories only track changes in-memory (Add/Remove/mutate) —
/// nothing hits the database until SaveChangesAsync is called.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
