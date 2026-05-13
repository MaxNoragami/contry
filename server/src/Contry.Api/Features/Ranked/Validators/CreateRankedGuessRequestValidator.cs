using FluentValidation;

namespace Contry.Api.Features.Ranked;

public sealed class CreateRankedGuessRequestValidator : AbstractValidator<CreateRankedGuessRequest>
{
    public CreateRankedGuessRequestValidator()
    {
        RuleFor(request => request.CountryId)
            .NotEmpty()
            .MaximumLength(16);
    }
}
