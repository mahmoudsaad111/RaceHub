using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Cars;
using RaceHub.Application.Interfaces.Persistence;

namespace RaceHub.Application.Features.Cars.GetCars;

public class GetCarsQueryHandler
    : IRequestHandler<GetCarsQuery, Result<IReadOnlyList<CarDto>>>
{
    private readonly ICarRepository _carRepository;
    private readonly IUserCarRepository _userCarRepository;

    public GetCarsQueryHandler(
        ICarRepository carRepository,
        IUserCarRepository userCarRepository)
    {
        _carRepository = carRepository;
        _userCarRepository = userCarRepository;
    }

    public async Task<Result<IReadOnlyList<CarDto>>> Handle(
        GetCarsQuery request,
        CancellationToken cancellationToken)
    {
        var cars = await _carRepository.GetAllActiveAsync(cancellationToken);

        var ownedCarIds = request.UserId.HasValue
            ? await _userCarRepository.GetOwnedCarIdsAsync(request.UserId.Value, cancellationToken)
            : new HashSet<Guid>();

        var dtos = cars
            .Select(c => new CarDto(
                c.Id,
                c.Name,
                c.TopSpeed,
                c.Acceleration,
                c.Handling,
                c.Braking,
                c.NitroCapacity,
                c.IsActive,
                c.Price,
                ownedCarIds.Contains(c.Id)))
            .ToList();

        return Result<IReadOnlyList<CarDto>>.Success(dtos);
    }
}
