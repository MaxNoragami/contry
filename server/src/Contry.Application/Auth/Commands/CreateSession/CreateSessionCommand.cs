namespace Contry.Application.Auth;

public sealed record CreateSessionCommand(string Credential, string Password);
