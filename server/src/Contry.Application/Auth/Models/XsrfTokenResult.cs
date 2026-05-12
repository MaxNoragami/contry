namespace Contry.Application.Auth;

public sealed record XsrfTokenResult(string Token, DateTimeOffset ExpiresAtUtc);
