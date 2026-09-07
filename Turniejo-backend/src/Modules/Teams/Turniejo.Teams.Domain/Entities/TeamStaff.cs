using Turniejo.SharedKernel.Primitives;
using Turniejo.SharedKernel.Results;
using Turniejo.Teams.Domain.Enums;
using Turniejo.Teams.Domain.Events;

namespace Turniejo.Teams.Domain.Entities
{
    public sealed class TeamStaff : BaseEntity
    {
        public Guid TournamentTeamId { get; private set; }
        public Guid UserId { get; private set; }
        public TeamStaffRole Role { get; private set; }

        private readonly List<IDomainEvent> _domainEvents = [];
        public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

        private TeamStaff() { }
        private TeamStaff(Guid id) : base(id) { }

        private static Result<TeamStaff> CreateCore(Guid id, Guid tournamentTeamId, Guid userId, TeamStaffRole role)
        {
            if (id == Guid.Empty)
                return Result.Failure<TeamStaff>(Error.Validation("Id", "TeamStaff id cannot be empty"));

            if (tournamentTeamId == Guid.Empty)
                return Result.Failure<TeamStaff>(Error.Validation("TournamentTeamId", "TeamStaff tournamentTeamId cannot be empty"));

            if (userId == Guid.Empty)
                return Result.Failure<TeamStaff>(Error.Validation("UserId", "TeamStaff userId cannot be empty"));

            return Result.Success(new TeamStaff(id)
            {
                TournamentTeamId = tournamentTeamId,
                UserId = userId,
                Role = role
            });
        }

        public static Result<TeamStaff> Create(Guid id, Guid tournamentTeamId, Guid userId, TeamStaffRole role, Guid assignedByUserId)
        {
            var result = CreateCore(id, tournamentTeamId, userId, role);
            if (result.IsFailure)
                return result;

            var teamStaff = result.Value;
            teamStaff._domainEvents.Add(new UserAssignedToTeam(teamStaff.Id, tournamentTeamId, userId, role, assignedByUserId));

            return Result.Success(teamStaff);
        }

        public static Result<TeamStaff> CreateCoach(Guid id, Guid tournamentTeamId, Guid userId, Guid assignedByUserId)
        {
            var result = CreateCore(id, tournamentTeamId, userId, TeamStaffRole.Coach);
            if (result.IsFailure)
                return result;

            var teamStaff = result.Value;
            teamStaff._domainEvents.Add(new CoachAssignedToTeam(teamStaff.Id, tournamentTeamId, userId, assignedByUserId));

            return Result.Success(teamStaff);
        }

        public void ClearDomainEvents() => _domainEvents.Clear();
    }
}
