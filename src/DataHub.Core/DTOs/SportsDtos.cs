namespace DataHub.Core.DTOs.Sports;

// ---------------- Sport ----------------
public record SportDto(
    Guid Id,
    string Name,
    string Slug,
    string? IconRef,
    int SortOrder,
    bool IsActive
);

public record CreateSportRequest(
    string Name,
    string Slug,
    string? IconRef,
    int SortOrder
);

public record UpdateSportRequest(
    string Name,
    string Slug,
    string? IconRef,
    int SortOrder,
    bool IsActive
);

// ---------------- SportLevel ----------------
public record SportLevelDto(
    Guid Id,
    Guid SportId,
    string Name,
    int SortOrder,
    bool IsActive
);

public record CreateSportLevelRequest(
    string Name,
    int SortOrder
);

public record UpdateSportLevelRequest(
    string Name,
    int SortOrder,
    bool IsActive
);

// ---------------- League ----------------
public record LeagueDto(
    Guid Id,
    Guid SportLevelId,
    string Name,
    string? Abbreviation,
    string? Country,
    int? FoundedYear,
    bool IsActive
);

public record CreateLeagueRequest(
    string Name,
    string? Abbreviation,
    string? Country,
    int? FoundedYear
);

public record UpdateLeagueRequest(
    string Name,
    string? Abbreviation,
    string? Country,
    int? FoundedYear,
    bool IsActive
);

// ---------------- Conference ----------------
public record ConferenceDto(
    Guid Id,
    Guid LeagueId,
    Guid? ParentConferenceId,
    string Name,
    bool IsActive
);

public record CreateConferenceRequest(
    string Name,
    Guid? ParentConferenceId
);

public record UpdateConferenceRequest(
    string Name,
    Guid? ParentConferenceId,
    bool IsActive
);

// ---------------- Venue ----------------
public record VenueDto(
    Guid Id,
    string Name,
    string? Address,
    string? City,
    string? State,
    string? Country,
    double? Lat,
    double? Lon,
    int? Capacity,
    int? OpenedYear,
    int? ClosedYear,
    bool IsActive
);

public record CreateVenueRequest(
    string Name,
    string? Address,
    string? City,
    string? State,
    string? Country,
    double? Lat,
    double? Lon,
    int? Capacity,
    int? OpenedYear,
    int? ClosedYear
);

public record UpdateVenueRequest(
    string Name,
    string? Address,
    string? City,
    string? State,
    string? Country,
    double? Lat,
    double? Lon,
    int? Capacity,
    int? OpenedYear,
    int? ClosedYear,
    bool IsActive
);

// ---------------- Team ----------------
public record TeamDto(
    Guid Id,
    Guid LeagueId,
    Guid? ConferenceId,
    Guid? VenueId,
    string Name,
    string? City,
    string? State,
    string? Country,
    int? FoundedYear,
    string? PrimaryColor,
    string? SecondaryColor,
    string? LogoRef,
    bool IsActive
);

public record CreateTeamRequest(
    string Name,
    Guid? ConferenceId,
    Guid? VenueId,
    string? City,
    string? State,
    string? Country,
    int? FoundedYear,
    string? PrimaryColor,
    string? SecondaryColor,
    string? LogoRef
);

public record UpdateTeamRequest(
    string Name,
    Guid? ConferenceId,
    Guid? VenueId,
    string? City,
    string? State,
    string? Country,
    int? FoundedYear,
    string? PrimaryColor,
    string? SecondaryColor,
    string? LogoRef,
    bool IsActive
);
