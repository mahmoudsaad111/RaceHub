using MediatR;
using RaceHub.Application.Common;

namespace RaceHub.Application.Features.Cars.BuyCar;

public record BuyCarCommand(Guid CarId, Guid UserId) : IRequest<Result>;
