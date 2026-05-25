namespace DataHub.Core.Entities.Geo;

/// <summary>
/// A second-level administrative subdivision (US county, parish, borough, etc.).
/// Geometry is rendered from static GeoJSON assets joined by 5-digit FIPS.
/// </summary>
public class County : AuditableEntity
{
    public Guid StateId { get; set; }
    public State? State { get; set; }

    /// <summary>Display name, e.g. "Los Angeles".</summary>
    public required string Name { get; set; }

    /// <summary>Full 5-digit FIPS code (state + county), e.g. "06037". Canonical join key to GeoJSON.</summary>
    public string? Fips { get; set; }
}
