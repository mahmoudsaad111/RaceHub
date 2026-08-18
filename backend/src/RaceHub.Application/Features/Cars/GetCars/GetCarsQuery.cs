using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Cars;

namespace RaceHub.Application.Features.Cars.GetCars;

public record GetCarsQuery(Guid? UserId = null) : IRequest<Result<IReadOnlyList<CarDto>>>;
