using FluentValidation;
using Contry.Api.Common.OpenApi;

namespace Contry.Api.Common.EndpointFilters;

public static class EndpointFilterExtensions
{
    public static RouteHandlerBuilder WithValidation<TRequest>(this RouteHandlerBuilder builder) where TRequest : class
        => builder.AddEndpointFilter<ValidationEndpointFilter<TRequest>>();

    public static RouteHandlerBuilder RequireXsrf(this RouteHandlerBuilder builder)
        => builder
            .WithMetadata(new RequireXsrfMetadata());
}
