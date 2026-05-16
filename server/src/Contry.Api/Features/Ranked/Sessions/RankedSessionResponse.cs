using Contry.Application.Ranked.Models;
using Contry.Api.Features.Ranked.Guesses;

namespace Contry.Api.Features.Ranked.Sessions;

public sealed record RankedSessionResponse(
    DateOnly ChallengeDateUtc,
    string Status,
    int GuessCount,
    DateTimeOffset? CompletedAtUtc,
    IReadOnlyList<RankedGuessResponse> Guesses)
{
    public static RankedSessionResponse FromModel(CurrentRankedSessionResult session)
        => new(
            session.ChallengeDateUtc,
            session.Status,
            session.GuessCount,
            session.CompletedAtUtc,
            session.Guesses.Select(RankedGuessResponse.FromModel).ToList());
}
