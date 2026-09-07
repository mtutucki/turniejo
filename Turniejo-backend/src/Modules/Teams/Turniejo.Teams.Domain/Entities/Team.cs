using Turniejo.SharedKernel.Primitives;
using Turniejo.SharedKernel.Results;
using Turniejo.Teams.Domain.Events;

namespace Turniejo.Teams.Domain.Entities
{
    public sealed class Team : BaseEntity
    {
        public Guid OrganizationId { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public string? ShortName { get; private set; }
        public string? LogoUrl { get; private set; }

        private readonly List<IDomainEvent> _domainEvents = [];
        public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

        private Team() { }
        private Team(Guid id) : base(id) { }

        public static Result<Team> Create(Guid id, Guid organizationId, Guid createdByUserId, string name, string? shortName, string? logoUrl)
        {
            if (id == Guid.Empty)
                return Result.Failure<Team>(Error.Validation("Id", "Team id cannot be empty"));

            if (organizationId == Guid.Empty)
                return Result.Failure<Team>(Error.Validation("OrganizationId", "Team organizationId cannot be empty"));

            if (createdByUserId == Guid.Empty)
                return Result.Failure<Team>(Error.Validation("CreatedByUserId", "Team createdByUserId cannot be empty"));

            if (string.IsNullOrWhiteSpace(name))
                return Result.Failure<Team>(Error.Validation("Name", "Team name cannot be empty"));

            var team = new Team(id)
            {
                OrganizationId = organizationId,
                Name = name,
                ShortName = shortName,
                LogoUrl = logoUrl
            };

            team._domainEvents.Add(new TeamCreated(team.Id, organizationId, createdByUserId, name));

            return Result.Success(team);
        }

        public Result UpdateDetails(string name, string? shortName)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Result.Failure(Error.Validation("Name", "Team name cannot be empty"));

            Name = name;
            ShortName = shortName;
            SetUpdatedAt();

            return Result.Success();
        }

        public Result UpdateLogo(string? logoUrl)
        {
            LogoUrl = logoUrl;
            SetUpdatedAt();

            return Result.Success();
        }

        public void ClearDomainEvents() => _domainEvents.Clear();
    }
}
