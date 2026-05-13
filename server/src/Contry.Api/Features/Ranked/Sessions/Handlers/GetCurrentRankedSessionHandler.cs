using Contry.Application.Ranked.Sessions.Queries;
using Contry.Api.Features.Ranked.Internal;
using Contry.Api.Features.Ranked.Sessions;

namespace Contry.Api.Features.Ranked.Sessions.Handlers;

public static class GetCurrentRankedSessionHandler
{
    public static async Task<IResult> HandleAsync(
        HttpContext httpContext,
        GetCurrentRankedSessionQueryHandler getCurrentRankedSessionQueryHandler,
        CancellationToken cancellationToken)
    {
        var identity = RankedIdentityResolver.RequireIdentity(httpContext);
        var session = await getCurrentRankedSessionQueryHandler.HandleAsync(new GetCurrentRankedSessionQuery(identity.UserId), cancellationToken);
        return Results.Ok(RankedSessionResponse.FromModel(session));
    }
}
