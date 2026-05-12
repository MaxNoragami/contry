using Contry.Domain.Users;

namespace Contry.Application.Auth;

public sealed class CreateSessionCommandHandler(
    IAuthStore authStore,
    IPasswordHasher passwordHasher,
    AuthSessionIssuer authSessionIssuer)
{
    private readonly IAuthStore _authStore = authStore;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly AuthSessionIssuer _authSessionIssuer = authSessionIssuer;

    public async Task<(User User, AuthSessionResult Session)> HandleAsync(CreateSessionCommand command, CancellationToken cancellationToken)
    {
        var normalizedCredential = Normalize(command.Credential);
        var user = await _authStore.FindUserByCredentialAsync(normalizedCredential, cancellationToken);

        if (user is null || !_passwordHasher.VerifyPassword(command.Password, user.PasswordHash))
        {
            throw new InvalidCredentialsException();
        }

        var session = await _authSessionIssuer.IssueSessionAsync(user, Guid.NewGuid(), cancellationToken);
        return (user, session);
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
