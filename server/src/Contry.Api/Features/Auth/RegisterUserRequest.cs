namespace Contry.Api.Features.Auth;

public sealed record RegisterUserRequest(string Username, string Email, string Password);
