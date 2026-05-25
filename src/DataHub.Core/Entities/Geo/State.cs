namespace DataHub.Core.Entities.Geo;

/// <summary>
/// A first-level administrative subdivision (US state, Canadian province, etc.).
/// Geometry is rendered from static GeoJSON assets joined by FIPS / postal code.
/// </summary>
public class State : AuditableEntity
{
    public Guid CountryId { get; set; }
    public Country? Country { get; set; }

    /// <summary>Short code, e.g. "CA" (US postal) or ISO 3166-2 subdivision code.</summary>
    public required string Code { get; set; }

    /// <summary>Display name, e.g. "California".</summary>
    public required string Name { get; set; }

    /// <summary>FIPS code (US states only), e.g. "06" for California. Canonical join key to GeoJSON.</summary>
    public string? Fips { get; set; }

    public ICollection<County> Counties { get; set; } = new List<County>();
}
