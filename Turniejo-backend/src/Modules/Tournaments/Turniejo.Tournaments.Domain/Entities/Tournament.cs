using Turniejo.SharedKernel.Helpers;
using Turniejo.SharedKernel.Primitives;
using Turniejo.SharedKernel.Results;
using Turniejo.Tournaments.Domain.Enums;
using Turniejo.Tournaments.Domain.Events;

namespace Turniejo.Tournaments.Domain.Entities;

public sealed class Tournament : BaseEntity
{
    public Guid OrganizationId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? Description { get; private set; } = string.Empty;
    public DateTimeOffset StartAt { get; private set; }
    public DateTimeOffset EndAt { get; private set; }
    public TournamentStatus Status { get; private set; } = TournamentStatus.Draft;
    public TournamentFormat? Format { get; private set; }
    public int TeamLimit { get; private set; }
    public bool IsPaid { get; private set; } = false;
    public string OrganizerCode { get; private set; } = string.Empty;
    public string RefereeCode { get; private set; } = string.Empty;
    public string CoachCode { get; private set; } = string.Empty;
    public string ViewerCode { get; private set; } = string.Empty;
    public TournamentSettings Settings { get; private set; } = null!;
    public TournamentPublicSettings PublicSettings { get; private set; } = null!;
    /// <summary>Miękkie usunięcie — turniej znika z list, szczegółów i portalu (globalny filtr EF).</summary>
    public DateTimeOffset? DeletedAt { get; private set; }

    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();


    private Tournament(Guid id) : base(id) { }
    private Tournament() { } // for EF Core

    public static Result<Tournament> Create(Guid id, Guid OrganizationId, Guid createdByUserId, string name, DateTimeOffset startAt, DateTimeOffset endAt, int teamLimit, string? description)
    {
        if (id == Guid.Empty)
            return Result.Failure<Tournament>(Error.Validation("Id", "Tournament Id cannot be empty"));
        if (OrganizationId == Guid.Empty)
            return Result.Failure<Tournament>(Error.Validation("OrganizationId", "Organization Id cannot be empty"));
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<Tournament>(Error.Validation("Name", "Tournament name cannot be empty"));
        if (endAt < startAt)
            return Result.Failure<Tournament>(Error.Validation("EndAt", "Tournament end date cannot be before start date"));
        if (teamLimit < 0)
            return Result.Failure<Tournament>(Error.Validation("TeamLimit", "Tournament team limit cannot be negative"));

        Tournament tournament = new Tournament(id)
        {
            OrganizationId = OrganizationId,
            CreatedByUserId = createdByUserId,
            Name = name,
            Slug = SlugGenerator.Generate(name),
            Description = description,
            StartAt = startAt,
            EndAt = endAt,
            TeamLimit = teamLimit,
            OrganizerCode = Guid.NewGuid().ToString("N")[..6].ToUpper(),
            RefereeCode = Guid.NewGuid().ToString("N")[..6].ToUpper(),
            CoachCode = Guid.NewGuid().ToString("N")[..6].ToUpper(),
            ViewerCode = Guid.NewGuid().ToString("N")[..6].ToUpper(),
            Settings = TournamentSettings.CreateDefault(id),
            PublicSettings = TournamentPublicSettings.CreateDefault(id)
        };

        tournament._domainEvents.Add(new TournamentCreated(
            tournament.Id,
            tournament.OrganizationId,
            tournament.CreatedByUserId,
            tournament.Name,
            tournament.StartAt,
            tournament.EndAt)
        );

        return Result.Success(tournament);
    }

    public Result ChangeStatus(TournamentStatus newStatus, Guid changedByUserId)
    {
        if (Status == newStatus)
            return Result.Failure(Error.Validation("Status", "Tournament status is already set to the specified value"));

        if (Status == TournamentStatus.Cancelled)
            return Result.Failure(Error.Validation("Status", "Tournament status cannot be changed after cancellation"));

        if (Status == TournamentStatus.Finished)
            return Result.Failure(Error.Validation("Status", "Tournament status cannot be changed after it has finished"));

        Dictionary<TournamentStatus, List<TournamentStatus>> allowedTransitions = new()
        {
            { TournamentStatus.Draft, new List<TournamentStatus> { TournamentStatus.RegistrationOpen, TournamentStatus.Cancelled } },
            // Przejścia wstecz (Draft <- RegistrationOpen <- RegistrationClosed): organizator może ponownie
            // otworzyć zapisy, np. żeby dopisać drużynę. Dalej cofa tylko RevertScheduleGeneration.
            { TournamentStatus.RegistrationOpen, new List<TournamentStatus> { TournamentStatus.RegistrationClosed, TournamentStatus.Draft, TournamentStatus.Cancelled } },
            { TournamentStatus.RegistrationClosed, new List<TournamentStatus> { TournamentStatus.ScheduleGenerated, TournamentStatus.RegistrationOpen, TournamentStatus.Cancelled } },
            { TournamentStatus.ScheduleGenerated, new List<TournamentStatus> { TournamentStatus.InProgress, TournamentStatus.Cancelled } },
            { TournamentStatus.InProgress, new List<TournamentStatus> { TournamentStatus.Finished, TournamentStatus.Cancelled } },
        };

        if (!allowedTransitions.TryGetValue(Status, out var allowedStatuses) || !allowedStatuses.Contains(newStatus))
        {
            return Result.Failure(Error.Validation("Status", $"Tournament status cannot be changed from {Status} to {newStatus}"));
        }

        if (newStatus == TournamentStatus.ScheduleGenerated && Format == null)
            return Result.Failure(Error.Validation("Format", "Cannot generate schedule before setting tournament format."));

        _domainEvents.Add(new TournamentStatusChanged(Id, OrganizationId, changedByUserId, Status, newStatus));
        Status = newStatus;
        SetUpdatedAt();

        return Result.Success();
    }

