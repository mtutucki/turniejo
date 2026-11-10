using FluentValidation;

namespace Turniejo.Users.Application.Users.SetMyTheme;

public sealed class SetMyThemeCommandValidator : AbstractValidator<SetMyThemeCommand>
{
    public SetMyThemeCommandValidator()
    {
        RuleFor(x => x.Theme)
            .Must(theme => ThemeNames.TryParse(theme, out _))
            .WithMessage("Theme must be system, light or dark.");
    }
}
