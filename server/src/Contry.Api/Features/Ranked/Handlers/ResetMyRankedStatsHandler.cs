using System.Security.Claims;
using Contry.Application.Ranked;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Contry.Api.Features.Ranked.Handlers;

public static class ResetMyRankedStatsHandler
{
    public static async Task<IResult> HandleAsync(
        HttpContext httpContext,
        [FromServices] ResetMyRankedStatsCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var identity = RankedIdentityResolver.RequireIdentity(httpContext);
        await handler.HandleAsync(new ResetMyRankedStatsCommand(identity.UserId), cancellationToken);

        return TypedResults.NoContent();
    }
}
