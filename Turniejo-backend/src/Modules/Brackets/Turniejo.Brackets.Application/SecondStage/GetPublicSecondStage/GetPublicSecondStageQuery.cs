using Turniejo.Brackets.Application.DTOs;
using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Brackets.Application.SecondStage.GetPublicSecondStage
{
    public sealed record GetPublicSecondStageQuery(string Slug) : IQuery<SecondStageDto>, IPublicRequest;
}
