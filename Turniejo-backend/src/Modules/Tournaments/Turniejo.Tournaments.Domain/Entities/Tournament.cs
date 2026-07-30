using Turniejo.SharedKernel.Helpers;
using Turniejo.SharedKernel.Primitives;
using Turniejo.SharedKernel.Results;
using Turniejo.Tournaments.Domain.Enums;

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

        return Result.Success(tournament);
    }

}