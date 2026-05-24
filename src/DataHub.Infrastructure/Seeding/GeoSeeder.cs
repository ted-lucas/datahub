using System.Text.Json;
using DataHub.Core.Entities.Geo;
using DataHub.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace DataHub.Infrastructure.Seeding;

/// <summary>
/// Seeds the US Country, all 50 states + DC, and ~3,100 counties from the embedded
/// <c>counties-10m.geojson</c> (us-atlas, WGS84 lon/lat).
///
/// State polygons are computed by dissolving the county polygons of each state via
/// <see cref="UnaryUnionOp"/> so we don't need a separate states file.
///
/// Geometry insertion strategy:
///   1. Insert all rows via EF Core with <c>Geometry = NULL</c> (lets EF handle Ids,
///      audit fields, FKs cleanly without any geometry-validation headaches).
///   2. Update each row's geometry via raw SQL using the well-known SQL Server idiom
///      <c>geometry::STGeomFromText(@wkt,4326).MakeValid()</c> -> cast to
///      <c>geography</c>, with a <c>ReorientObject()</c> fallback for shells whose
///      orientation SQL Server doesn't like. SQL Server itself is the orientation
///      authority -- no client-side shoelace/CCW gymnastics required.
///
/// Idempotent: if any Counties already exist, the seeder is a no-op.
/// First run takes 30-90 seconds; subsequent startups are near-instant.
/// </summary>
public static class GeoSeeder
{
    private const string SourceTag = "seed:geo-us-atlas-10m";
    private const string EmbeddedResourceSuffix = ".Seeding.Data.counties-10m.geojson";
    private const int Wgs84 = 4326;

