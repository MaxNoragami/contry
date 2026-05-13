using Contry.Application.Ranked;

namespace Contry.Api.Features.Ranked.Handlers;

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
