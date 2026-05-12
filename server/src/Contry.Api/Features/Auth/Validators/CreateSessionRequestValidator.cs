using FluentValidation;

namespace Contry.Api.Features.Auth;

public sealed class CreateSessionRequestValidator : AbstractValidator<CreateSessionRequest>
{
    public CreateSessionRequestValidator()
    {
        RuleFor(request => request.Credential).NotEmpty().MaximumLength(320);
        RuleFor(request => request.Password).NotEmpty().MaximumLength(128);
    }
}
