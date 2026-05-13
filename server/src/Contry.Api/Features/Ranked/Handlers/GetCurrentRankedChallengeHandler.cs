using Contry.Application.Ranked;

namespace Contry.Api.Features.Ranked.Handlers;

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
