using Contry.Domain.Users;

namespace Contry.Api.Features.Auth;

public sealed record UserResponse(Guid Id, string Username, string Email, string Role)
{
    public static UserResponse FromUser(User user) => new(user.Id, user.Username, user.Email, user.Role.ToString().ToUpperInvariant());
}
