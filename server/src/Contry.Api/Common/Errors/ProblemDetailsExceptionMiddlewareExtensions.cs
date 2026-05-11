namespace Contry.Api.Common.Errors;

public static class ProblemDetailsExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseProblemDetailsExceptionMiddleware(this IApplicationBuilder app)
        => app.UseMiddleware<ProblemDetailsExceptionMiddleware>();
}
