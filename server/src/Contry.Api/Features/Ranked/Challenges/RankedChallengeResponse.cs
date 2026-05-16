using Contry.Application.Ranked.Models;

namespace Contry.Api.Features.Ranked.Challenges;

public sealed record RankedChallengeResponse(
    DateOnly ChallengeDateUtc,
    IReadOnlyList<RankedClueResponse> Clues)
{
    public static RankedChallengeResponse FromModel(CurrentRankedChallengeResult challenge)
        => new(challenge.ChallengeDateUtc, challenge.Clues.Select(RankedClueResponse.FromModel).ToList());
}
