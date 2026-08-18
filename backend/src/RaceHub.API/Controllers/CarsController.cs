using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceHub.Application.Features.Cars.BuyCar;
using RaceHub.Application.Features.Cars.GetCarById;
using RaceHub.Application.Features.Cars.GetCars;

namespace RaceHub.API.Controllers;

[Route("api/cars")]
[Authorize]
public class CarsController : ApiControllerBase
{
    private readonly ISender _sender;

    public CarsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>Returns the full active car catalog with ownership info for the current user.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        var result = await _sender.Send(new GetCarsQuery(userId), cancellationToken);

        return HandleResult(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        var result = await _sender.Send(new GetCarByIdQuery(id, userId), cancellationToken);

        return HandleResult(result);
    }

    /// <summary>Purchases a car for the authenticated user.</summary>
    [HttpPost("{id:guid}/buy")]
    public async Task<IActionResult> Buy(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new BuyCarCommand(id, CurrentUserId), cancellationToken);

        return HandleResult(result, "Car purchased successfully.");
    }

    private Guid CurrentUserId
    {
        get
        {
            var claim = User.FindFirst("userId")?.Value
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            return Guid.TryParse(claim, out var id)
                ? id
                : throw new UnauthorizedAccessException("No valid userId claim on the access token.");
        }
    }
}