using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Contry.Api.Common.OpenApi;

public sealed class XsrfOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var requiresXsrf = context.ApiDescription.ActionDescriptor.EndpointMetadata
            .OfType<RequireXsrfMetadata>()
            .Any();

        if (!requiresXsrf)
        {
            return;
        }

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("xsrf")] = []
        });
    }
}
