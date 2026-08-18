using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Tracks;

namespace RaceHub.Application.Features.Users.GetPersonalBests;

public record GetPersonalBestsQuery(Guid UserId)
    : IRequest<Result<IReadOnlyList<PersonalBestDto>>>;
