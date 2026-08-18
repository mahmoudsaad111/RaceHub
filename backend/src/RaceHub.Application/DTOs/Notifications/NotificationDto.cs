namespace RaceHub.Application.DTOs.Notifications;

public record NotificationDto(
    Guid NotificationId,
    string Type,
    string Title,
    string Message,
    string? Data,
    bool IsRead,
    DateTime CreatedAtUtc);
