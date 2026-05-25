namespace DataHub.Core.Entities.Geo;

/// <summary>
/// A sovereign country or comparable top-level administrative region.
/// Geometry is intentionally NOT stored in the DB; the frontend renders boundaries
/// from static GeoJSON assets under <c>wwwroot/geo/</c>, joined to these rows by ISO/FIPS.
/// </summary>
public class Country : AuditableEntity
{
    /// <summary>ISO 3166-1 alpha-2 code, e.g. "US". Canonical join key to GeoJSON.</summary>
    public required string Iso2 { get; set; }

    /// <summary>ISO 3166-1 alpha-3 code, e.g. "USA".</summary>
    public string? Iso3 { get; set; }

    /// <summary>Display name, e.g. "United States".</summary>
    public required string Name { get; set; }

    public ICollection<State> States { get; set; } = new List<State>();
}
