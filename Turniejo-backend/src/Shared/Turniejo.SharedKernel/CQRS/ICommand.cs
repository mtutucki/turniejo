using MediatR;
using Turniejo.SharedKernel.Results;

namespace Turniejo.SharedKernel.CQRS;

public interface ICommand : IRequest<Result> { }

public interface ICommand<TResponse> : IRequest<Result<TResponse>> { }