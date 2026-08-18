using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace RaceHub.Infrastructure.Messaging;

public static class MessagingServiceCollectionExtensions
{
    public static IServiceCollection AddRabbitMqMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RabbitMqOptions>(
            configuration.GetSection(RabbitMqOptions.SectionName));

        services.AddSingleton<IConnection>(sp =>
        {
            var options = sp
                .GetRequiredService<IOptions<RabbitMqOptions>>()
                .Value;

            var logger = sp
                .GetRequiredService<ILogger<IConnection>>();

            var factory = new ConnectionFactory
            {
                HostName = options.HostName,
                Port = options.Port,
                UserName = options.UserName,
                Password = options.Password,
                VirtualHost = options.VirtualHost,

                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval =
                    TimeSpan.FromSeconds(5)
            };

            const int maxAttempts = 10;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    return factory
                        .CreateConnectionAsync()
                        .GetAwaiter()
                        .GetResult();
                }
                catch (Exception ex) when (attempt < maxAttempts)
                {
                    var delay =
                        TimeSpan.FromSeconds(attempt * 2);

                    logger.LogWarning(
                        ex,
                        "RabbitMQ connection attempt {Attempt}/{Max} failed. Retrying in {Delay}",
                        attempt,
                        maxAttempts,
                        delay);

                    Thread.Sleep(delay);
                }
            }

            throw new InvalidOperationException(
                "Could not connect to RabbitMQ after multiple attempts.");
        });

        return services;
    }
}