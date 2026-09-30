using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Users.Application.Users.ExportMyData;

// Prawo dostępu/przenoszenia danych (RODO art. 15/20) — tylko dane zalogowanego usera.
public sealed record ExportMyDataQuery : IQuery<MyDataExportDto>, IAuthenticatedOnlyRequest;

public sealed record MyDataExportDto(
    DateTimeOffset ExportedAt,
    MyProfileExportDto Profile,
    IReadOnlyList<MyRoleExportDto> Roles,
    IReadOnlyList<MyChildExportDto> Children);

public sealed record MyProfileExportDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    DateTimeOffset CreatedAt,
    DateTimeOffset? TermsAcceptedAt,
    string? TermsVersion);

public sealed record MyRoleExportDto(string Role, string ScopeType, Guid? ScopeId, DateTimeOffset AssignedAt);

public sealed record MyChildExportDto(Guid PlayerId, string FirstName, string LastName);
