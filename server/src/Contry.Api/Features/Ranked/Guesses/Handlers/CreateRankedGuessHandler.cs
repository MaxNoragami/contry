using Contry.Application.Ranked.Guesses.Commands;
using Contry.Api.Features.Ranked.Guesses;
using Contry.Api.Features.Ranked.Internal;

namespace Contry.Api.Features.Ranked.Guesses.Handlers;

public static class CreateRankedGuessHandler
{
    public static async Task<IResult> HandleAsync(
        CreateRankedGuessRequest request,
        HttpContext httpContext,
        CreateRankedGuessCommandHandler createRankedGuessCommandHandler,
        CancellationToken cancellationToken)
    {
        var identity = RankedIdentityResolver.RequireIdentity(httpContext);
        var result = await createRankedGuessCommandHandler.HandleAsync(new CreateRankedGuessCommand(identity.UserId, request.CountryId), cancellationToken);
        return Results.Ok(CreateRankedGuessResponse.FromModel(result));
    }
}
