using RaceHub.Domain.Entities;

namespace RaceHub.Application.Interfaces.Persistence;

public interface IChatMessageRepository
{
    Task AddAsync(ChatMessage message, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatMessage>> GetConversationAsync(
        Guid conversationId,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    Task MarkAsReadAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default);
}