    // Przejście wstecz poza ChangeStatus: po wyczyszczeniu terminarza (żaden mecz się nie rozpoczął)
    // turniej wraca do zamkniętych zapisów, żeby organizator mógł poprawić drużyny/grupy i wygenerować ponownie.
    public Result RevertScheduleGeneration(Guid changedByUserId)
    {
        if (Status != TournamentStatus.ScheduleGenerated)
            return Result.Failure(Error.Validation("Status", "Schedule can only be cleared before the tournament starts"));

        _domainEvents.Add(new TournamentStatusChanged(Id, OrganizationId, changedByUserId, Status, TournamentStatus.RegistrationClosed));
        Status = TournamentStatus.RegistrationClosed;
        SetUpdatedAt();

        return Result.Success();
    }

    public Result UpdateDetails(string name, DateTimeOffset startAt, DateTimeOffset endAt, int teamLimit, string? description)
    {
        if (Status == TournamentStatus.Cancelled)
            return Result.Failure(Error.Validation("Status", "Tournament status cannot be changed after cancellation"));

        if (Status == TournamentStatus.Finished)
            return Result.Failure(Error.Validation("Status", "Tournament status cannot be changed after it has finished"));

        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure(Error.Validation("Name", "Tournament name cannot be empty"));
        if (endAt < startAt)
            return Result.Failure(Error.Validation("EndAt", "Tournament end date cannot be before start date"));
        if (teamLimit < 0)
            return Result.Failure(Error.Validation("TeamLimit", "Tournament team limit cannot be negative"));
        Name = name;
        StartAt = startAt;
        EndAt = endAt;
        TeamLimit = teamLimit;
        Description = description;
        _domainEvents.Add(new TournamentUpdated(Id, OrganizationId, name, startAt, endAt, teamLimit, description));
        SetUpdatedAt();
        return Result.Success();
    }

    /// <summary>
    /// Usunięcie z list organizatora. Tylko anulowany turniej: trwający czy zakończony ma wyniki, tabele
    /// i historię zawodników. Dane w innych modułach zostają (miękkie usunięcie), ale bez turnieju nigdzie
    /// ich nie widać.
    /// </summary>
    public Result Delete()
    {
        if (Status != TournamentStatus.Cancelled)
            return Result.Failure(Error.Validation("Status", "Only a cancelled tournament can be deleted."));
        if (DeletedAt != null)
            return Result.Success();

        DeletedAt = DateTimeOffset.UtcNow;
        SetUpdatedAt();
        return Result.Success();
    }

    public Result SetFormat(TournamentFormat format)
    {
        if (Status >= TournamentStatus.ScheduleGenerated)
            return Result.Failure(Error.Validation("Format", "Cannot change format after schedule has been generated."));
        Format = format;
        _domainEvents.Add(new TournamentFormatChanged(Id, OrganizationId, format));
        SetUpdatedAt();
        return Result.Success();
    }

    public Result UpdateSettings(int pointsForWin, int pointsForDraw, int pointsForLoss, int matchDurationMinutes, int breakDurationMinutes,
        bool isThirdPlaceMatchEnabled, int refereeEditWindowMinutes, int walkoverHomeScore, int walkoverAwayScore, bool canCoachEditSquadAfterStart, Guid changedByUserId)
    {
        if (Status >= TournamentStatus.InProgress)
            return Result.Failure(Error.Conflict("Status", "You cannot edit tournament settings after tournament is in progres"));

        var result = Settings.Update(pointsForWin, pointsForDraw, pointsForLoss, matchDurationMinutes, breakDurationMinutes,
            isThirdPlaceMatchEnabled, refereeEditWindowMinutes, walkoverHomeScore, walkoverAwayScore, canCoachEditSquadAfterStart);

        if (result.IsFailure)
            return Result.Failure(result.Error);

        _domainEvents.Add(new TournamentSettingsUpdated(Id, OrganizationId, changedByUserId));

        return Result.Success();
    }

    public Result UpdatePublicSettings(
        bool isPublic, bool showTeams, bool showPlayers, bool showSchedule,
        bool showStandings, bool showBracket, bool showLiveMatches, bool showStats,
        bool abbreviatePlayerNames = true)
    {
        var result = PublicSettings.Update(isPublic, showTeams, showPlayers, showSchedule, showStandings, showBracket, showLiveMatches, showStats, abbreviatePlayerNames);
        if (result.IsFailure)
            return result;

        SetUpdatedAt();

        return Result.Success();
    }

    public void ClearDomainEvents() => _domainEvents.Clear();

}