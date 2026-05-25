using DataHub.Core.DTOs.Geo;
using DataHub.Core.Entities.Geo;
using DataHub.Core.Interfaces;
using DataHub.Infrastructure.Data;
using DataHub.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;

namespace DataHub.Infrastructure.Services;

/// <summary>
/// Read-mostly reference data for the map module. No geometry — the frontend
/// renders polygons from static GeoJSON assets and joins to these rows by FIPS.
/// </summary>
public class GeoService : IGeoService
{
    private readonly DataHubDbContext _db;

    public GeoService(DataHubDbContext db)
    {
        _db = db;
    }

    // ------------------------------------------------------------------ Countries

    public async Task<IReadOnlyList<CountryDto>> ListCountriesAsync(bool includeInactive = false, CancellationToken ct = default)
    {
        var q = _db.Countries.AsNoTracking();
        if (!includeInactive) q = q.Where(c => c.IsActive);
        return await q.OrderBy(c => c.Name)
            .Select(c => new CountryDto(c.Id, c.Iso2, c.Iso3, c.Name, c.IsActive, c.UpdatedAt))
            .ToListAsync(ct);
    }

    public async Task<CountryDto?> GetCountryAsync(Guid id, CancellationToken ct = default)
    {
        var c = await _db.Countries.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return c is null ? null : ToDto(c);
    }

    public async Task<CountryDto?> GetCountryByIso2Async(string iso2, CancellationToken ct = default)
    {
        var c = await _db.Countries.AsNoTracking().FirstOrDefaultAsync(x => x.Iso2 == iso2, ct);
        return c is null ? null : ToDto(c);
    }

    public async Task<CountryDto> CreateCountryAsync(CreateCountryRequest req, CancellationToken ct = default)
    {
        var c = new Country { Iso2 = req.Iso2, Iso3 = req.Iso3, Name = req.Name, IsActive = true };
        _db.Countries.Add(c);
        await _db.SaveChangesAsync(ct);
        return ToDto(c);
    }

    public async Task<CountryDto?> UpdateCountryAsync(Guid id, UpdateCountryRequest req, CancellationToken ct = default)
    {
        var c = await _db.Countries.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return null;
        c.Iso2 = req.Iso2;
        c.Iso3 = req.Iso3;
        c.Name = req.Name;
        await _db.SaveChangesAsync(ct);
        return ToDto(c);
    }

