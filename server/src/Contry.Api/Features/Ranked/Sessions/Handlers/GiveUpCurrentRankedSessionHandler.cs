using Contry.Application.Ranked.Sessions.Commands;
using Contry.Api.Features.Ranked.Internal;
using Contry.Api.Features.Ranked.Sessions;

namespace Contry.Api.Features.Ranked.Sessions.Handlers;

public static class GiveUpCurrentRankedSessionHandler
{
    public static async Task<IResult> HandleAsync(
        HttpContext httpContext,
        GiveUpCurrentRankedSessionCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var identity = RankedIdentityResolver.RequireIdentity(httpContext);
        var session = await handler.HandleAsync(new GiveUpCurrentRankedSessionCommand(identity.UserId), cancellationToken);
        return Results.Ok(RankedSessionResponse.FromModel(session));
    }
}
