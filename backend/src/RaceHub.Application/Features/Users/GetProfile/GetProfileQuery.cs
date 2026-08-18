using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Users;

namespace RaceHub.Application.Features.Users.GetProfile;

public record GetProfileQuery(Guid UserId) : IRequest<Result<ProfileDto>>;
