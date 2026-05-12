namespace Contry.Application.Auth;

public sealed record XsrfSessionBinding(Guid UserId, Guid SessionFamilyId, DateTimeOffset ExpiresAtUtc);
