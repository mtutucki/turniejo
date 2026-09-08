namespace Turniejo.SharedKernel.Results;

public record Error(string Code, string Message, ErrorType Type = ErrorType.Failure)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public static Error NotFound(string entity, Guid id) =>
        new($"{entity}.NotFound", $"{entity} with id '{id}' was not found.", ErrorType.NotFound);

    public static Error NotFound(string entity, string identifier) =>
        new($"{entity}.NotFound", $"{entity} with identifier '{identifier}' was not found.", ErrorType.NotFound);

    public static Error Validation(string field, string message) =>
        new($"Validation.{field}", message, ErrorType.Validation);

    public static Error Conflict(string field, string message) =>
        new($"Conflict.{field}", message, ErrorType.Conflict);
}