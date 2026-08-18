using RaceHub.Application.Interfaces.Persistence;
using RaceHub.Domain.Entities;
using RaceHub.API.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace RaceHub.API.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IChatMessageRepository _chatMessageRepository;
    private readonly INotificationRepository _notificationRepository;
    private readonly IFriendshipRepository _friendshipRepository;

    public ChatHub(
        IChatMessageRepository chatMessageRepository,
        INotificationRepository notificationRepository,
        IFriendshipRepository friendshipRepository)
    {
        _chatMessageRepository = chatMessageRepository;
        _notificationRepository = notificationRepository;
        _friendshipRepository = friendshipRepository;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();

        await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
        await base.OnConnectedAsync();
    }

    public async Task JoinConversation(string conversationId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"chat-{conversationId}");
        await _chatMessageRepository.MarkAsReadAsync(Guid.Parse(conversationId), GetUserId());
    }

    public async Task LeaveConversation(string conversationId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"chat-{conversationId}");
    }

    public async Task SendFriendMessage(Guid friendId, string content)
    {
        var senderId = GetUserId();

        var friendship = await _friendshipRepository.GetBetweenAsync(senderId, friendId);
        if (friendship is null)
        {
            await Clients.Caller.SendAsync("ChatError", new { error = "You are not friends with this user.", code = "not_friends" });
            return;
        }

        var conversationId = $"dm-{MinId(senderId, friendId)}";
        var message = new ChatMessage(Guid.Parse(conversationId), senderId, content);

        await _chatMessageRepository.AddAsync(message);
        await _chatMessageRepository.SaveChangesAsync();

        var dto = new
        {
            messageId = message.Id,
            conversationId = message.ConversationId,
            senderId = message.SenderId,
            content = message.Content,
            sentAtUtc = message.SentAtUtc,
            isRead = message.IsRead
        };

        await Clients.Group($"chat-{conversationId}").SendAsync("ReceiveMessage", dto);
        await Clients.Group($"user-{friendId}").SendAsync("NewMessageNotification", new { conversationId, message = dto });
    }

    public async Task SendRaceMessage(Guid raceId, string content)
    {
        var senderId = GetUserId();

        var conversationId = $"race-{raceId}";
        var message = new ChatMessage(Guid.Parse(conversationId), senderId, content);

        await _chatMessageRepository.AddAsync(message);
        await _chatMessageRepository.SaveChangesAsync();

        var dto = new
        {                                                                                                        
            messageId = message.Id,
            conversationId = message.ConversationId,
            senderId = message.SenderId,
            content = message.Content,
            sentAtUtc = message.SentAtUtc,
            isRead = message.IsRead
        };

        await Clients.Group($"chat-{conversationId}").SendAsync("ReceiveMessage", dto);
    }

    public async Task<IReadOnlyList<object>> GetConversationHistory(string conversationId, int skip = 0, int take = 50)
    {
        var messages = await _chatMessageRepository.GetConversationAsync(
            Guid.Parse(conversationId), skip, take);

        return messages.Select(m => new
        {
            messageId = m.Id,
            conversationId = m.ConversationId,
            senderId = m.SenderId,
            content = m.Content,
            sentAtUtc = m.SentAtUtc,
            isRead = m.IsRead
        }).ToList();
    }

    private Guid GetUserId()
    {
        var claim = Context.User?.FindFirst("userId")?.Value
            ?? Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(claim, out var id)
            ? id
            : throw new HubException("No valid userId claim on the connection.");
    }

    private static string MinId(Guid a, Guid b) => a.CompareTo(b) < 0 ? a.ToString() : b.ToString();
}
