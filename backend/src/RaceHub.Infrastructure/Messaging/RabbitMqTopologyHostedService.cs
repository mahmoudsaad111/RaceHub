using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;

namespace RaceHub.Infrastructure.Messaging;

public class RabbitMqTopologyHostedService : IHostedService
{
    private readonly IConnection _connection;

    public RabbitMqTopologyHostedService(IConnection connection)
    {
        _connection = connection;
    }

    public async Task StartAsync(
        CancellationToken cancellationToken)
    {
        var channel = await _connection.CreateChannelAsync(
            cancellationToken: cancellationToken);

        await TopologyDeclarer.DeclareAsync(
            channel,
            cancellationToken);
    }

    public Task StopAsync(
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}