namespace Turniejo.SharedKernel.Exceptions;

public class UnauthorizedException(string message = "You do not have permission to perform this action.")
    : Exception(message);