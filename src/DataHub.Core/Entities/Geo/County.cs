using NetTopologySuite.Geometries;

namespace DataHub.Core.Entities.Geo;

/// <summary>
/// A second-level administrative subdivision (US county, parish, borough, etc.).
/// </summary>
public class County : AuditableEntity
{
    public Guid StateId { get; set; }
    public State? State { get; set; }

    /// <summary>Display name, e.g. "Los Angeles".</summary>
    public required string Name { get; set; }

    /// <summary>Full 5-digit FIPS code (state + county), e.g. "06037".</summary>
    public string? Fips { get; set; }

    /// <summary>County polygon / multipolygon. May be null if not yet loaded.</summary>
    public Geometry? Geometry { get; set; }
}
