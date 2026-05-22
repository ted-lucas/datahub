using DataHub.Core.DTOs.Sports;

namespace DataHub.Core.Interfaces;

public interface ISportsService
{
    // Sports
    Task<IEnumerable<SportDto>> GetSportsAsync(bool includeInactive, CancellationToken ct = default);
    Task<SportDto?> GetSportAsync(Guid id, CancellationToken ct = default);
    Task<SportDto> CreateSportAsync(CreateSportRequest req, CancellationToken ct = default);
    Task<SportDto?> UpdateSportAsync(Guid id, UpdateSportRequest req, CancellationToken ct = default);
    Task<bool> DeleteSportAsync(Guid id, CancellationToken ct = default);

    // SportLevels (scoped to a Sport)
    Task<IEnumerable<SportLevelDto>> GetLevelsAsync(Guid sportId, bool includeInactive, CancellationToken ct = default);
    Task<SportLevelDto?> GetLevelAsync(Guid id, CancellationToken ct = default);
    Task<SportLevelDto?> CreateLevelAsync(Guid sportId, CreateSportLevelRequest req, CancellationToken ct = default);
    Task<SportLevelDto?> UpdateLevelAsync(Guid id, UpdateSportLevelRequest req, CancellationToken ct = default);
    Task<bool> DeleteLevelAsync(Guid id, CancellationToken ct = default);

    // Leagues (scoped to a SportLevel)
    Task<IEnumerable<LeagueDto>> GetLeaguesAsync(Guid sportLevelId, bool includeInactive, CancellationToken ct = default);
    Task<LeagueDto?> GetLeagueAsync(Guid id, CancellationToken ct = default);
    Task<LeagueDto?> CreateLeagueAsync(Guid sportLevelId, CreateLeagueRequest req, CancellationToken ct = default);
    Task<LeagueDto?> UpdateLeagueAsync(Guid id, UpdateLeagueRequest req, CancellationToken ct = default);
    Task<bool> DeleteLeagueAsync(Guid id, CancellationToken ct = default);

    // Conferences (scoped to a League)
    Task<IEnumerable<ConferenceDto>> GetConferencesAsync(Guid leagueId, bool includeInactive, CancellationToken ct = default);
    Task<ConferenceDto?> GetConferenceAsync(Guid id, CancellationToken ct = default);
    Task<ConferenceDto?> CreateConferenceAsync(Guid leagueId, CreateConferenceRequest req, CancellationToken ct = default);
    Task<ConferenceDto?> UpdateConferenceAsync(Guid id, UpdateConferenceRequest req, CancellationToken ct = default);
    Task<bool> DeleteConferenceAsync(Guid id, CancellationToken ct = default);

    // Venues (flat)
    Task<IEnumerable<VenueDto>> GetVenuesAsync(bool includeInactive, CancellationToken ct = default);
    Task<VenueDto?> GetVenueAsync(Guid id, CancellationToken ct = default);
    Task<VenueDto> CreateVenueAsync(CreateVenueRequest req, CancellationToken ct = default);
    Task<VenueDto?> UpdateVenueAsync(Guid id, UpdateVenueRequest req, CancellationToken ct = default);
    Task<bool> DeleteVenueAsync(Guid id, CancellationToken ct = default);

    // Teams (scoped to a League)
    Task<IEnumerable<TeamDto>> GetTeamsAsync(Guid? leagueId, string? state, bool includeInactive, CancellationToken ct = default);
    Task<TeamDto?> GetTeamAsync(Guid id, CancellationToken ct = default);
    Task<TeamDto?> CreateTeamAsync(Guid leagueId, CreateTeamRequest req, CancellationToken ct = default);
    Task<TeamDto?> UpdateTeamAsync(Guid id, UpdateTeamRequest req, CancellationToken ct = default);
    Task<bool> DeleteTeamAsync(Guid id, CancellationToken ct = default);
}
