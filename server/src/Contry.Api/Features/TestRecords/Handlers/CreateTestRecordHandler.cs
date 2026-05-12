using Contry.Domain.TestRecords;
using Contry.Infrastructure.Persistence;

namespace Contry.Api.Features.TestRecords.Handlers;

public static class CreateTestRecordHandler
{
    public static async Task<IResult> HandleAsync(
        CreateTestRecordRequest request,
        HttpContext httpContext,
        ContryDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var identity = TestRecordIdentityResolver.RequireIdentity(httpContext);
        var record = new TestRecord
        {
            Id = Guid.NewGuid(),
            UserId = identity.UserId,
            Name = request.Name.Trim(),
            Notes = request.Notes.Trim(),
            CreatedAtUtc = timeProvider.GetUtcNow()
        };

        dbContext.TestRecords.Add(record);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created($"/test-records/{record.Id}", TestRecordResponse.FromEntity(record));
    }
}
