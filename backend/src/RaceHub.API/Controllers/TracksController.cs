using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceHub.Application.Features.Tracks.GetTrackById;
using RaceHub.Application.Features.Tracks.GetTracks;

namespace RaceHub.API.Controllers;

[Route("api/tracks")]
[Authorize]
public class TracksController : ApiControllerBase
{
    private readonly ISender _sender;

    public TracksController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>Active track catalog, used by the lobby's "create race" track picker.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetTracksQuery(), cancellationToken);

        return HandleResult(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetTrackByIdQuery(id), cancellationToken);

        return HandleResult(result);
    }
}
