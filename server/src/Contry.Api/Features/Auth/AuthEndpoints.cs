using Contry.Api.Common.EndpointFilters;
using Contry.Api.Common.Security;
using Contry.Application.Auth;
using Contry.Application.Errors;
using Contry.Infrastructure.Authentication;
using Contry.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Contry.Api.Features.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var users = app.MapGroup("/users").WithTags("Auth");
        users.MapPost(string.Empty, RegisterUserAsync)
            .WithValidation<RegisterUserRequest>()
            .WithName("RegisterUser")
            .WithSummary("Register a new user account.")
            .WithDescription("Creates a new user resource, starts an authenticated session, and sets access and refresh cookies.")
            .Produces<AuthSessionResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        users.MapGet("/me", GetCurrentUserAsync)
            .RequireAuthorization()
            .WithName("GetCurrentUser")
            .WithSummary("Get the authenticated user profile.")
            .WithDescription("Returns the currently authenticated user represented by the access cookie.")
            .Produces<UserResponse>()
            .Produces(StatusCodes.Status401Unauthorized);

        var sessions = app.MapGroup("/sessions").WithTags("Auth");
        sessions.MapPost(string.Empty, CreateSessionAsync)
            .WithValidation<CreateSessionRequest>()
            .WithName("CreateSession")
            .WithSummary("Create a login session.")
            .WithDescription("Authenticates the user by email or username and sets access and refresh cookies.")
            .Produces<AuthSessionResponse>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status401Unauthorized);

        sessions.MapDelete("/current", DeleteCurrentSessionAsync)
            .RequireXsrf()
            .WithName("DeleteCurrentSession")
            .WithSummary("Destroy the current login session.")
            .WithDescription("Revokes the current refresh-token-backed session and clears auth cookies.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        app.MapPost("/tokens/refresh", RefreshSessionAsync)
            .WithTags("Auth")
            .RequireXsrf()
            .WithName("RefreshSession")
            .WithSummary("Rotate credentials for the current session.")
            .WithDescription("Rotates the refresh token, issues a fresh access token, and revokes all user sessions if refresh-token reuse is detected.")
            .Produces<AuthSessionResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/xsrf", GetXsrfTokenAsync)
            .WithTags("Auth")
            .WithName("GetXsrfToken")
            .WithSummary("Mint an XSRF token for the current refresh-session family.")
            .WithDescription("Creates a signed XSRF token derived from the current refresh-session family so it stays valid across access-token refresh rotation.")
            .Produces<XsrfTokenResponse>()
            .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static async Task<IResult> RegisterUserAsync(
        RegisterUserRequest request,
        AuthSessionService authSessionService,
        AuthCookieService authCookieService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var (user, session) = await authSessionService.RegisterUserAsync(request.Username, request.Email, request.Password, cancellationToken);
        WriteCookies(authCookieService, httpContext, session);
        return Results.Created($"/users/{user.Id}", new AuthSessionResponse(UserResponse.FromUser(user), session.AccessTokenExpiresAtUtc, session.RefreshTokenExpiresAtUtc));
    }

    private static async Task<IResult> CreateSessionAsync(
        CreateSessionRequest request,
        AuthSessionService authSessionService,
        AuthCookieService authCookieService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var (user, session) = await authSessionService.CreateSessionAsync(request.Credential, request.Password, cancellationToken);
        WriteCookies(authCookieService, httpContext, session);
        return Results.Ok(new AuthSessionResponse(UserResponse.FromUser(user), session.AccessTokenExpiresAtUtc, session.RefreshTokenExpiresAtUtc));
    }

    private static async Task<IResult> DeleteCurrentSessionAsync(
        HttpContext httpContext,
        AuthSessionService authSessionService,
        AuthCookieService authCookieService,
        CancellationToken cancellationToken)
    {
        if (!httpContext.Request.Cookies.TryGetValue(authCookieService.RefreshCookieName, out var refreshToken) || string.IsNullOrWhiteSpace(refreshToken))
        {
            authCookieService.ClearAuthCookies(httpContext.Response);
            return Results.NoContent();
        }

        await authSessionService.RevokeCurrentSessionAsync(refreshToken, cancellationToken);
        authCookieService.ClearAuthCookies(httpContext.Response);
        return Results.NoContent();
    }

    private static async Task<IResult> RefreshSessionAsync(
        HttpContext httpContext,
        AuthSessionService authSessionService,
        AuthCookieService authCookieService,
        CancellationToken cancellationToken)
    {
        if (!httpContext.Request.Cookies.TryGetValue(authCookieService.RefreshCookieName, out var refreshToken) || string.IsNullOrWhiteSpace(refreshToken))
        {
            authCookieService.ClearAuthCookies(httpContext.Response);
            throw new InvalidRefreshTokenException();
        }

        try
        {
            var (user, session) = await authSessionService.RefreshSessionAsync(refreshToken, cancellationToken);
            WriteCookies(authCookieService, httpContext, session);
            return Results.Ok(new AuthSessionResponse(UserResponse.FromUser(user), session.AccessTokenExpiresAtUtc, session.RefreshTokenExpiresAtUtc));
        }
        catch
        {
            authCookieService.ClearAuthCookies(httpContext.Response);
            throw;
        }
    }

    private static async Task<IResult> GetCurrentUserAsync(HttpContext httpContext, ContryDbContext dbContext, CancellationToken cancellationToken)
    {
        if (!AccessTokenIdentityResolver.TryResolve(httpContext.User, out var identity) || identity is null)
        {
            throw new InvalidAccessTokenException();
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(entity => entity.Id == identity.UserId, cancellationToken);

        if (user is null)
        {
            throw new InvalidAccessTokenException();
        }

        return Results.Ok(UserResponse.FromUser(user));
    }

    private static async Task<IResult> GetXsrfTokenAsync(HttpContext httpContext, IXsrfTokenService xsrfTokenService, CurrentRefreshSessionService currentRefreshSessionService)
    {
        var session = await currentRefreshSessionService.GetSessionAsync(httpContext, allowRevoked: false, httpContext.RequestAborted);

        if (session is null)
        {
            throw new InvalidRefreshTokenException();
        }

        var xsrf = xsrfTokenService.CreateToken(new XsrfSessionBinding(session.UserId, session.SessionFamilyId, session.ExpiresAtUtc));
        return Results.Ok(new XsrfTokenResponse(xsrf.Token, xsrf.ExpiresAtUtc));
    }

    private static void WriteCookies(AuthCookieService authCookieService, HttpContext httpContext, AuthSessionResult session)
    {
        // The access cookie is kept as long as the refresh cookie so refresh and XSRF validation can still bind to the previous JWT identity.
        authCookieService.AppendAccessToken(httpContext.Response, session.AccessToken, session.RefreshTokenExpiresAtUtc);
        authCookieService.AppendRefreshToken(httpContext.Response, session.RefreshToken, session.RefreshTokenExpiresAtUtc);
    }
}
