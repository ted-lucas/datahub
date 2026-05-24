using DataHub.Core.DTOs.Geo;
using DataHub.Core.Entities.Geo;
using DataHub.Core.Interfaces;
using DataHub.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace DataHub.Infrastructure.Services;

public class GeoService : IGeoService
{
    private readonly DataHubDbContext _db;
    private readonly IGeoCacheWriter _cache;

    // SRID 4326 = WGS84 (lat/lon). SQL Server geography requires a valid SRID.
    private const int Wgs84 = 4326;

    public GeoService(DataHubDbContext db, IGeoCacheWriter cache)
    {
        _db = db;
        _cache = cache;
    }

    // ------------------------------------------------------------------ Countries

    public async Task<IReadOnlyList<CountryDto>> ListCountriesAsync(bool includeInactive = false, CancellationToken ct = default)
    {
        var q = _db.Countries.AsNoTracking();
        if (!includeInactive) q = q.Where(c => c.IsActive);
        return await q.OrderBy(c => c.Name)
            .Select(c => new CountryDto(c.Id, c.Iso2, c.Iso3, c.Name, c.Geometry != null, c.IsActive, c.UpdatedAt))
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

    public async Task<bool> SetCountryGeometryAsync(Guid id, string geoJson, CancellationToken ct = default)
    {
        var c = await _db.Countries.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return false;
        c.Geometry = ParseGeometry(geoJson);
        await _db.SaveChangesAsync(ct);
        await _cache.WriteCountryAsync(id, geoJson, ct);
        return true;
    }

    // ------------------------------------------------------------------ States

    public async Task<IReadOnlyList<StateDto>> ListStatesAsync(Guid countryId, bool includeInactive = false, CancellationToken ct = default)
    {
        var q = _db.States.AsNoTracking().Where(s => s.CountryId == countryId);
        if (!includeInactive) q = q.Where(s => s.IsActive);
        return await q.OrderBy(s => s.Name)
            .Select(s => new StateDto(s.Id, s.CountryId, s.Code, s.Name, s.Fips, s.Geometry != null, s.IsActive, s.UpdatedAt))
            .ToListAsync(ct);
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

    public async Task<bool> SetStateGeometryAsync(Guid id, string geoJson, CancellationToken ct = default)
    {
        var s = await _db.States.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return false;
        s.Geometry = ParseGeometry(geoJson);
        await _db.SaveChangesAsync(ct);
        await _cache.WriteStateAsync(id, geoJson, ct);
        return true;
    }

    // ------------------------------------------------------------------ Counties

    public async Task<IReadOnlyList<CountyDto>> ListCountiesAsync(Guid stateId, bool includeInactive = false, CancellationToken ct = default)
    {
        var q = _db.Counties.AsNoTracking().Where(c => c.StateId == stateId);
        if (!includeInactive) q = q.Where(c => c.IsActive);
        return await q.OrderBy(c => c.Name)
            .Select(c => new CountyDto(c.Id, c.StateId, c.Name, c.Fips, c.Geometry != null, c.IsActive, c.UpdatedAt))
            .ToListAsync(ct);
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

    public async Task<bool> SetCountyGeometryAsync(Guid id, string geoJson, CancellationToken ct = default)
    {
        var c = await _db.Counties.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return false;
        c.Geometry = ParseGeometry(geoJson);
        await _db.SaveChangesAsync(ct);
        await _cache.WriteCountyAsync(id, geoJson, ct);
        return true;
    }

    // ------------------------------------------------------------------ helpers

    private static Geometry ParseGeometry(string geoJson)
    {
        if (string.IsNullOrWhiteSpace(geoJson))
            throw new ArgumentException("GeoJSON payload is empty.", nameof(geoJson));

        var reader = new GeoJsonReader();
        var geom = reader.Read<Geometry>(geoJson)
            ?? throw new ArgumentException("Failed to parse GeoJSON geometry.", nameof(geoJson));
        geom.SRID = Wgs84;

        // SQL Server `geography` requires outer rings counter-clockwise (left-hand rule).
        // GeoJSON files are commonly clockwise; flip them so SQL Server doesn't interpret
        // each polygon as "everything except this region" (which then fails validation).
        return NormalizePolygonOrientation(geom);
    }

    private static Geometry NormalizePolygonOrientation(Geometry geom)
    {
        switch (geom)
        {
            case Polygon poly:
                return EnsureCcw(poly);
            case MultiPolygon mp:
                var polys = new Polygon[mp.NumGeometries];
                for (var i = 0; i < mp.NumGeometries; i++)
                    polys[i] = EnsureCcw((Polygon)mp.GetGeometryN(i));
                var result = new MultiPolygon(polys) { SRID = mp.SRID };
                return result;
            default:
                return geom;
        }
    }

    private static Polygon EnsureCcw(Polygon poly)
    {
        var shell = (LinearRing)poly.ExteriorRing;
        if (!IsCounterClockwise(shell)) shell = (LinearRing)shell.Reverse();

        var holes = new LinearRing[poly.NumInteriorRings];
        for (var i = 0; i < poly.NumInteriorRings; i++)
        {
            var hole = (LinearRing)poly.GetInteriorRingN(i);
            // Holes must be the opposite orientation of the shell (so: clockwise).
            if (IsCounterClockwise(hole)) hole = (LinearRing)hole.Reverse();
            holes[i] = hole;
        }

        var fixedPoly = new Polygon(shell, holes) { SRID = poly.SRID };
        return fixedPoly;
    }

    /// <summary>
    /// Shoelace-based CCW test matching SQL Server's <c>geography</c> shell convention.
    /// See <c>GeoSeeder.IsCounterClockwise</c> for the longer explanation.
    /// </summary>
    private static bool IsCounterClockwise(LinearRing ring)
    {
        var coords = ring.CoordinateSequence;
        double sum = 0;
        for (var i = 0; i < coords.Count - 1; i++)
        {
            var x1 = coords.GetX(i);
            var y1 = coords.GetY(i);
            var x2 = coords.GetX(i + 1);
            var y2 = coords.GetY(i + 1);
            sum += (x2 - x1) * (y2 + y1);
        }
        return sum < 0;
    }

    private static CountryDto ToDto(Country c) =>
        new(c.Id, c.Iso2, c.Iso3, c.Name, c.Geometry != null, c.IsActive, c.UpdatedAt);

    private static StateDto ToDto(State s) =>
        new(s.Id, s.CountryId, s.Code, s.Name, s.Fips, s.Geometry != null, s.IsActive, s.UpdatedAt);

    private static CountyDto ToDto(County c) =>
        new(c.Id, c.StateId, c.Name, c.Fips, c.Geometry != null, c.IsActive, c.UpdatedAt);
}