    public static async Task SeedAsync(DataHubDbContext db, CancellationToken ct = default)
    {
        if (await db.Counties.AnyAsync(ct)) return; // already seeded

        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("[GeoSeeder] Starting US geo seed (this takes 30-90 seconds on first run)...");

        // ----- Country (idempotent independent of counties) -----
        var usa = await db.Countries.FirstOrDefaultAsync(c => c.Iso2 == "US", ct);
        if (usa is null)
        {
            usa = new Country { Iso2 = "US", Iso3 = "USA", Name = "United States", IsActive = true, Source = SourceTag };
            db.Countries.Add(usa);
            await db.SaveChangesAsync(ct);
        }

        // ----- Load and parse the embedded counties file -----
        var assembly = typeof(GeoSeeder).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(EmbeddedResourceSuffix, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"Embedded resource ending in '{EmbeddedResourceSuffix}' not found. " +
                "Check that Seeding/Data/counties-10m.geojson is included as an <EmbeddedResource>.");

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Failed to open embedded resource '{resourceName}'.");

        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var features = doc.RootElement.GetProperty("features");

        var factory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(Wgs84);
        var wktWriter = new WKTWriter();

        // Per-county parsed geometry, keyed by FIPS (the embedded resource's `id`).
        var countyGeomByFips = new Dictionary<string, Geometry>(capacity: 3500);
        // Per-state-FIPS list of polygons for the UnaryUnion dissolve below.
        var geometriesByStateFips = new Dictionary<string, List<Geometry>>();
        // County rows to insert (without geometry).
        var countyRows = new List<County>(capacity: 3500);
        var skippedFeatures = 0;

        // ----- Pass 1: parse each county feature into NTS geometries -----
        foreach (var feature in features.EnumerateArray())
        {
            ct.ThrowIfCancellationRequested();

            var fips = feature.GetProperty("id").GetString();
            if (string.IsNullOrWhiteSpace(fips) || fips.Length != 5) continue;

            var stateFips = fips[..2];
            var name = feature.GetProperty("properties").GetProperty("name").GetString() ?? "(unknown)";

            Geometry? geom;
            try
            {
                geom = ParseGeometryElement(feature.GetProperty("geometry"), factory);
            }
            catch
            {
                skippedFeatures++;
                continue;
            }
            if (geom is null || geom.IsEmpty)
            {
                skippedFeatures++;
                continue;
            }

            countyGeomByFips[fips] = geom;

            if (!geometriesByStateFips.TryGetValue(stateFips, out var list))
            {
                list = new List<Geometry>();
                geometriesByStateFips[stateFips] = list;
            }
            list.Add(geom);

            countyRows.Add(new County
            {
                Name = name,
                Fips = fips,
                Geometry = null, // populated via raw SQL after EF insert
                IsActive = true,
                Source = SourceTag,
                // StateId set after states are inserted
            });
        }

        // ----- Pass 2: build State rows (geometry computed in DB later from counties) -----
        var existingStates = await db.States
            .Where(s => s.CountryId == usa.Id)
            .ToDictionaryAsync(s => s.Fips ?? string.Empty, ct);

        var statesByFips = new Dictionary<string, State>();
        var newStates = new List<State>();

        foreach (var (stateFips, _) in geometriesByStateFips.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            if (!UsStates.ByFips.TryGetValue(stateFips, out var meta)) continue;

            if (existingStates.TryGetValue(stateFips, out var existing))
            {
                statesByFips[stateFips] = existing;
                continue;
            }

            var state = new State
            {
                CountryId = usa.Id,
                Code = meta.Postal,
                Name = meta.Name,
                Fips = stateFips,
                Geometry = null, // populated via SQL UnionAggregate over counties below
                IsActive = true,
                Source = SourceTag,
            };
            statesByFips[stateFips] = state;
            newStates.Add(state);
        }

        if (newStates.Count > 0)
        {
            db.States.AddRange(newStates);
            await db.SaveChangesAsync(ct);
        }
        Console.WriteLine($"[GeoSeeder] States inserted: {newStates.Count} new ({existingStates.Count} pre-existing reused).");

        // ----- Pass 3: assign StateId to each county row and bulk-insert counties (no geometry) -----
        foreach (var county in countyRows)
        {
            var stateFips = county.Fips![..2];
            if (!statesByFips.TryGetValue(stateFips, out var state)) continue;
            county.StateId = state.Id;
        }
        var insertable = countyRows.Where(c => c.StateId != Guid.Empty).ToList();

        // Batched insert -- without geometry, EF can ship these efficiently.
        const int batchSize = 500;
        for (var i = 0; i < insertable.Count; i += batchSize)
        {
            var batch = insertable.Skip(i).Take(batchSize).ToList();
            db.Counties.AddRange(batch);
            await db.SaveChangesAsync(ct);
            foreach (var entry in db.ChangeTracker.Entries<County>().ToList())
                entry.State = EntityState.Detached;
        }
        Console.WriteLine($"[GeoSeeder] Counties inserted (geometry pending): {insertable.Count}.");

        // ----- Pass 4: update geometry via raw SQL, letting SQL Server handle orientation -----
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(ct);

        // Counties first (need the freshly-inserted IDs -- re-query by Fips since we detached)
        var countyIdsByFips = await db.Counties
            .Where(c => c.Source == SourceTag)
            .Select(c => new { c.Fips, c.Id })
            .ToDictionaryAsync(x => x.Fips!, x => x.Id, ct);

        var countyGeomOk = 0;
        var countyGeomFailed = 0;
        foreach (var (fips, geom) in countyGeomByFips)
        {
            if (!countyIdsByFips.TryGetValue(fips, out var id)) continue;
            try
            {
                await UpdateGeographyAsync(connection, "Counties", id, geom, wktWriter, ct);
                countyGeomOk++;
            }
            catch (Exception ex)
            {
                countyGeomFailed++;
                Console.WriteLine($"[GeoSeeder] County {fips}: geometry update failed: {ex.GetBaseException().Message}");
            }
        }

        // States: compute each state's polygon as the union of its counties via SQL.
        // SQL Server handles the planetary union natively (no NTS antimeridian quirks).
        const string stateUnionSql = @"
WITH UnionedStates AS (
    SELECT StateId, geography::UnionAggregate(Geometry.MakeValid()) AS Geom
    FROM Counties
    WHERE Geometry IS NOT NULL
    GROUP BY StateId
)
UPDATE s
SET s.Geometry = u.Geom
FROM States s
INNER JOIN UnionedStates u ON u.StateId = s.Id;";
        int stateGeomOk = 0;
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = stateUnionSql;
            cmd.CommandTimeout = 300;
            stateGeomOk = await cmd.ExecuteNonQueryAsync(ct);
            Console.WriteLine($"[GeoSeeder] State geometries computed in DB via UnionAggregate: {stateGeomOk} states updated.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GeoSeeder] State UnionAggregate failed (states will have NULL geometry, fixable later): {ex.GetBaseException().Message}");
        }

