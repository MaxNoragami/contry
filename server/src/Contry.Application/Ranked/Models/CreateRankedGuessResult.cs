namespace Contry.Application.Ranked.Models;

public sealed record CreateRankedGuessResult(
    DateOnly ChallengeDateUtc,
    string Status,
    int GuessCount,
    DateTimeOffset? CompletedAtUtc,
    RankedGuessRecordResult Guess);
