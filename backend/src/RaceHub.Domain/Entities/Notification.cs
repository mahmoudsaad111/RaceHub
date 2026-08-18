namespace RaceHub.Domain.Entities;

public class Notification : BaseEntity
{
    public Guid UserId { get; private set; }

    public string Type { get; private set; } = null!;

    public string Title { get; private set; } = null!;

    public string Message { get; private set; } = null!;

    public string? Data { get; private set; }

    public bool IsRead { get; private set; }

    public DateTime? ReadAtUtc { get; private set; }

    private Notification() { }

    public Notification(Guid userId, string type, string title, string message, string? data = null)
    {
        UserId = userId;
        Type = type;
        Title = title;
        Message = message;
        Data = data;
        IsRead = false;
    }

    public void MarkAsRead()
    {
        IsRead = true;
        ReadAtUtc = DateTime.UtcNow;
    }
}
