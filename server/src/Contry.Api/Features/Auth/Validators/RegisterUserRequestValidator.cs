using FluentValidation;

namespace Contry.Api.Features.Auth;

public sealed class RegisterUserRequestValidator : AbstractValidator<RegisterUserRequest>
{
    public RegisterUserRequestValidator()
    {
        RuleFor(request => request.Username).NotEmpty().MinimumLength(3).MaximumLength(64);
        RuleFor(request => request.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(request => request.Password).NotEmpty().MinimumLength(8).MaximumLength(128);
    }
}
