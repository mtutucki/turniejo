using MediatR;
using Turniejo.SharedKernel.Results;

namespace Turniejo.SharedKernel.CQRS;

public interface IQueryHandler<TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>
{ }