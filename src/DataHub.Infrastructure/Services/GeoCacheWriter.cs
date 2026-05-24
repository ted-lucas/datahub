using DataHub.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using NetTopologySuite.Features;
using NetTopologySuite.IO;
using System.Text;

namespace DataHub.Infrastructure.Services;

/// <summary>
/// Writes per-entity GeoJSON files and merged bundle files to disk. Files are
/// served by ASP.NET Core's static file middleware (configured in Program.cs)
/// at the URL prefix <c>/geo-cache/</c>.
/// </summary>
public class GeoCacheWriter : IGeoCacheWriter
{
    public string CacheRoot { get; }

    public GeoCacheWriter(IConfiguration config)
    {
        // Default: <contentRoot>/wwwroot/geo-cache. Configurable via Geo:CacheRoot.
        var configured = config["Geo:CacheRoot"];
        CacheRoot = !string.IsNullOrWhiteSpace(configured)
            ? Path.GetFullPath(configured)
            : Path.Combine(AppContext.BaseDirectory, "wwwroot", "geo-cache");

        Directory.CreateDirectory(Path.Combine(CacheRoot, "countries"));
        Directory.CreateDirectory(Path.Combine(CacheRoot, "states"));
        Directory.CreateDirectory(Path.Combine(CacheRoot, "counties"));
    }

    public Task WriteCountryAsync(Guid id, string geoJson, CancellationToken ct = default)
        => WriteAsync(Path.Combine(CacheRoot, "countries", $"{id}.geojson"), geoJson, ct);

    public Task WriteStateAsync(Guid id, string geoJson, CancellationToken ct = default)
        => WriteAsync(Path.Combine(CacheRoot, "states", $"{id}.geojson"), geoJson, ct);

    public Task WriteCountyAsync(Guid id, string geoJson, CancellationToken ct = default)
        => WriteAsync(Path.Combine(CacheRoot, "counties", $"{id}.geojson"), geoJson, ct);

    public async Task RebuildStatesBundleAsync(
        Guid countryId,
        IEnumerable<(Guid Id, string Code, string Name, string FeatureGeoJson)> features,
        CancellationToken ct = default)
    {
        var path = Path.Combine(CacheRoot, "states", $"bundle-{countryId}.geojson");
        await WriteFeatureCollectionAsync(
            path,
            features.Select(f => (f.FeatureGeoJson, (IDictionary<string, object?>)new Dictionary<string, object?>
            {
                ["id"] = f.Id,
                ["code"] = f.Code,
                ["name"] = f.Name,
            })),
            ct);
    }

    public async Task RebuildCountiesBundleAsync(
        Guid stateId,
        IEnumerable<(Guid Id, string Name, string? Fips, string FeatureGeoJson)> features,
        CancellationToken ct = default)
    {
        var path = Path.Combine(CacheRoot, "counties", $"bundle-{stateId}.geojson");
        await WriteFeatureCollectionAsync(
            path,
            features.Select(f => (f.FeatureGeoJson, (IDictionary<string, object?>)new Dictionary<string, object?>
            {
                ["id"] = f.Id,
                ["name"] = f.Name,
                ["fips"] = f.Fips,
            })),
            ct);
    }

    private static async Task WriteAsync(string path, string content, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content, Encoding.UTF8, ct);
    }

    private static async Task WriteFeatureCollectionAsync(
        string path,
        IEnumerable<(string GeometryGeoJson, IDictionary<string, object?> Properties)> entries,
        CancellationToken ct)
    {
        var reader = new GeoJsonReader();
        var fc = new FeatureCollection();
        foreach (var (geoJson, props) in entries)
        {
            var geom = reader.Read<NetTopologySuite.Geometries.Geometry>(geoJson);
            var attrs = new AttributesTable();
            foreach (var kv in props) attrs.Add(kv.Key, kv.Value);
            fc.Add(new Feature(geom, attrs));
        }

        var writer = new GeoJsonWriter();
        var serialized = writer.Write(fc);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, serialized, Encoding.UTF8, ct);
    }
}
