namespace DataHub.Core.DTOs.Geo;

// Boundaries are NOT stored in the DB or returned by these DTOs. The frontend
// renders polygons from static GeoJSON assets under /geo/* and joins them to
// these rows by ISO/FIPS code.

public record CountryDto(
    Guid Id,
    string Iso2,
    string? Iso3,
    string Name,
    bool IsActive,
    DateTime UpdatedAt);

public record StateDto(
    Guid Id,
    Guid CountryId,
    string Code,
    string Name,
    string? Fips,
    bool IsActive,
    DateTime UpdatedAt);

public record CountyDto(
    Guid Id,
    Guid StateId,
    string Name,
    string? Fips,
    bool IsActive,
    DateTime UpdatedAt);

public record CreateCountryRequest(string Iso2, string? Iso3, string Name);
public record UpdateCountryRequest(string Iso2, string? Iso3, string Name);

public record CreateStateRequest(string Code, string Name, string? Fips);
public record UpdateStateRequest(string Code, string Name, string? Fips);

public record CreateCountyRequest(string Name, string? Fips);
public record UpdateCountyRequest(string Name, string? Fips);

/// <summary>
/// One row in a choropleth payload. Joined to GeoJSON features on the frontend
/// by <c>Fips</c> (or ISO-2 for country-level metrics).
/// </summary>
public record GeoMetricDto(string Fips, string Name, long Count);
