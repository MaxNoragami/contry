using Contry.Api.Common.EndpointFilters;
using Contry.Api.Features.Ranked.Stats.Handlers;
using Contry.Application.Ranked.Stats.Queries;

namespace Contry.Api.Features.Ranked.Stats;

public static class RankedStatsEndpoints
{
    public static IEndpointRouteBuilder MapRankedStatsEndpoints(this IEndpointRouteBuilder app)
    {
        var stats = app.MapGroup("/ranked/stats")
            .WithTags("Ranked");

        stats.MapGet("/me", GetMyRankedStatsHandler.HandleAsync)
            .RequireAuthorization()
            .WithName("GetMyRankedStats")
            .WithSummary("Get the authenticated user's ranked stats.")
            .Produces<MyRankedStatsResult>()
            .Produces(StatusCodes.Status401Unauthorized);

        stats.MapDelete("/me", ResetMyRankedStatsHandler.HandleAsync)
            .RequireAuthorization()
            .RequireXsrf()
            .WithName("ResetMyRankedStats")
            .WithSummary("Reset the authenticated user's ranked stats.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }
}
