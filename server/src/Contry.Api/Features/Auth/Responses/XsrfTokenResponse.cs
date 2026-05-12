namespace Contry.Api.Features.Auth;

public sealed record XsrfTokenResponse(string Token, DateTimeOffset ExpiresAtUtc);
