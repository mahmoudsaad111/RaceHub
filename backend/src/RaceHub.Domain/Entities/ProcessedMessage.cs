namespace RaceHub.Domain.Entities;

public class ProcessedMessage
{
    public Guid MessageId { get; private set; }
    public string ConsumerName { get; private set; } = default!;
    public DateTime ProcessedAtUtc { get; private set; }

    private ProcessedMessage() { }
    public ProcessedMessage(Guid messageId, string consumerName, DateTime processedAtUtc)
    {
        MessageId = messageId;
        ConsumerName = consumerName;
        ProcessedAtUtc = processedAtUtc;
    }
}

