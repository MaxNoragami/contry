using Contry.Api.Common.EndpointFilters;
using Contry.Api.Features.Ranked.Handlers;

namespace Contry.Api.Features.Ranked;

public static class RankedEndpoints
{
    public static IEndpointRouteBuilder MapRankedEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/ranked-challenges/current", GetCurrentRankedChallengeHandler.HandleAsync)
            .WithTags("Ranked")
            .RequireAuthorization()
            .WithName("GetCurrentRankedChallenge")
            .WithSummary("Get the current ranked challenge metadata.")
            .WithDescription("Returns the current UTC daily ranked clue set without exposing the hidden target country.")
            .Produces<RankedChallengeResponse>()
            .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/ranked-sessions/current", GetCurrentRankedSessionHandler.HandleAsync)
            .WithTags("Ranked")
            .RequireAuthorization()
            .WithName("GetCurrentRankedSession")
            .WithSummary("Get the authenticated user's current ranked session.")
            .WithDescription("Returns the authenticated user's current daily ranked session state and evaluated guess history, or not_started if no ranked guess was made yet today.")
            .Produces<RankedSessionResponse>()
            .Produces(StatusCodes.Status401Unauthorized);

        app.MapPost("/ranked-guesses", CreateRankedGuessHandler.HandleAsync)
            .WithTags("Ranked")
            .WithValidation<CreateRankedGuessRequest>()
            .RequireXsrf()
            .WithName("CreateRankedGuess")
            .WithSummary("Submit a ranked guess.")
            .WithDescription("Evaluates a ranked guess on the server against the hidden daily country and returns the authoritative guess result.")
            .Produces<CreateRankedGuessResponse>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
