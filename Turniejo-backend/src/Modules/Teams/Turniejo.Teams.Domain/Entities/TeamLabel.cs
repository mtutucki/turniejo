using Turniejo.SharedKernel.Primitives;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Teams.Domain.Entities
{
    /// <summary>
    /// Etykieta drużyn w bazie organizacji (np. „Rocznik 2015”, „Klub Orlik”). Drużyna może mieć kilka;
    /// przypisania trzyma drużyna (<see cref="Team.LabelIds"/>).
    /// </summary>
    public sealed class TeamLabel : BaseEntity
    {
        public const int NameMaxLength = 60;

        public Guid OrganizationId { get; private set; }
        public string Name { get; private set; } = string.Empty;

        private TeamLabel() { }
        private TeamLabel(Guid id) : base(id) { }

        public static Result<TeamLabel> Create(Guid id, Guid organizationId, string name)
        {
            if (id == Guid.Empty)
                return Result.Failure<TeamLabel>(Error.Validation("Id", "Label id cannot be empty"));

            if (organizationId == Guid.Empty)
                return Result.Failure<TeamLabel>(Error.Validation("OrganizationId", "Label organizationId cannot be empty"));

            if (string.IsNullOrWhiteSpace(name))
                return Result.Failure<TeamLabel>(Error.Validation("Name", "Label name cannot be empty"));

            return Result.Success(new TeamLabel(id) { OrganizationId = organizationId, Name = name.Trim() });
        }

        public Result Rename(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Result.Failure(Error.Validation("Name", "Label name cannot be empty"));

            Name = name.Trim();
            SetUpdatedAt();
            return Result.Success();
        }
    }
}
