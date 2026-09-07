using Turniejo.Players.Application.DTOs;
using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Players.Application.Players.GetPlayerById
{
    public sealed record GetPlayerByIdQuery(Guid PlayerId) : IQuery<PlayerDto>;
}
