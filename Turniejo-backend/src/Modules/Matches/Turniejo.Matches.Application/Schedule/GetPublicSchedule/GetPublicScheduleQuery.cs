using Turniejo.Matches.Application.DTOs;
using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Matches.Application.Schedule.GetPublicSchedule
{
    public sealed record GetPublicScheduleQuery(string Slug) : IQuery<IReadOnlyList<MatchDto>>, IPublicRequest;
}
