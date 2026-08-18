namespace RaceHub.Domain.Entities;

public class ChatMessage : BaseEntity
{
    public Guid ConversationId { get; private set; }

    public Guid SenderId { get; private set; }

    public string Content { get; private set; } = null!;

    public DateTime SentAtUtc { get; private set; } = DateTime.UtcNow;

    public bool IsRead { get; private set; }

    private ChatMessage() { }

    public ChatMessage(Guid conversationId, Guid senderId, string content)
    {
        ConversationId = conversationId;
        SenderId = senderId;
        Content = content;
        SentAtUtc = DateTime.UtcNow;
        IsRead = false;
    }

    public void MarkAsRead()
    {
        IsRead = true;
    }
}
