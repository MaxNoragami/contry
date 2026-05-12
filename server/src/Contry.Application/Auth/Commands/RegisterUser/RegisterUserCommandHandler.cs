using Contry.Domain.Users;

namespace Contry.Application.Auth;

public sealed class RegisterUserCommandHandler(
    IAuthStore authStore,
    IPasswordHasher passwordHasher,
    AuthSessionIssuer authSessionIssuer,
    TimeProvider timeProvider)
{
    private readonly IAuthStore _authStore = authStore;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly AuthSessionIssuer _authSessionIssuer = authSessionIssuer;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<(User User, AuthSessionResult Session)> HandleAsync(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        var normalizedUsername = Normalize(command.Username);
        var normalizedEmail = Normalize(command.Email);
        var conflicts = await _authStore.CheckRegistrationConflictsAsync(normalizedUsername, normalizedEmail, cancellationToken);

        if (conflicts.UsernameExists)
        {
            throw new AuthConflictException("username", "A user with that username already exists.");
        }

        if (conflicts.EmailExists)
        {
            throw new AuthConflictException("email", "A user with that email already exists.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = command.Username.Trim(),
            NormalizedUsername = normalizedUsername,
            Email = command.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            PasswordHash = _passwordHasher.HashPassword(command.Password),
            Role = UserRole.User,
            CreatedAtUtc = _timeProvider.GetUtcNow()
        };

        await _authStore.AddUserAsync(user, cancellationToken);
        var session = await _authSessionIssuer.IssueSessionAsync(user, Guid.NewGuid(), cancellationToken);
        return (user, session);
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
