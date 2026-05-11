namespace Contry.Application.Errors;

public sealed class InvalidAccessTokenException() : UnauthorizedException(
    "/problems/auth/invalid-access-token",
    "Invalid access token.",
    "The request requires a valid authenticated access token with a valid subject, role, token identifier, issuer, audience, and signature.");
