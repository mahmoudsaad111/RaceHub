// Infrastructure/Persistence/Repositories/OutboxRepository.cs
using RaceHub.Application.Interfaces.Persistence;
using RaceHub.Domain.Entities;
using RaceHub.Infrastructure.Persistence;
namespace RaceHub.Infrastructure.Persistence.Repositories;  
public class OutboxRepository : IOutboxRepository
{
    private readonly AppDbContext _context;
    public OutboxRepository(AppDbContext context) => _context = context;


    public Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default)
        => _context.OutboxMessages.AddAsync(message, cancellationToken).AsTask();
}