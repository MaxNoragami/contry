namespace Contry.Application.Auth;

public interface IAuthSessionOptions
{
    int RefreshTokenLifetimeMinutes { get; }
}
