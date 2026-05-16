using Contry.Application.Ranked.Stats.Queries;
using Contry.Api.Features.Ranked.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Contry.Api.Features.Ranked.Stats.Handlers;

public static class GetMyRankedStatsHandler
{
    public static async Task<IResult> HandleAsync(
        HttpContext httpContext,
        [FromServices] GetMyRankedStatsQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var identity = RankedIdentityResolver.RequireIdentity(httpContext);
        var result = await handler.HandleAsync(new GetMyRankedStatsQuery(identity.UserId), cancellationToken);

        return TypedResults.Ok(result);
    }
}
