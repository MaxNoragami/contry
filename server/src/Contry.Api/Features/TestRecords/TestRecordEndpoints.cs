using Contry.Api.Common.EndpointFilters;
using Contry.Api.Common.Security;
using Contry.Application.Errors;
using Contry.Domain.TestRecords;
using Contry.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Contry.Api.Features.TestRecords;

public static class TestRecordEndpoints
{
    public static IEndpointRouteBuilder MapTestRecordEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/test-records")
            .WithTags("Test Records")
            .RequireAuthorization();

        group.MapPost(string.Empty, CreateTestRecordAsync)
            .WithValidation<CreateTestRecordRequest>()
            .RequireXsrf()
            .WithName("CreateTestRecord")
            .WithSummary("Create a protected test record.")
            .WithDescription("Creates a sample record owned by the authenticated user to exercise protected write flows.")
            .Produces<TestRecordResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/{id:guid}", GetTestRecordByIdAsync)
            .WithName("GetTestRecordById")
            .WithSummary("Get a protected test record by id.")
            .WithDescription("Returns a sample record owned by the authenticated user to exercise protected read flows.")
            .Produces<TestRecordResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> CreateTestRecordAsync(
        CreateTestRecordRequest request,
        HttpContext httpContext,
        ContryDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var identity = RequireIdentity(httpContext);
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

    private static async Task<IResult> GetTestRecordByIdAsync(
        Guid id,
        HttpContext httpContext,
        ContryDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var identity = RequireIdentity(httpContext);
        var record = await dbContext.TestRecords
            .SingleOrDefaultAsync(entity => entity.Id == id && entity.UserId == identity.UserId, cancellationToken);

        return record is null
            ? Results.NotFound()
            : Results.Ok(TestRecordResponse.FromEntity(record));
    }

    private static Contry.Application.Auth.AccessTokenIdentity RequireIdentity(HttpContext httpContext)
    {
        if (!AccessTokenIdentityResolver.TryResolve(httpContext.User, out var identity) || identity is null)
        {
            throw new InvalidAccessTokenException();
        }

        return identity;
    }
}
