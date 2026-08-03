using FluentValidation;

namespace Turniejo.Organizations.Application.Organizations.CreateOrganization;

public sealed class CreateOrganizationCommandValidator
    : AbstractValidator<CreateOrganizationCommand>
{
    public CreateOrganizationCommandValidator()
    {
        RuleFor(x => x.OwnerUserId)
            .NotEmpty().WithMessage("OwnerUserId is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200);
    }
}