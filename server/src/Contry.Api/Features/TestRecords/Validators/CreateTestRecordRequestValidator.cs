using FluentValidation;

namespace Contry.Api.Features.TestRecords;

public sealed class CreateTestRecordRequestValidator : AbstractValidator<CreateTestRecordRequest>
{
    public CreateTestRecordRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(request => request.Notes)
            .NotEmpty()
            .MaximumLength(2048);
    }
}
