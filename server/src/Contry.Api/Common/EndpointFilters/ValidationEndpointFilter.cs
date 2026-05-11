using FluentValidation;

namespace Contry.Api.Common.EndpointFilters;

public sealed class ValidationEndpointFilter<TRequest>(IValidator<TRequest> validator) : IEndpointFilter where TRequest : class
{
    private readonly IValidator<TRequest> _validator = validator;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<TRequest>().FirstOrDefault();

        if (request is null)
        {
            return await next(context);
        }

        await _validator.ValidateAndThrowAsync(request, context.HttpContext.RequestAborted);
        return await next(context);
    }
}
