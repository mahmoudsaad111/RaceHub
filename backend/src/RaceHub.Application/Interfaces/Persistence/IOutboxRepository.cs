// Application/Interfaces/Persistence/IOutboxRepository.cs
using RaceHub.Domain.Entities;
namespace RaceHub.Application.Interfaces.Persistence;

public interface IOutboxRepository
{
    Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default);
}