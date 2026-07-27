using MediatR;
using Turniejo.SharedKernel.Results;

namespace Turniejo.SharedKernel.CQRS;

public interface IQuery<TResponse> : IRequest<Result<TResponse>> { }