    public async Task<bool> DeactivateCountryAsync(Guid id, CancellationToken ct = default)
    {
        var c = await _db.Countries.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return false;
        c.IsActive = false;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ------------------------------------------------------------------ States

    public async Task<IReadOnlyList<StateDto>> ListStatesAsync(Guid countryId, bool includeInactive = false, CancellationToken ct = default)
    {
        var q = _db.States.AsNoTracking().Where(s => s.CountryId == countryId);
        if (!includeInactive) q = q.Where(s => s.IsActive);
        return await q.OrderBy(s => s.Name)
            .Select(s => new StateDto(s.Id, s.CountryId, s.Code, s.Name, s.Fips, s.IsActive, s.UpdatedAt))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<StateDto>> ListStatesByCountryIso2Async(string iso2, bool includeInactive = false, CancellationToken ct = default)
    {
        var q = from s in _db.States.AsNoTracking()
                join c in _db.Countries.AsNoTracking() on s.CountryId equals c.Id
                where c.Iso2 == iso2 && (includeInactive || s.IsActive)
                orderby s.Name
                select new StateDto(s.Id, s.CountryId, s.Code, s.Name, s.Fips, s.IsActive, s.UpdatedAt);
        return await q.ToListAsync(ct);
    }

    public async Task<StateDto?> GetStateAsync(Guid id, CancellationToken ct = default)
    {
        var s = await _db.States.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return s is null ? null : ToDto(s);
    }

    public async Task<StateDto> CreateStateAsync(Guid countryId, CreateStateRequest req, CancellationToken ct = default)
    {
        var s = new State { CountryId = countryId, Code = req.Code, Name = req.Name, Fips = req.Fips, IsActive = true };
        _db.States.Add(s);
        await _db.SaveChangesAsync(ct);
        return ToDto(s);
    }

    public async Task<StateDto?> UpdateStateAsync(Guid id, UpdateStateRequest req, CancellationToken ct = default)
    {
        var s = await _db.States.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return null;
        s.Code = req.Code;
        s.Name = req.Name;
        s.Fips = req.Fips;
        await _db.SaveChangesAsync(ct);
        return ToDto(s);
    }

    public async Task<bool> DeactivateStateAsync(Guid id, CancellationToken ct = default)
    {
        var s = await _db.States.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return false;
        s.IsActive = false;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ------------------------------------------------------------------ Counties

    public async Task<IReadOnlyList<CountyDto>> ListCountiesAsync(Guid stateId, bool includeInactive = false, CancellationToken ct = default)
    {
        var q = _db.Counties.AsNoTracking().Where(c => c.StateId == stateId);
        if (!includeInactive) q = q.Where(c => c.IsActive);
        return await q.OrderBy(c => c.Name)
            .Select(c => new CountyDto(c.Id, c.StateId, c.Name, c.Fips, c.IsActive, c.UpdatedAt))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CountyDto>> ListCountiesByStateFipsAsync(string stateFips, bool includeInactive = false, CancellationToken ct = default)
    {
        var q = from c in _db.Counties.AsNoTracking()
                join s in _db.States.AsNoTracking() on c.StateId equals s.Id
                where s.Fips == stateFips && (includeInactive || c.IsActive)
                orderby c.Name
                select new CountyDto(c.Id, c.StateId, c.Name, c.Fips, c.IsActive, c.UpdatedAt);
        return await q.ToListAsync(ct);
    }

    public async Task<CountyDto?> GetCountyAsync(Guid id, CancellationToken ct = default)
    {
        var c = await _db.Counties.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return c is null ? null : ToDto(c);
    }

    public async Task<CountyDto> CreateCountyAsync(Guid stateId, CreateCountyRequest req, CancellationToken ct = default)
    {
        var c = new County { StateId = stateId, Name = req.Name, Fips = req.Fips, IsActive = true };
        _db.Counties.Add(c);
        await _db.SaveChangesAsync(ct);
        return ToDto(c);
    }

    public async Task<CountyDto?> UpdateCountyAsync(Guid id, UpdateCountyRequest req, CancellationToken ct = default)
    {
        var c = await _db.Counties.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return null;
        c.Name = req.Name;
        c.Fips = req.Fips;
        await _db.SaveChangesAsync(ct);
        return ToDto(c);
    }

    public async Task<bool> DeactivateCountyAsync(Guid id, CancellationToken ct = default)
    {
        var c = await _db.Counties.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return false;
        c.IsActive = false;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ------------------------------------------------------------------ Metrics

    /// <summary>
    /// Choropleth data source. Returns one row per region keyed by FIPS (state/county)
    /// or ISO-2 (country). See <see cref="GeoMetricKind"/> for what's counted.
    /// </summary>
    public async Task<IReadOnlyList<GeoMetricDto>> GetMetricsAsync(
        GeoMetricsLevel level,
        string? parentFips,
        GeoMetricKind kind = GeoMetricKind.Regions,
        CancellationToken ct = default)
    {
        return kind switch
        {
            GeoMetricKind.Regions => await RegionsMetricsAsync(level, parentFips, ct),
            GeoMetricKind.Teams => await TeamsMetricsAsync(level, parentFips, ct),
            GeoMetricKind.Venues => await VenuesMetricsAsync(level, parentFips, ct),
            _ => Array.Empty<GeoMetricDto>(),
        };
    }

    // Original placeholder: counts of geographic children. Always usable, even
    // with zero domain data loaded.
    private async Task<IReadOnlyList<GeoMetricDto>> RegionsMetricsAsync(
        GeoMetricsLevel level, string? parentFips, CancellationToken ct)
    {
        switch (level)
        {
            case GeoMetricsLevel.Country:
                return await _db.Countries.AsNoTracking()
                    .Where(c => c.IsActive)
                    .Select(c => new GeoMetricDto(c.Iso2, c.Name, c.States.Count(s => s.IsActive)))
                    .ToListAsync(ct);

            case GeoMetricsLevel.State:
                return await _db.States.AsNoTracking()
                    .Where(s => s.IsActive && s.Fips != null)
                    .Select(s => new GeoMetricDto(s.Fips!, s.Name, s.Counties.Count(c => c.IsActive)))
                    .ToListAsync(ct);

            case GeoMetricsLevel.County:
                var q = _db.Counties.AsNoTracking().Where(c => c.IsActive && c.Fips != null);
                if (!string.IsNullOrWhiteSpace(parentFips))
                    q = q.Where(c => c.Fips!.StartsWith(parentFips));
                // Count = 1 so all counties have a non-zero shade until real metrics arrive.
                return await q.Select(c => new GeoMetricDto(c.Fips!, c.Name, 1L)).ToListAsync(ct);

            default:
                return Array.Empty<GeoMetricDto>();
        }
    }

    // Sports.Team counts. Keyed at country level by `Team.Country` (matched
    // loosely against ISO-2 *and* legacy ISO-3 forms like "USA"), and at state
    // level by `Team.State` postal -> 2-digit FIPS via the seeded lookup.
    private async Task<IReadOnlyList<GeoMetricDto>> TeamsMetricsAsync(
        GeoMetricsLevel level, string? parentFips, CancellationToken ct)
    {
        if (level == GeoMetricsLevel.County)
        {
            // Team has no county column; degrade gracefully so the map still paints.
            return await RegionsMetricsAsync(level, parentFips, ct);
        }

        if (level == GeoMetricsLevel.Country)
        {
            var grouped = await _db.Teams.AsNoTracking()
                .Where(t => t.IsActive && t.Country != null)
                .GroupBy(t => t.Country!)
                .Select(g => new { Country = g.Key, Count = g.LongCount() })
                .ToListAsync(ct);

            // Normalize a few common encodings of the same country to a single ISO-2 row.
            return CollapseByIso2(grouped.Select(x => (x.Country, x.Count)));
        }

        // State level: optionally narrow by parent country (ISO-2 like "US").
        var teamsQ = _db.Teams.AsNoTracking()
            .Where(t => t.IsActive && t.State != null);
        if (IsUsaParent(parentFips))
            teamsQ = teamsQ.Where(t => t.Country == "US" || t.Country == "USA");

        var stateRows = await teamsQ
            .GroupBy(t => t.State!)
            .Select(g => new { Postal = g.Key, Count = g.LongCount() })
            .ToListAsync(ct);

        return ToStateMetrics(stateRows.Select(r => (r.Postal, r.Count)));
    }

    // Sports.Venue counts. Same join shape as Teams.
    private async Task<IReadOnlyList<GeoMetricDto>> VenuesMetricsAsync(
        GeoMetricsLevel level, string? parentFips, CancellationToken ct)
    {
        if (level == GeoMetricsLevel.County)
            return await RegionsMetricsAsync(level, parentFips, ct);

        if (level == GeoMetricsLevel.Country)
        {
            var grouped = await _db.Venues.AsNoTracking()
                .Where(v => v.IsActive && v.Country != null)
                .GroupBy(v => v.Country!)
                .Select(g => new { Country = g.Key, Count = g.LongCount() })
                .ToListAsync(ct);

            return CollapseByIso2(grouped.Select(x => (x.Country, x.Count)));
        }

        var venuesQ = _db.Venues.AsNoTracking()
            .Where(v => v.IsActive && v.State != null);
        if (IsUsaParent(parentFips))
            venuesQ = venuesQ.Where(v => v.Country == "US" || v.Country == "USA");

        var stateRows = await venuesQ
            .GroupBy(v => v.State!)
            .Select(g => new { Postal = g.Key, Count = g.LongCount() })
            .ToListAsync(ct);

        return ToStateMetrics(stateRows.Select(r => (r.Postal, r.Count)));
    }

    // ── metric helpers ─────────────────────────────────────────────────────

    private static bool IsUsaParent(string? parent) =>
        !string.IsNullOrWhiteSpace(parent) &&
        (parent.Equals("US", StringComparison.OrdinalIgnoreCase) ||
         parent.Equals("USA", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Map raw <c>(Country string, count)</c> rows to ISO-2-keyed metrics, merging
    /// duplicates that differ only by ISO-2 vs ISO-3 encoding. Names come from the
    /// Countries table when we have a row; otherwise we pass the raw value through.
    /// </summary>
    private IReadOnlyList<GeoMetricDto> CollapseByIso2(IEnumerable<(string Country, long Count)> rows)
    {
        // Build an in-memory country lookup once. Small table (Phase 2 = a handful of rows).
        var countries = _db.Countries.AsNoTracking()
            .Where(c => c.IsActive)
            .Select(c => new { c.Iso2, c.Iso3, c.Name })
            .ToList();

        var byIso2 = countries.ToDictionary(c => c.Iso2.ToUpperInvariant(), c => c.Name, StringComparer.OrdinalIgnoreCase);
        var iso3ToIso2 = countries
            .Where(c => !string.IsNullOrEmpty(c.Iso3))
            .GroupBy(c => c.Iso3.ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.First().Iso2.ToUpperInvariant(), StringComparer.OrdinalIgnoreCase);

        var merged = new Dictionary<string, (string Name, long Count)>(StringComparer.OrdinalIgnoreCase);
        foreach (var (raw, count) in rows)
        {
            var key = raw.ToUpperInvariant();
            if (iso3ToIso2.TryGetValue(key, out var iso2)) key = iso2;

            var name = byIso2.TryGetValue(key, out var n) ? n : raw;
            if (merged.TryGetValue(key, out var existing))
                merged[key] = (existing.Name, existing.Count + count);
            else
                merged[key] = (name, count);
        }

        return merged
            .Select(kv => new GeoMetricDto(kv.Key, kv.Value.Name, kv.Value.Count))
            .ToList();
    }

    /// <summary>
    /// Map raw <c>(state postal, count)</c> rows to FIPS-keyed metrics using
    /// <see cref="UsStates.FipsByPostal"/>. Unknown postals are silently dropped
    /// (they wouldn't render on the map anyway).
    /// </summary>
    private static IReadOnlyList<GeoMetricDto> ToStateMetrics(IEnumerable<(string Postal, long Count)> rows)
    {
        var merged = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var (postal, count) in rows)
        {
            if (postal is null) continue;
            if (!UsStates.FipsByPostal.TryGetValue(postal.Trim(), out var fips)) continue;
            merged[fips] = merged.TryGetValue(fips, out var existing) ? existing + count : count;
        }
        return merged
            .Select(kv => new GeoMetricDto(
                kv.Key,
                UsStates.ByFips.TryGetValue(kv.Key, out var meta) ? meta.Name : kv.Key,
                kv.Value))
            .ToList();
    }

    // ------------------------------------------------------------------ helpers

    private static CountryDto ToDto(Country c) =>
        new(c.Id, c.Iso2, c.Iso3, c.Name, c.IsActive, c.UpdatedAt);

    private static StateDto ToDto(State s) =>
        new(s.Id, s.CountryId, s.Code, s.Name, s.Fips, s.IsActive, s.UpdatedAt);

    private static CountyDto ToDto(County c) =>
        new(c.Id, c.StateId, c.Name, c.Fips, c.IsActive, c.UpdatedAt);
}
