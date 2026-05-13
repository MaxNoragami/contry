using System.Security.Claims;
using Contry.Application.Ranked;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Contry.Api.Features.Ranked.Handlers;

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
