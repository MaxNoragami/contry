using Contry.Application.Ranked.Models;

namespace Contry.Api.Features.Ranked;

public sealed record RankedGuessResponse(
    int AttemptNumber,
    string GuessCountryId,
    string GuessCountryName,
    IReadOnlyList<RankedClueResultResponse> Results,
    DateTimeOffset CreatedAtUtc)
{
    public static RankedGuessResponse FromModel(RankedGuessRecordResult guess)
        => new(
            guess.AttemptNumber,
            guess.GuessCountryId,
            guess.GuessCountryName,
            guess.Results.Select(RankedClueResultResponse.FromModel).ToList(),
            guess.CreatedAtUtc);
}
