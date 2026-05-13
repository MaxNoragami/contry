using System.Text;
using Contry.Infrastructure.Datasets;

namespace Contry.Api.Features.Datasets;

public static class DatasetEndpoints
{
    public static IEndpointRouteBuilder MapDatasetEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/datasets/{**assetPath}", HandleGetAsync)
            .WithTags("Datasets")
            .WithName("GetDatasetAsset")
            .WithSummary("Get a built-in dataset asset.")
            .WithDescription("Returns a canonical built-in dataset asset from the backend dataset catalog, including the manifest and built-in CSV files.")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> HandleGetAsync(string assetPath, BuiltInDatasetCatalog builtInDatasetCatalog, CancellationToken cancellationToken)
    {
        var normalizedPath = $"/datasets/{assetPath.TrimStart('/')}";
        var document = await builtInDatasetCatalog.FindByPathAsync(normalizedPath, cancellationToken);

        if (document is null)
        {
            return Results.NotFound();
        }

        return Results.Text(document.Content, document.ContentType, Encoding.UTF8);
    }
}
