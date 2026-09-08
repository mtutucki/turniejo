using Turniejo.Brackets.Application.DTOs;
using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Brackets.Application.Brackets.GetPublicBracket
{
    public sealed record GetPublicBracketQuery(string Slug, Guid CategoryId) : IQuery<BracketDto>, IPublicRequest;
}
