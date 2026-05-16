using Contry.Api.Common.EndpointFilters;
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

        app.MapPost("/ranked/sessions/current/give-up", GiveUpCurrentRankedSessionHandler.HandleAsync)
            .WithTags("Ranked")
            .RequireAuthorization()
            .RequireXsrf()
            .WithName("GiveUpCurrentRankedSession")
            .WithSummary("Give up on the current ranked session.")
            .WithDescription("Marks the authenticated user's current ranked session as lost, reveals the target country, and records the result as a DNF in ranked stats.")
            .Produces<RankedSessionResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
