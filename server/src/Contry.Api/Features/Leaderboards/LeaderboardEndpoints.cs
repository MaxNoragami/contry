using Contry.Api.Common.EndpointFilters;
using Contry.Api.Features.Leaderboards.Handlers;
using Contry.Application.Ranked;
using Contry.Application.Ranked.Leaderboards.Queries;

namespace Contry.Api.Features.Leaderboards;

public static class LeaderboardEndpoints
{
    public static IEndpointRouteBuilder MapLeaderboardEndpoints(this IEndpointRouteBuilder app)
    {
        var leaderboards = app.MapGroup("/leaderboards")
            .WithTags("Leaderboard");

        leaderboards.MapGet("/ranked", GetRankedLeaderboardHandler.HandleAsync)
            .WithName("GetRankedLeaderboard")
            .WithSummary("Get the global ranked leaderboard.")
            .Produces<GetRankedLeaderboardResult>();

        leaderboards.MapDelete("/ranked", ResetRankedLeaderboardAsync)
            .RequireAuthorization(policy => policy.RequireRole("ADMIN"))
            .RequireXsrf()
            .WithName("ResetRankedLeaderboard")
            .WithSummary("Admin only: reset the ranked leaderboard.")
            .WithDescription("Available only to admins. Deletes ranked user stats, clue usage, discovery data, sessions, and guesses while preserving ranked challenge history.")
            .Produces<ResetRankedLeaderboardResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }

    private static async Task<IResult> ResetRankedLeaderboardAsync(
        IRankedStore rankedStore,
        CancellationToken cancellationToken)
    {
        await rankedStore.ClearAllRankedDataAsync(cancellationToken);
        return TypedResults.Ok(new ResetRankedLeaderboardResponse(true, true));
    }
}

public sealed record ResetRankedLeaderboardResponse(
    bool LeaderboardReset,
    bool PreservedChallengeHistory);
