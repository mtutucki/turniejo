using Turniejo.SharedKernel.Primitives;
using Turniejo.SharedKernel.Results;
using Turniejo.Teams.Domain.Enums;
using Turniejo.Teams.Domain.Events;

namespace Turniejo.Teams.Domain.Entities
{
    public sealed class TournamentTeam : BaseEntity
    {
        private static readonly Dictionary<RegistrationStatus, List<RegistrationStatus>> AllowedTransitions = new()
        {
            { RegistrationStatus.Pending, [RegistrationStatus.Accepted, RegistrationStatus.Rejected] },
            { RegistrationStatus.Accepted, [RegistrationStatus.Withdrawn] },
        };

        public Guid TournamentId { get; private set; }
        public Guid TeamId { get; private set; }
        public Guid CategoryId { get; private set; }
        public Guid? GroupId { get; private set; }
        public RegistrationStatus RegistrationStatus { get; private set; } = RegistrationStatus.Pending;
        public PaymentStatus PaymentStatus { get; private set; } = PaymentStatus.NotRequired;
        public int? SeedNumber { get; private set; }

        private readonly List<IDomainEvent> _domainEvents = [];
        public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

        private TournamentTeam() { }
        private TournamentTeam(Guid id) : base(id) { }

        public static Result<TournamentTeam> Create(Guid id, Guid tournamentId, Guid teamId, Guid categoryId, Guid addedByUserId)
        {
            if (id == Guid.Empty)
                return Result.Failure<TournamentTeam>(Error.Validation("Id", "TournamentTeam id cannot be empty"));

            if (tournamentId == Guid.Empty)
                return Result.Failure<TournamentTeam>(Error.Validation("TournamentId", "TournamentTeam tournamentId cannot be empty"));

            if (teamId == Guid.Empty)
                return Result.Failure<TournamentTeam>(Error.Validation("TeamId", "TournamentTeam teamId cannot be empty"));

            if (categoryId == Guid.Empty)
                return Result.Failure<TournamentTeam>(Error.Validation("CategoryId", "TournamentTeam categoryId cannot be empty"));

            var tournamentTeam = new TournamentTeam(id)
            {
                TournamentId = tournamentId,
                TeamId = teamId,
                CategoryId = categoryId
            };

            tournamentTeam._domainEvents.Add(new TeamAddedToTournament(
                tournamentTeam.Id, tournamentId, teamId, categoryId, addedByUserId));

            return Result.Success(tournamentTeam);
        }

        public Result ChangeRegistrationStatus(RegistrationStatus newStatus, Guid changedByUserId)
        {
            if (RegistrationStatus == newStatus)
                return Result.Failure(Error.Validation("RegistrationStatus", "TournamentTeam registration status is already set to the specified value"));

            if (!AllowedTransitions.TryGetValue(RegistrationStatus, out var allowedStatuses) || !allowedStatuses.Contains(newStatus))
                return Result.Failure(Error.Validation("RegistrationStatus", $"Cannot change registration status from {RegistrationStatus} to {newStatus}"));

            var oldStatus = RegistrationStatus;
            RegistrationStatus = newStatus;
            SetUpdatedAt();

            _domainEvents.Add(new TeamRegistrationStatusChanged(Id, TournamentId, oldStatus, newStatus, changedByUserId));

            return Result.Success();
        }

        public Result ChangePaymentStatus(PaymentStatus newStatus, Guid changedByUserId)
        {
            if (PaymentStatus == newStatus)
                return Result.Failure(Error.Validation("PaymentStatus", "TournamentTeam payment status is already set to the specified value"));

            var oldStatus = PaymentStatus;
            PaymentStatus = newStatus;
            SetUpdatedAt();

            _domainEvents.Add(new TeamPaymentStatusChanged(Id, TournamentId, oldStatus, newStatus, changedByUserId));

            return Result.Success();
        }

        public Result AssignToGroup(Guid? groupId)
        {
            GroupId = groupId;
            SetUpdatedAt();

            return Result.Success();
        }

        public void ClearDomainEvents() => _domainEvents.Clear();
    }
}
