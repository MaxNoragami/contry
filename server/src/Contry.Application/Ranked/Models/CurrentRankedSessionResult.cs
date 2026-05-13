namespace Contry.Application.Ranked.Models;

public sealed record CurrentRankedSessionResult(
    DateOnly ChallengeDateUtc,
    string Status,
    int GuessCount,
    DateTimeOffset? CompletedAtUtc,
    IReadOnlyList<RankedGuessRecordResult> Guesses);
