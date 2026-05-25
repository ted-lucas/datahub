using System.Text.Json;
using DataHub.Core.Entities.Geo;
using DataHub.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DataHub.Infrastructure.Seeding;

/// <summary>
/// Seeds reference-data rows for USA + 50 states + DC + ~3,143 counties. Boundaries
/// are NOT stored in the DB; the frontend renders polygons from the static GeoJSON
/// files under <c>src/DataHub.Api/wwwroot/geo/</c> and joins to these rows by FIPS.
///
/// Source for the FIPS / name list: the embedded us-atlas <c>counties-10m.geojson</c>
/// (same file the frontend serves statically). We only read the <c>id</c> + <c>name</c>
/// properties — no geometry parsing, no NTS, no SQL Server <c>geography</c>.
///
/// Idempotent: if any Counties already exist, the seeder is a no-op.
/// First run is sub-second.
/// </summary>
public static class GeoSeeder
{
    private const string SourceTag = "seed:geo-us-atlas-10m";
    private const string EmbeddedResourceSuffix = ".Seeding.Data.counties-10m.geojson";

    public static async Task SeedAsync(DataHubDbContext db, CancellationToken ct = default)
    {
        if (await db.Counties.AnyAsync(ct)) return; // already seeded

        var sw = System.Diagnostics.Stopwatch.StartNew();

        // ----- Country -----
        var usa = await db.Countries.FirstOrDefaultAsync(c => c.Iso2 == "US", ct);
        if (usa is null)
        {
            usa = new Country { Iso2 = "US", Iso3 = "USA", Name = "United States", IsActive = true, Source = SourceTag };
            db.Countries.Add(usa);
            await db.SaveChangesAsync(ct);
        }

        // ----- Read FIPS + name list from the embedded counties file -----
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

        // (fips5, name) per county
        var countyRows = new List<(string Fips5, string Name)>(capacity: 3500);
        var stateFipsSet = new HashSet<string>(StringComparer.Ordinal);

        foreach (var feature in features.EnumerateArray())
        {
            ct.ThrowIfCancellationRequested();
            var fips = feature.GetProperty("id").GetString();
            if (string.IsNullOrWhiteSpace(fips) || fips.Length != 5) continue;
            var name = feature.GetProperty("properties").GetProperty("name").GetString() ?? "(unknown)";
            countyRows.Add((fips, name));
            stateFipsSet.Add(fips[..2]);
        }

        // ----- States -----
        var existingStates = await db.States
            .Where(s => s.CountryId == usa.Id)
            .ToDictionaryAsync(s => s.Fips ?? string.Empty, ct);

        var statesByFips = new Dictionary<string, State>();
        var newStates = new List<State>();

        foreach (var stateFips in stateFipsSet.OrderBy(s => s, StringComparer.Ordinal))
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

        // ----- Counties (batched insert) -----
        var counties = new List<County>(capacity: countyRows.Count);
        foreach (var (fips5, name) in countyRows)
        {
            if (!statesByFips.TryGetValue(fips5[..2], out var state)) continue;
            counties.Add(new County
            {
                StateId = state.Id,
                Name = name,
                Fips = fips5,
                IsActive = true,
                Source = SourceTag,
            });
        }

        const int batchSize = 1000;
        for (var i = 0; i < counties.Count; i += batchSize)
        {
            var batch = counties.Skip(i).Take(batchSize).ToList();
            db.Counties.AddRange(batch);
            await db.SaveChangesAsync(ct);
            foreach (var entry in db.ChangeTracker.Entries<County>().ToList())
                entry.State = EntityState.Detached;
        }

        sw.Stop();
        Console.WriteLine(
            $"[GeoSeeder] Done in {sw.Elapsed.TotalSeconds:F2}s. " +
            $"Countries: 1, States: {statesByFips.Count} ({newStates.Count} new), " +
            $"Counties: {counties.Count}.");
    }
}

/// <summary>
/// Static FIPS -> (postal code, name) lookup for US states + DC.
/// Source: US Census Bureau. Territories (PR, VI, GU, MP, AS) omitted because
/// the us-atlas counties-10m file doesn't include them.
///
/// Public so non-seeder code (e.g. <c>GeoService</c>'s metric aggregations) can
/// translate the free-form postal codes stored on Team/Venue rows into the
/// 2-digit FIPS used as the choropleth join key.
/// </summary>
public static class UsStates
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

    /// <summary>
    /// Reverse lookup: postal code (uppercased) -> 2-digit FIPS string.
    /// Built once on first access from <see cref="ByFips"/>.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> FipsByPostal =
        ByFips.ToDictionary(kv => kv.Value.Postal, kv => kv.Key, StringComparer.OrdinalIgnoreCase);
}
