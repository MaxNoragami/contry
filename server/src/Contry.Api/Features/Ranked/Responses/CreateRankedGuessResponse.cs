using Contry.Application.Ranked.Models;

namespace Contry.Api.Features.Ranked;

public sealed record CreateRankedGuessResponse(
    DateOnly ChallengeDateUtc,
    string Status,
    int GuessCount,
    DateTimeOffset? CompletedAtUtc,
    RankedGuessResponse Guess)
{
    public static CreateRankedGuessResponse FromModel(CreateRankedGuessResult result)
        => new(
            result.ChallengeDateUtc,
            result.Status,
            result.GuessCount,
            result.CompletedAtUtc,
            RankedGuessResponse.FromModel(result.Guess));
}
