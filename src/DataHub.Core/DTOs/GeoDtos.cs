namespace DataHub.Core.DTOs.Geo;

// Notes:
// - List/detail DTOs intentionally omit raw geometry. The frontend fetches geometry
//   from cached GeoJSON files served under /geo-cache/, keyed by Id + UpdatedAt.
// - Geometry write endpoints accept GeoJSON as a string (the service parses it
//   via NetTopologySuite's GeoJsonReader, then writes the cache file).

public record CountryDto(
    Guid Id,
    string Iso2,
    string? Iso3,
    string Name,
    bool HasGeometry,
    bool IsActive,
    DateTime UpdatedAt);

public record StateDto(
    Guid Id,
    Guid CountryId,
    string Code,
    string Name,
    string? Fips,
    bool HasGeometry,
    bool IsActive,
    DateTime UpdatedAt);

public record CountyDto(
    Guid Id,
    Guid StateId,
    string Name,
    string? Fips,
    bool HasGeometry,
    bool IsActive,
    DateTime UpdatedAt);

public record CreateCountryRequest(string Iso2, string? Iso3, string Name);
public record UpdateCountryRequest(string Iso2, string? Iso3, string Name);

public record CreateStateRequest(string Code, string Name, string? Fips);
public record UpdateStateRequest(string Code, string Name, string? Fips);

public record CreateCountyRequest(string Name, string? Fips);
public record UpdateCountyRequest(string Name, string? Fips);

/// <summary>
/// Replaces the geometry on a geographic entity. <c>GeoJson</c> must be a valid
/// GeoJSON <c>Geometry</c> object (not a Feature or FeatureCollection).
/// </summary>
public record SetGeometryRequest(string GeoJson);
