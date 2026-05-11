using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Contry.Api.Common.OpenApi;

public sealed class XsrfOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var relativePath = "/" + (context.ApiDescription.RelativePath?.TrimStart('/') ?? string.Empty);
        var method = context.ApiDescription.HttpMethod?.ToUpperInvariant();
        var requiresXsrf = (relativePath, method) switch
        {
            ("/tokens/refresh", "POST") => true,
            ("/sessions/current", "DELETE") => true,
            _ => false
        };

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
