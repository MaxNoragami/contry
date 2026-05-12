using Contry.Application.Errors;
using Contry.Domain.Users;

namespace Contry.Application.Auth;

public sealed class GetCurrentUserQueryHandler(IAuthStore authStore)
{
    private readonly IAuthStore _authStore = authStore;

    public async Task<User> HandleAsync(GetCurrentUserQuery query, CancellationToken cancellationToken)
    {
        var user = await _authStore.FindUserByIdAsync(query.UserId, cancellationToken);

        if (user is null)
        {
            throw new InvalidAccessTokenException();
        }

        return user;
    }
}
