namespace Contry.Application.Ranked.Models;

public sealed record RankedGuessRecordResult(
    int AttemptNumber,
    string GuessCountryId,
    string GuessCountryName,
    IReadOnlyList<RankedClueResult> Results,
    DateTimeOffset CreatedAtUtc);
