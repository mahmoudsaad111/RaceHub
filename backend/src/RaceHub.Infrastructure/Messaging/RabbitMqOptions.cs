// RaceHub.Infrastructure/Messaging/RabbitMqOptions.cs
namespace RaceHub.Infrastructure.Messaging;
public class RabbitMqOptions
{
    public const string SectionName = "RabbitMQ";
    public string HostName { get; set; } = null!;
    public int Port { get; set; }
    public string UserName { get; set; }=  null!;
    public string Password { get; set; } = null!;
    public string VirtualHost { get; set; } = null!;

    /// <summary>
    /// Base URL of the RabbitMQ management HTTP API (the rabbitmq:3-management
    /// image serves it on 15672). Only used by the API's messaging
    /// diagnostics endpoint — never by the AMQP clients. Optional: falls
    /// back to http://{HostName}:15672 so the common case (management API
    /// on the broker host, standard port) needs no extra config.
    /// </summary>
    public string? ManagementUrl { get; set; }
}
