using Contry.Api.Common.EndpointFilters;
using Contry.Api.Features.TestRecords.Handlers;

namespace Contry.Api.Features.TestRecords;

public static class TestRecordEndpoints
{
    public static IEndpointRouteBuilder MapTestRecordEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/test-records")
            .WithTags("Test Records");

        group.MapPost(string.Empty, CreateTestRecordHandler.HandleAsync)
            .WithValidation<CreateTestRecordRequest>()
            .RequireXsrf()
            .WithName("CreateTestRecord")
            .WithSummary("Create a protected test record.")
            .WithDescription("Creates a sample record owned by the authenticated user to exercise protected write flows.")
            .Produces<TestRecordResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/{id:guid}", GetTestRecordByIdHandler.HandleAsync)
            .RequireAuthorization()
            .WithName("GetTestRecordById")
            .WithSummary("Get a protected test record by id.")
            .WithDescription("Returns a sample record owned by the authenticated user to exercise protected read flows.")
            .Produces<TestRecordResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
