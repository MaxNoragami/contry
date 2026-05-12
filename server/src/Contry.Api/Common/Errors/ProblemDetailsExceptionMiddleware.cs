using Contry.Application.Errors;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Contry.Api.Common.Errors;

public sealed class ProblemDetailsExceptionMiddleware(RequestDelegate next, ILogger<ProblemDetailsExceptionMiddleware> logger, IHostEnvironment environment)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<ProblemDetailsExceptionMiddleware> _logger = logger;
    private readonly IHostEnvironment _environment = environment;

    public async Task InvokeAsync(HttpContext httpContext)
    {
        try
        {
            await _next(httpContext);
        }
        catch (Exception exception)
        {
            await HandleExceptionAsync(httpContext, exception);
        }
    }

    private async Task HandleExceptionAsync(HttpContext httpContext, Exception exception)
    {
        var traceId = httpContext.TraceIdentifier;

        switch (exception)
        {
            case BadHttpRequestException badHttpRequestException:
                _logger.LogWarning(badHttpRequestException, "Bad request for {Method} {Path}. TraceId: {TraceId}", httpContext.Request.Method, httpContext.Request.Path, traceId);
                await WriteProblemAsync(httpContext, CreateBadRequestProblem(httpContext, badHttpRequestException, traceId));
                return;

            case ValidationException validationException:
                _logger.LogWarning(validationException, "Validation failed for {Method} {Path}. TraceId: {TraceId}", httpContext.Request.Method, httpContext.Request.Path, traceId);
                await WriteProblemAsync(httpContext, CreateValidationProblem(httpContext, validationException, traceId));
                return;

            case AppException appException:
                _logger.LogWarning(appException, "Handled application exception for {Method} {Path}. TraceId: {TraceId}", httpContext.Request.Method, httpContext.Request.Path, traceId);
                await WriteProblemAsync(httpContext, CreateAppProblem(httpContext, appException, traceId));
                return;

            default:
                _logger.LogError(exception, "Unhandled exception for {Method} {Path}. TraceId: {TraceId}", httpContext.Request.Method, httpContext.Request.Path, traceId);
                await WriteProblemAsync(httpContext, CreateUnhandledProblem(httpContext, exception, traceId));
                return;
        }
    }

    private ProblemDetails CreateBadRequestProblem(HttpContext httpContext, BadHttpRequestException exception, string traceId)
    {
        var detail = _environment.IsDevelopment()
            ? exception.Message
            : "The request body or shape is invalid.";

        return CreateBaseProblem(
            httpContext,
            "/problems/request/invalid-request",
            "Invalid request.",
            exception.StatusCode,
            detail,
            traceId);
    }

    private ProblemDetails CreateAppProblem(HttpContext httpContext, AppException exception, string traceId)
    {
        var problem = CreateBaseProblem(httpContext, exception.Type, exception.Title, exception.Status, exception.Detail, traceId);

        foreach (var extension in exception.GetExtensions())
        {
            problem.Extensions[extension.Key] = extension.Value;
        }

        return problem;
    }

    private ValidationProblemDetails CreateValidationProblem(HttpContext httpContext, ValidationException exception, string traceId)
    {
        var problem = new ValidationProblemDetails(exception.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).Distinct().ToArray()))
        {
            Type = "/problems/validation",
            Title = "Validation failed.",
            Status = StatusCodes.Status400BadRequest,
            Detail = "One or more validation errors occurred.",
            Instance = httpContext.Request.Path
        };

        problem.Extensions["traceId"] = traceId;
        return problem;
    }

    private ProblemDetails CreateUnhandledProblem(HttpContext httpContext, Exception exception, string traceId)
    {
        var detail = _environment.IsDevelopment()
            ? exception.Message
            : "An unexpected error occurred while processing the request.";

        var problem = CreateBaseProblem(
            httpContext,
            "/problems/internal-server-error",
            "Internal server error.",
            StatusCodes.Status500InternalServerError,
            detail,
            traceId);

        if (_environment.IsDevelopment())
        {
            problem.Extensions["exception"] = exception.GetType().FullName;
            problem.Extensions["stackTrace"] = exception.StackTrace;
        }

        return problem;
    }

    private static ProblemDetails CreateBaseProblem(HttpContext httpContext, string type, string title, int status, string detail, string traceId)
    {
        var problem = new ProblemDetails
        {
            Type = type,
            Title = title,
            Status = status,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        problem.Extensions["traceId"] = traceId;
        return problem;
    }

    private static async Task WriteProblemAsync(HttpContext httpContext, ProblemDetails problem)
    {
        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(problem, problem.GetType());
    }
}
