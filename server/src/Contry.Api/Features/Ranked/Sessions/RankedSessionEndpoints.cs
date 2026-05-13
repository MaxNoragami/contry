using Contry.Api.Features.Ranked.Sessions.Handlers;

namespace Contry.Api.Features.Ranked.Sessions;

public static class RankedSessionEndpoints
{
    public static IEndpointRouteBuilder MapRankedSessionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/ranked/sessions/current", GetCurrentRankedSessionHandler.HandleAsync)
            .WithTags("Ranked")
            .RequireAuthorization()
            .WithName("GetCurrentRankedSession")
            .WithSummary("Get the authenticated user's current ranked session.")
            .WithDescription("Returns the authenticated user's current daily ranked session state and evaluated guess history, or not_started if no ranked guess was made yet today.")
            .Produces<RankedSessionResponse>()
            .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }
}