        sw.Stop();
        Console.WriteLine(
            $"[GeoSeeder] Done in {sw.Elapsed.TotalSeconds:F1}s. " +
            $"Countries: 1, States: {statesByFips.Count} ({stateGeomOk} with geometry), " +
            $"Counties: {insertable.Count} ({countyGeomOk} with geometry, {countyGeomFailed} geometry failed, " +
            $"{skippedFeatures} parse-skipped).");
    }

    /// <summary>
    /// Sets the <c>Geometry</c> column of the given table+id using SQL Server's
    /// native geography parser as the orientation authority.
    /// Strategy:
    ///   1. Try as-is.
    ///   2. If rejected, reverse all rings and retry.
    ///   3. For MultiPolygons, if both whole-flip attempts fail, decompose into
    ///      individual polygons, accept each one in whichever orientation SQL
    ///      Server allows, and rebuild a MULTIPOLYGON WKT from the accepted parts.
    /// </summary>
    private static async Task UpdateGeographyAsync(
        System.Data.Common.DbConnection connection,
        string tableName,
        Guid id,
        Geometry geom,
        WKTWriter wktWriter,
        CancellationToken ct)
    {
        var sql = $@"UPDATE [{tableName}] SET [Geometry] = geography::STGeomFromText(@wkt, 4326) WHERE [Id] = @id;";

        // Attempt 1: as-is.
        try { await ExecAsync(connection, sql, wktWriter.Write(geom), id, ct); return; }
        catch (Exception ex) when (IsOrientationError(ex)) { /* fall through */ }

        // Attempt 2: reverse all rings.
        var reversed = ReverseAllRings(geom);
        try { await ExecAsync(connection, sql, wktWriter.Write(reversed), id, ct); return; }
        catch (Exception ex) when (IsOrientationError(ex)) { /* fall through */ }

        // Attempt 3: per-polygon orientation discovery. Only meaningful for MultiPolygon.
        if (geom is MultiPolygon mp)
        {
            var factory = geom.Factory;
            var fixedPolys = new List<Polygon>(mp.NumGeometries);
            for (var i = 0; i < mp.NumGeometries; i++)
            {
                var part = (Polygon)mp.GetGeometryN(i);
                var partFixed = await TryEitherOrientationAsync(connection, part, wktWriter, ct);
                if (partFixed is not null) fixedPolys.Add(partFixed);
            }
            if (fixedPolys.Count > 0)
            {
                var rebuilt = factory.CreateMultiPolygon(fixedPolys.ToArray());
                rebuilt.SRID = geom.SRID;
                await ExecAsync(connection, sql, wktWriter.Write(rebuilt), id, ct);
                return;
            }
        }

        // No combination worked; surface the original error to the caller.
        await ExecAsync(connection, sql, wktWriter.Write(geom), id, ct);
    }

    /// <summary>
    /// Returns a polygon in whichever orientation SQL Server's geography parser
    /// accepts, or null if neither works. Uses a no-op SELECT to validate without
    /// touching any table.
    /// </summary>
    private static async Task<Polygon?> TryEitherOrientationAsync(
        System.Data.Common.DbConnection connection,
        Polygon poly,
        WKTWriter wktWriter,
        CancellationToken ct)
    {
        const string probeSql = "SELECT geography::STGeomFromText(@wkt, 4326);";
        try
        {
            await ProbeAsync(connection, probeSql, wktWriter.Write(poly), ct);
            return poly;
        }
        catch (Exception ex) when (IsOrientationError(ex))
        {
            var rev = ReversePolygon(poly);
            try
            {
                await ProbeAsync(connection, probeSql, wktWriter.Write(rev), ct);
                return rev;
            }
            catch
            {
                return null;
            }
        }
    }

    private static async Task ProbeAsync(
        System.Data.Common.DbConnection connection,
        string sql,
        string wkt,
        CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        var p = cmd.CreateParameter();
        p.ParameterName = "@wkt";
        p.Value = wkt;
        cmd.Parameters.Add(p);
        await cmd.ExecuteScalarAsync(ct);
    }

    private static async Task ExecAsync(
        System.Data.Common.DbConnection connection,
        string sql,
        string wkt,
        Guid id,
        CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        var pWkt = cmd.CreateParameter();
        pWkt.ParameterName = "@wkt";
        pWkt.Value = wkt;
        cmd.Parameters.Add(pWkt);
        var pId = cmd.CreateParameter();
        pId.ParameterName = "@id";
        pId.Value = id;
        cmd.Parameters.Add(pId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static bool IsOrientationError(Exception ex)
        => ex.GetBaseException().Message.Contains("counter-clockwise", StringComparison.OrdinalIgnoreCase)
        || ex.GetBaseException().Message.Contains("FullGlobe", StringComparison.OrdinalIgnoreCase);

    private static Geometry ReverseAllRings(Geometry geom)
    {
        switch (geom)
        {
            case Polygon poly: return ReversePolygon(poly);
            case MultiPolygon mp:
                var polys = new Polygon[mp.NumGeometries];
                for (var i = 0; i < mp.NumGeometries; i++)
                    polys[i] = ReversePolygon((Polygon)mp.GetGeometryN(i));
                return new MultiPolygon(polys) { SRID = mp.SRID };
            default: return geom;
        }
    }

    private static Polygon ReversePolygon(Polygon poly)
    {
        var shell = (LinearRing)((LinearRing)poly.ExteriorRing).Reverse();
        var holes = new LinearRing[poly.NumInteriorRings];
        for (var i = 0; i < poly.NumInteriorRings; i++)
            holes[i] = (LinearRing)((LinearRing)poly.GetInteriorRingN(i)).Reverse();
        return new Polygon(shell, holes) { SRID = poly.SRID };
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>
    /// Parses a GeoJSON Geometry JsonElement (Polygon or MultiPolygon) into an NTS
    /// geometry, sanitizing rings as it goes: removes consecutive duplicate points
    /// and closes any ring whose last point isn't equal to its first.
    /// </summary>
    private static Geometry? ParseGeometryElement(JsonElement geomElement, GeometryFactory factory)
    {
        var type = geomElement.GetProperty("type").GetString();
        var coords = geomElement.GetProperty("coordinates");
        return type switch
        {
            "Polygon" => BuildPolygon(coords, factory),
            "MultiPolygon" => BuildMultiPolygon(coords, factory),
            _ => null,
        };
    }

    private static Polygon? BuildPolygon(JsonElement ringsElement, GeometryFactory factory)
    {
        var rings = new List<LinearRing>();
        foreach (var ringElement in ringsElement.EnumerateArray())
        {
            var ring = BuildRing(ringElement, factory);
            if (ring is not null) rings.Add(ring);
        }
        if (rings.Count == 0) return null;
        var holes = rings.Count > 1 ? rings.Skip(1).ToArray() : Array.Empty<LinearRing>();
        return factory.CreatePolygon(rings[0], holes);
    }

    private static MultiPolygon? BuildMultiPolygon(JsonElement polysElement, GeometryFactory factory)
    {
        var polys = new List<Polygon>();
        foreach (var polyElement in polysElement.EnumerateArray())
        {
            var poly = BuildPolygon(polyElement, factory);
            if (poly is not null) polys.Add(poly);
        }
        if (polys.Count == 0) return null;
        return factory.CreateMultiPolygon(polys.ToArray());
    }

    private static LinearRing? BuildRing(JsonElement ringElement, GeometryFactory factory)
    {
        var raw = new List<Coordinate>(capacity: 32);
        foreach (var ptElement in ringElement.EnumerateArray())
        {
            if (ptElement.GetArrayLength() < 2) continue;
            var x = ptElement[0].GetDouble();
            var y = ptElement[1].GetDouble();
            if (raw.Count > 0 && raw[^1].X == x && raw[^1].Y == y) continue;
            raw.Add(new Coordinate(x, y));
        }
        if (raw.Count < 3) return null;
        if (!raw[0].Equals2D(raw[^1])) raw.Add(new Coordinate(raw[0].X, raw[0].Y));
        if (raw.Count < 4) return null;
        return factory.CreateLinearRing(raw.ToArray());
    }
}

/// <summary>
/// Static FIPS -> (postal code, name) lookup for US states + DC.
/// Source: US Census Bureau. Territories (PR, VI, GU, MP, AS) omitted because
/// the us-atlas counties-10m file doesn't include them.
/// </summary>
internal static class UsStates
{
    public readonly record struct StateMeta(string Postal, string Name);

    public static readonly IReadOnlyDictionary<string, StateMeta> ByFips = new Dictionary<string, StateMeta>
    {
        ["01"] = new("AL", "Alabama"),
        ["02"] = new("AK", "Alaska"),
        ["04"] = new("AZ", "Arizona"),
        ["05"] = new("AR", "Arkansas"),
        ["06"] = new("CA", "California"),
        ["08"] = new("CO", "Colorado"),
        ["09"] = new("CT", "Connecticut"),
        ["10"] = new("DE", "Delaware"),
        ["11"] = new("DC", "District of Columbia"),
        ["12"] = new("FL", "Florida"),
        ["13"] = new("GA", "Georgia"),
        ["15"] = new("HI", "Hawaii"),
        ["16"] = new("ID", "Idaho"),
        ["17"] = new("IL", "Illinois"),
        ["18"] = new("IN", "Indiana"),
        ["19"] = new("IA", "Iowa"),
        ["20"] = new("KS", "Kansas"),
        ["21"] = new("KY", "Kentucky"),
        ["22"] = new("LA", "Louisiana"),
        ["23"] = new("ME", "Maine"),
        ["24"] = new("MD", "Maryland"),
        ["25"] = new("MA", "Massachusetts"),
        ["26"] = new("MI", "Michigan"),
        ["27"] = new("MN", "Minnesota"),
        ["28"] = new("MS", "Mississippi"),
        ["29"] = new("MO", "Missouri"),
        ["30"] = new("MT", "Montana"),
        ["31"] = new("NE", "Nebraska"),
        ["32"] = new("NV", "Nevada"),
        ["33"] = new("NH", "New Hampshire"),
        ["34"] = new("NJ", "New Jersey"),
        ["35"] = new("NM", "New Mexico"),
        ["36"] = new("NY", "New York"),
        ["37"] = new("NC", "North Carolina"),
        ["38"] = new("ND", "North Dakota"),
        ["39"] = new("OH", "Ohio"),
        ["40"] = new("OK", "Oklahoma"),
        ["41"] = new("OR", "Oregon"),
        ["42"] = new("PA", "Pennsylvania"),
        ["44"] = new("RI", "Rhode Island"),
        ["45"] = new("SC", "South Carolina"),
        ["46"] = new("SD", "South Dakota"),
        ["47"] = new("TN", "Tennessee"),
        ["48"] = new("TX", "Texas"),
        ["49"] = new("UT", "Utah"),
        ["50"] = new("VT", "Vermont"),
        ["51"] = new("VA", "Virginia"),
        ["53"] = new("WA", "Washington"),
        ["54"] = new("WV", "West Virginia"),
        ["55"] = new("WI", "Wisconsin"),
        ["56"] = new("WY", "Wyoming"),
    };
}
