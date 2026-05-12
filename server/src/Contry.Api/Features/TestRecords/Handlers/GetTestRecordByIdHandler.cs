using Contry.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Contry.Api.Features.TestRecords.Handlers;

public static class GetTestRecordByIdHandler
{
    public static async Task<IResult> HandleAsync(
        Guid id,
        HttpContext httpContext,
        ContryDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var identity = TestRecordIdentityResolver.RequireIdentity(httpContext);
        var record = await dbContext.TestRecords
            .SingleOrDefaultAsync(entity => entity.Id == id && entity.UserId == identity.UserId, cancellationToken);

        return record is null
            ? Results.NotFound()
            : Results.Ok(TestRecordResponse.FromEntity(record));
    }
}
