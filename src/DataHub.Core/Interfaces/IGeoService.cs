using DataHub.Core.DTOs.Geo;

namespace DataHub.Core.Interfaces;

/// <summary>
/// Read-mostly reference data for the map module. Boundaries (polygons) are
/// served as static GeoJSON files under <c>/geo/*</c> and joined to these
/// rows on the frontend by FIPS / ISO-2.
/// </summary>
public interface IGeoService
{
    // Countries
    Task<IReadOnlyList<CountryDto>> ListCountriesAsync(bool includeInactive = false, CancellationToken ct = default);
    Task<CountryDto?> GetCountryAsync(Guid id, CancellationToken ct = default);
    Task<CountryDto?> GetCountryByIso2Async(string iso2, CancellationToken ct = default);
    Task<CountryDto> CreateCountryAsync(CreateCountryRequest req, CancellationToken ct = default);
    Task<CountryDto?> UpdateCountryAsync(Guid id, UpdateCountryRequest req, CancellationToken ct = default);
    Task<bool> DeactivateCountryAsync(Guid id, CancellationToken ct = default);

    // States
    Task<IReadOnlyList<StateDto>> ListStatesAsync(Guid countryId, bool includeInactive = false, CancellationToken ct = default);
    Task<IReadOnlyList<StateDto>> ListStatesByCountryIso2Async(string iso2, bool includeInactive = false, CancellationToken ct = default);
    Task<StateDto?> GetStateAsync(Guid id, CancellationToken ct = default);
    Task<StateDto> CreateStateAsync(Guid countryId, CreateStateRequest req, CancellationToken ct = default);
    Task<StateDto?> UpdateStateAsync(Guid id, UpdateStateRequest req, CancellationToken ct = default);
    Task<bool> DeactivateStateAsync(Guid id, CancellationToken ct = default);

    // Counties
    Task<IReadOnlyList<CountyDto>> ListCountiesAsync(Guid stateId, bool includeInactive = false, CancellationToken ct = default);
    Task<IReadOnlyList<CountyDto>> ListCountiesByStateFipsAsync(string stateFips, bool includeInactive = false, CancellationToken ct = default);
    Task<CountyDto?> GetCountyAsync(Guid id, CancellationToken ct = default);
    Task<CountyDto> CreateCountyAsync(Guid stateId, CreateCountyRequest req, CancellationToken ct = default);
    Task<CountyDto?> UpdateCountyAsync(Guid id, UpdateCountyRequest req, CancellationToken ct = default);
    Task<bool> DeactivateCountyAsync(Guid id, CancellationToken ct = default);

    // Metrics — choropleth data source. One row per region keyed by FIPS/ISO.
    //
    // `kind` selects what's being counted:
    //   - Regions: row counts of geographic children (states-per-country, counties-per-state).
    //              The original placeholder; useful even with no domain data loaded.
    //   - Teams:   Sports.Team rows joined by `Team.Country` (ISO-2/USA) and `Team.State` (postal -> FIPS).
    //   - Venues:  Sports.Venue rows joined the same way.
    //
    // Teams/Venues are currently state-grained only — neither entity stores a county
    // assignment. Calling them at County level falls back to Regions to keep the
    // map painted instead of returning an empty FeatureCollection.
    Task<IReadOnlyList<GeoMetricDto>> GetMetricsAsync(GeoMetricsLevel level, string? parentFips, GeoMetricKind kind = GeoMetricKind.Regions, CancellationToken ct = default);
}

public enum GeoMetricsLevel
{
    Country,
    State,
    County,
}

public enum GeoMetricKind
{
    Regions,
    Teams,
    Venues,
}
