using Microsoft.AspNetCore.SignalR;

namespace RaceHub.API.Hubs;

public class RaceHubUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User?.FindFirst("userId")?.Value;
}
