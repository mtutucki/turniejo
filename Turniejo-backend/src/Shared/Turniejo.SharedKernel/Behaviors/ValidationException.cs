namespace Turniejo.SharedKernel.Behaviors;

public record ValidationError(string Field, string Message);

public sealed class TurniejoValidationException(IEnumerable<ValidationError> errors)
    : Exception("One or more validation errors occurred.")
{
    public IReadOnlyList<ValidationError> Errors { get; } = errors.ToList();
}