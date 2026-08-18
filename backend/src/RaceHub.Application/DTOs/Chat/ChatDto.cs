namespace RaceHub.Application.DTOs.Chat;

public record ChatMessageDto(
    Guid MessageId,
    Guid ConversationId,
    Guid SenderId,
    string SenderDisplayName,
    string Content,
    DateTime SentAtUtc,
    bool IsRead);

public record ConversationDto(
    Guid ConversationId,
    string Type,
    string Name,
    string? Avatar,
    Guid OtherUserId,
    string OtherUserDisplayName,
    int UnreadCount,
    ChatMessageDto? LastMessage);
