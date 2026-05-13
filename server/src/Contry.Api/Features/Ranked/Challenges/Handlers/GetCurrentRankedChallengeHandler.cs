using Contry.Application.Ranked.Challenges.Queries;
using Contry.Api.Features.Ranked.Challenges;

namespace Contry.Api.Features.Ranked.Challenges.Handlers;

public static class GetCurrentRankedChallengeHandler
{
    public static async Task<IResult> HandleAsync(
        GetCurrentRankedChallengeQueryHandler getCurrentRankedChallengeQueryHandler,
        CancellationToken cancellationToken)
    {
        var challenge = await getCurrentRankedChallengeQueryHandler.HandleAsync(new GetCurrentRankedChallengeQuery(), cancellationToken);
        return Results.Ok(RankedChallengeResponse.FromModel(challenge));
    }
}
