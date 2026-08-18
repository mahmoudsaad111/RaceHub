using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Friends;
using RaceHub.Application.Interfaces;
using RaceHub.Application.Interfaces.Persistence;
using RaceHub.Domain.Enums;

namespace RaceHub.Application.Features.Friends.GetFriends;

public class GetFriendsQueryHandler
    : IRequestHandler<GetFriendsQuery, Result<IReadOnlyList<FriendDto>>>
{
    private readonly IFriendshipRepository _friendshipRepository;
    private readonly IRaceRepository _raceRepository;
    private readonly IPresenceTracker _presenceTracker;

    public GetFriendsQueryHandler(
        IFriendshipRepository friendshipRepository,
        IRaceRepository raceRepository,
        IPresenceTracker presenceTracker)
    {
        _friendshipRepository = friendshipRepository;
        _raceRepository = raceRepository;
        _presenceTracker = presenceTracker;
    }

    public async Task<Result<IReadOnlyList<FriendDto>>> Handle(
        GetFriendsQuery request,
        CancellationToken cancellationToken)
    {
        var friendships = await _friendshipRepository.GetAcceptedForUserAsync(request.UserId, cancellationToken);

        var friendIds = friendships
            .Select(f => f.RequesterId == request.UserId ? f.AddresseeId : f.RequesterId)
            .ToList();

        // One batched query for every friend's active room instead of one
        // query per friend — see IRaceRepository.GetActiveRacesForUsersAsync.
        var activeRaces = await _raceRepository.GetActiveRacesForUsersAsync(friendIds, cancellationToken);

        // A friend could theoretically be a player in more than one active
        // race (nothing stops joining multiple different rooms); First()
        // picks whichever a lookup finds first, which is an acceptable
        // simplification for a "here's roughly where they are" indicator.
        //
        // Filtered to the *player's own* status, not just the race's
        // overall status: GetActiveRacesForUsersAsync only excludes races
        // that are RaceStatus.Finished, but a race can sit at Running
        // forever if one player abandons it without finishing (see
        // RaceHub.MarkDisconnectedInActiveRacesAsync). A Disconnected
        // player should stop showing as "in this race" to friends the
        // moment they drop, not only once the whole race eventually wraps
        // up (which might never happen if nobody else finishes either).
        var raceByUserId = activeRaces
            .SelectMany(race => race.Players
                .Where(p => p.Status != PlayerRaceStatus.Disconnected)
                .Select(p => (p.UserId, Race: race)))
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.First().Race);

        var friends = friendships
            .Select(f =>
            {
                // The "other" user in the relationship, regardless of who
                // originally sent the request.
                var other = f.RequesterId == request.UserId ? f.Addressee : f.Requester;

                FriendCurrentRaceDto? currentRace = null;

                if (raceByUserId.TryGetValue(other.Id, out var race))
                {
                    currentRace = new FriendCurrentRaceDto(
                        race.Id,
                        race.Track.Name,
                        race.Players.Count,
                        race.MaxPlayers,
                        race.Status.ToString());
                }

                return new FriendDto(
                    other.Id,
                    other.DisplayName,
                    _presenceTracker.IsOnline(other.Id),
                    currentRace);
            })
            .ToList();

        return Result<IReadOnlyList<FriendDto>>.Success(friends);
    }
}
