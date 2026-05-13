using Contry.Application.Ranked;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Contry.Api.Features.Ranked.Handlers;

public static class GetRankedLeaderboardHandler
{
    public static async Task<IResult> HandleAsync(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromServices] GetRankedLeaderboardQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetRankedLeaderboardQuery(page ?? 1, pageSize ?? 7), cancellationToken);
        return TypedResults.Ok(result);
    }
}
