using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Users;

namespace RaceHub.Application.Features.Users.GetRaceHistory;

public record GetRaceHistoryQuery(Guid UserId, int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedRaceHistoryDto>>;
