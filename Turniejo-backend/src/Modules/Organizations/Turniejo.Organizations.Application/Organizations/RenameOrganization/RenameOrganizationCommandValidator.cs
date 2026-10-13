using FluentValidation;

namespace Turniejo.Organizations.Application.Organizations.RenameOrganization;

public sealed class RenameOrganizationCommandValidator : AbstractValidator<RenameOrganizationCommand>
{
    public RenameOrganizationCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200);
    }
}
