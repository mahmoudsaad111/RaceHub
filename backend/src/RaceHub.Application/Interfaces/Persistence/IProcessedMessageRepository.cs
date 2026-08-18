// RaceHub.Application/Interfaces/Persistence/IProcessedMessageRepository.cs
namespace RaceHub.Application.Interfaces.Persistence;
public interface IProcessedMessageRepository
{
    Task<bool> HasBeenProcessedAsync(Guid messageId, string consumerName, CancellationToken ct = default);
    Task MarkProcessedAsync(Guid messageId, string consumerName, CancellationToken ct = default);
}