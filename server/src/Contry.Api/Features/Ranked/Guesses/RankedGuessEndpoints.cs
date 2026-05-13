using Contry.Api.Common.EndpointFilters;
using Contry.Api.Features.Ranked.Guesses.Handlers;

namespace Contry.Api.Features.Ranked.Guesses;

public static class RankedGuessEndpoints
{
    public static IEndpointRouteBuilder MapRankedGuessEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/ranked/guesses", CreateRankedGuessHandler.HandleAsync)
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
