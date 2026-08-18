using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Cars;

namespace RaceHub.Application.Features.Cars.GetCarById;

public record GetCarByIdQuery(Guid Id, Guid? UserId = null) : IRequest<Result<CarDto>>;
