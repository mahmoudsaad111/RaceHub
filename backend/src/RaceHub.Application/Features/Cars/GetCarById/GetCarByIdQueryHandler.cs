using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Cars;
using RaceHub.Application.Interfaces.Persistence;

namespace RaceHub.Application.Features.Cars.GetCarById;

public class GetCarByIdQueryHandler
    : IRequestHandler<GetCarByIdQuery, Result<CarDto>>
{
    private readonly ICarRepository _carRepository;
    private readonly IUserCarRepository _userCarRepository;

    public GetCarByIdQueryHandler(
        ICarRepository carRepository,
        IUserCarRepository userCarRepository)
    {
        _carRepository = carRepository;
        _userCarRepository = userCarRepository;
    }

    public async Task<Result<CarDto>> Handle(
        GetCarByIdQuery request,
        CancellationToken cancellationToken)
    {
        var car = await _carRepository.GetByIdAsync(request.Id, cancellationToken);

        if (car is null)
        {
            return Result<CarDto>.Failure("Car not found.", "car_not_found");
        }

        var owned = request.UserId.HasValue
            ? await _userCarRepository.OwnsAsync(request.UserId.Value, car.Id, cancellationToken)
            : false;

        var dto = new CarDto(
            car.Id,
            car.Name,
            car.TopSpeed,
            car.Acceleration,
            car.Handling,
            car.Braking,
            car.NitroCapacity,
            car.IsActive,
            car.Price,
            owned);

        return Result<CarDto>.Success(dto);
    }
}
