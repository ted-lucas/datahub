using DataHub.Core.DTOs.Geo;

namespace DataHub.Core.Interfaces;

public interface IGeoService
{
    // Countries
    Task<IReadOnlyList<CountryDto>> ListCountriesAsync(bool includeInactive = false, CancellationToken ct = default);
    Task<CountryDto?> GetCountryAsync(Guid id, CancellationToken ct = default);
    Task<CountryDto?> GetCountryByIso2Async(string iso2, CancellationToken ct = default);
    Task<CountryDto> CreateCountryAsync(CreateCountryRequest req, CancellationToken ct = default);
    Task<CountryDto?> UpdateCountryAsync(Guid id, UpdateCountryRequest req, CancellationToken ct = default);
    Task<bool> DeactivateCountryAsync(Guid id, CancellationToken ct = default);
    Task<bool> SetCountryGeometryAsync(Guid id, string geoJson, CancellationToken ct = default);

    // States
    Task<IReadOnlyList<StateDto>> ListStatesAsync(Guid countryId, bool includeInactive = false, CancellationToken ct = default);
    Task<StateDto?> GetStateAsync(Guid id, CancellationToken ct = default);
    Task<StateDto> CreateStateAsync(Guid countryId, CreateStateRequest req, CancellationToken ct = default);
    Task<StateDto?> UpdateStateAsync(Guid id, UpdateStateRequest req, CancellationToken ct = default);
    Task<bool> DeactivateStateAsync(Guid id, CancellationToken ct = default);
    Task<bool> SetStateGeometryAsync(Guid id, string geoJson, CancellationToken ct = default);

    // Counties
    Task<IReadOnlyList<CountyDto>> ListCountiesAsync(Guid stateId, bool includeInactive = false, CancellationToken ct = default);
    Task<CountyDto?> GetCountyAsync(Guid id, CancellationToken ct = default);
    Task<CountyDto> CreateCountyAsync(Guid stateId, CreateCountyRequest req, CancellationToken ct = default);
    Task<CountyDto?> UpdateCountyAsync(Guid id, UpdateCountyRequest req, CancellationToken ct = default);
    Task<bool> DeactivateCountyAsync(Guid id, CancellationToken ct = default);
    Task<bool> SetCountyGeometryAsync(Guid id, string geoJson, CancellationToken ct = default);
}

/// <summary>
/// Writes per-entity GeoJSON cache files plus rolled-up bundle files
/// under <c>wwwroot/geo-cache/</c>. Frontend reads these via the static
/// file middleware instead of pulling geometry through the API.
/// </summary>
public interface IGeoCacheWriter
{
    /// <summary>Root cache directory, e.g. <c>{contentRoot}/wwwroot/geo-cache</c>.</summary>
    string CacheRoot { get; }

    Task WriteCountryAsync(Guid id, string geoJson, CancellationToken ct = default);
    Task WriteStateAsync(Guid id, string geoJson, CancellationToken ct = default);
    Task WriteCountyAsync(Guid id, string geoJson, CancellationToken ct = default);

    /// <summary>Rewrites <c>states/bundle-{countryId}.geojson</c> from all active states of that country.</summary>
    Task RebuildStatesBundleAsync(Guid countryId, IEnumerable<(Guid Id, string Code, string Name, string FeatureGeoJson)> features, CancellationToken ct = default);

    /// <summary>Rewrites <c>counties/bundle-{stateId}.geojson</c> from all active counties of that state.</summary>
    Task RebuildCountiesBundleAsync(Guid stateId, IEnumerable<(Guid Id, string Name, string? Fips, string FeatureGeoJson)> features, CancellationToken ct = default);
}
