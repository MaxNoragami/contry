namespace Contry.Application.Ranked.Models;

public sealed record RankedChallengeDefinition(
    DateOnly ChallengeDateUtc,
    string TargetCountryId,
    IReadOnlyList<RankedClueDefinition> Clues);
