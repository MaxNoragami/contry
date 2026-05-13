using Contry.Application.Ranked;

namespace Contry.Api.Features.Ranked.Handlers;

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
