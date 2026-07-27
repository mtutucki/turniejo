namespace Turniejo.SharedKernel.Exceptions;

public class NotFoundException(string entity, Guid id)
    : Exception($"{entity} with id '{id}' was not found.");