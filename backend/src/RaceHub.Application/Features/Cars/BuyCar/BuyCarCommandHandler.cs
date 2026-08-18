using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.Interfaces.Persistence;
using RaceHub.Domain.Entities;

namespace RaceHub.Application.Features.Cars.BuyCar;

public class BuyCarCommandHandler : IRequestHandler<BuyCarCommand, Result>
{
    private readonly ICarRepository _carRepository;
    private readonly IUserCarRepository _userCarRepository;
    private readonly IUserRewardRepository _userRewardRepository;
    private readonly IUnitOfWork _unitOfWork;

    public BuyCarCommandHandler(
        ICarRepository carRepository,
        IUserCarRepository userCarRepository,
        IUserRewardRepository userRewardRepository,
        IUnitOfWork unitOfWork)
    {
        _carRepository = carRepository;
        _userCarRepository = userCarRepository;
        _userRewardRepository = userRewardRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(BuyCarCommand request, CancellationToken cancellationToken)
    {
        var car = await _carRepository.GetByIdAsync(request.CarId, cancellationToken);
        if (car is null)
        {
            return Result.Failure("Car not found.", "car_not_found");
        }

        if (!car.IsActive)
        {
            return Result.Failure("This car is no longer available.", "car_not_available");
        }

        if (car.Price <= 0)
        {
            return Result.Failure("This car is free and does not need to be purchased.", "car_is_free");
        }

        var alreadyOwned = await _userCarRepository.OwnsAsync(request.UserId, car.Id, cancellationToken);
        if (alreadyOwned)
        {
            return Result.Failure("You already own this car.", "car_already_owned");
        }

        var user = await _userRewardRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure("User not found.", "user_not_found");
        }

        if (!user.SpendCoins((int)car.Price))
        {
            return Result.Failure("Not enough coins.", "insufficient_coins");
        }

        await _userCarRepository.AddAsync(new UserCar(request.UserId, car.Id), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
