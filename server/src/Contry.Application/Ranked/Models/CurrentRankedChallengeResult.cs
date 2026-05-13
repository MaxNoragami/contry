namespace Contry.Application.Ranked.Models;

public sealed record CurrentRankedChallengeResult(
    DateOnly ChallengeDateUtc,
    IReadOnlyList<RankedClueDefinition> Clues);
