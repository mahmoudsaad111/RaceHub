using RaceHub.Application.DTOs.Races;

namespace RaceHub.Application.Features.Races.FinishRace;

/// <summary>
/// FinalResults is null unless this was the last player to finish —
/// RaceHub uses that to decide whether to also broadcast "RaceFinished"
/// alongside "PlayerFinished".
/// </summary>
public record FinishPlayerResult(
    PlayerFinishedDto PlayerFinished,
    bool RaceFinished,
    RaceFinishedDto? FinalResults);
