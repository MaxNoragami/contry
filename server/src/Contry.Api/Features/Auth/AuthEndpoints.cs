using Contry.Api.Common.EndpointFilters;
using Contry.Api.Features.Auth.Handlers;

namespace Contry.Api.Features.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var users = app.MapGroup("/users").WithTags("Auth");
        users.MapPost(string.Empty, RegisterUserHandler.HandleAsync)
            .WithValidation<RegisterUserRequest>()
            .WithName("RegisterUser")
            .WithSummary("Register a new user account.")
            .WithDescription("Creates a new user resource, starts an authenticated session, and sets access and refresh cookies.")
            .Produces<AuthSessionResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        users.MapGet("/me", GetCurrentUserHandler.HandleAsync)
            .RequireAuthorization()
            .WithName("GetCurrentUser")
            .WithSummary("Get the authenticated user profile.")
            .WithDescription("Returns the currently authenticated user represented by the access cookie.")
            .Produces<UserResponse>()
            .Produces(StatusCodes.Status401Unauthorized);

        var sessions = app.MapGroup("/sessions").WithTags("Auth");
        sessions.MapPost(string.Empty, CreateSessionHandler.HandleAsync)
            .WithValidation<CreateSessionRequest>()
            .WithName("CreateSession")
            .WithSummary("Create a login session.")
            .WithDescription("Authenticates the user by email or username and sets access and refresh cookies.")
            .Produces<AuthSessionResponse>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status401Unauthorized);

        sessions.MapDelete("/current", DeleteCurrentSessionHandler.HandleAsync)
            .RequireXsrf()
            .WithName("DeleteCurrentSession")
            .WithSummary("Destroy the current login session.")
            .WithDescription("Revokes the current refresh-token-backed session and clears auth cookies.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        app.MapPost("/tokens/refresh", RefreshSessionHandler.HandleAsync)
            .WithTags("Auth")
            .RequireXsrf()
            .WithName("RefreshSession")
            .WithSummary("Rotate credentials for the current session.")
            .WithDescription("Rotates the refresh token, issues a fresh access token, and revokes all user sessions if refresh-token reuse is detected.")
            .Produces<AuthSessionResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/xsrf", GetXsrfTokenHandler.HandleAsync)
            .WithTags("Auth")
            .WithName("GetXsrfToken")
            .WithSummary("Mint an XSRF token for the current refresh-session family.")
            .WithDescription("Creates a signed XSRF token derived from the current refresh-session family so it stays valid across access-token refresh rotation.")
            .Produces<XsrfTokenResponse>()
            .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }
}
