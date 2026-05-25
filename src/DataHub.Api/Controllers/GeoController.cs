using DataHub.Core.Constants;
using DataHub.Core.DTOs.Geo;
using DataHub.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DataHub.Api.Controllers;

// ============================================================================
// Countries
// ============================================================================
[ApiController]
[Authorize]
[Route("api/geo/countries")]
public class GeoCountriesController : ControllerBase
{
    private readonly IGeoService _geo;
    public GeoCountriesController(IGeoService geo) { _geo = geo; }

    [HttpGet]
    [Authorize(Policy = Permissions.GeoRead)]
    public async Task<ActionResult<IReadOnlyList<CountryDto>>> List([FromQuery] bool includeInactive = false, CancellationToken ct = default)
        => Ok(await _geo.ListCountriesAsync(includeInactive, ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.GeoRead)]
    public async Task<ActionResult<CountryDto>> Get(Guid id, CancellationToken ct)
        => await _geo.GetCountryAsync(id, ct) is { } dto ? Ok(dto) : NotFound();

    [HttpGet("by-iso2/{iso2}")]
    [Authorize(Policy = Permissions.GeoRead)]
    public async Task<ActionResult<CountryDto>> GetByIso2(string iso2, CancellationToken ct)
        => await _geo.GetCountryByIso2Async(iso2, ct) is { } dto ? Ok(dto) : NotFound();

    [HttpPost]
    [Authorize(Policy = Permissions.GeoManage)]
    public async Task<ActionResult<CountryDto>> Create([FromBody] CreateCountryRequest req, CancellationToken ct)
    {
        var dto = await _geo.CreateCountryAsync(req, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.GeoManage)]
    public async Task<ActionResult<CountryDto>> Update(Guid id, [FromBody] UpdateCountryRequest req, CancellationToken ct)
        => await _geo.UpdateCountryAsync(id, req, ct) is { } dto ? Ok(dto) : NotFound();

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.GeoManage)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
        => await _geo.DeactivateCountryAsync(id, ct) ? NoContent() : NotFound();

    [HttpGet("{countryId:guid}/states")]
    [Authorize(Policy = Permissions.GeoRead)]
    public async Task<ActionResult<IReadOnlyList<StateDto>>> ListStates(Guid countryId, [FromQuery] bool includeInactive = false, CancellationToken ct = default)
        => Ok(await _geo.ListStatesAsync(countryId, includeInactive, ct));

    [HttpPost("{countryId:guid}/states")]
    [Authorize(Policy = Permissions.GeoManage)]
    public async Task<ActionResult<StateDto>> CreateState(Guid countryId, [FromBody] CreateStateRequest req, CancellationToken ct)
    {
        var dto = await _geo.CreateStateAsync(countryId, req, ct);
        return CreatedAtAction(nameof(GeoStatesController.Get), "GeoStates", new { id = dto.Id }, dto);
    }
}

// ============================================================================
// States
// ============================================================================
[ApiController]
[Authorize]
[Route("api/geo/states")]
public class GeoStatesController : ControllerBase
{
    private readonly IGeoService _geo;
    public GeoStatesController(IGeoService geo) { _geo = geo; }

    /// <summary>List states for a country, addressed by ISO-2 (e.g. <c>?country=US</c>).</summary>
    [HttpGet]
    [Authorize(Policy = Permissions.GeoRead)]
    public async Task<ActionResult<IReadOnlyList<StateDto>>> List([FromQuery] string country, [FromQuery] bool includeInactive = false, CancellationToken ct = default)
        => Ok(await _geo.ListStatesByCountryIso2Async(country, includeInactive, ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.GeoRead)]
    public async Task<ActionResult<StateDto>> Get(Guid id, CancellationToken ct)
        => await _geo.GetStateAsync(id, ct) is { } dto ? Ok(dto) : NotFound();

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.GeoManage)]
    public async Task<ActionResult<StateDto>> Update(Guid id, [FromBody] UpdateStateRequest req, CancellationToken ct)
        => await _geo.UpdateStateAsync(id, req, ct) is { } dto ? Ok(dto) : NotFound();

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.GeoManage)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
        => await _geo.DeactivateStateAsync(id, ct) ? NoContent() : NotFound();

    [HttpGet("{stateId:guid}/counties")]
    [Authorize(Policy = Permissions.GeoRead)]
    public async Task<ActionResult<IReadOnlyList<CountyDto>>> ListCounties(Guid stateId, [FromQuery] bool includeInactive = false, CancellationToken ct = default)
        => Ok(await _geo.ListCountiesAsync(stateId, includeInactive, ct));

    [HttpPost("{stateId:guid}/counties")]
    [Authorize(Policy = Permissions.GeoManage)]
    public async Task<ActionResult<CountyDto>> CreateCounty(Guid stateId, [FromBody] CreateCountyRequest req, CancellationToken ct)
    {
        var dto = await _geo.CreateCountyAsync(stateId, req, ct);
        return CreatedAtAction(nameof(GeoCountiesController.Get), "GeoCounties", new { id = dto.Id }, dto);
    }
}

// ============================================================================
// Counties
// ============================================================================
[ApiController]
[Authorize]
[Route("api/geo/counties")]
public class GeoCountiesController : ControllerBase
{
    private readonly IGeoService _geo;
    public GeoCountiesController(IGeoService geo) { _geo = geo; }

    /// <summary>List counties for a state, addressed by state FIPS (e.g. <c>?state=06</c>).</summary>
    [HttpGet]
    [Authorize(Policy = Permissions.GeoRead)]
    public async Task<ActionResult<IReadOnlyList<CountyDto>>> List([FromQuery] string state, [FromQuery] bool includeInactive = false, CancellationToken ct = default)
        => Ok(await _geo.ListCountiesByStateFipsAsync(state, includeInactive, ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.GeoRead)]
    public async Task<ActionResult<CountyDto>> Get(Guid id, CancellationToken ct)
        => await _geo.GetCountyAsync(id, ct) is { } dto ? Ok(dto) : NotFound();

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.GeoManage)]
    public async Task<ActionResult<CountyDto>> Update(Guid id, [FromBody] UpdateCountyRequest req, CancellationToken ct)
        => await _geo.UpdateCountyAsync(id, req, ct) is { } dto ? Ok(dto) : NotFound();

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.GeoManage)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
        => await _geo.DeactivateCountyAsync(id, ct) ? NoContent() : NotFound();
}

// ============================================================================
// Metrics — choropleth payload for the map. Keyed by FIPS (or ISO-2 for country).
// ============================================================================
[ApiController]
[Authorize]
[Route("api/geo/metrics")]
public class GeoMetricsController : ControllerBase
{
    private readonly IGeoService _geo;
    public GeoMetricsController(IGeoService geo) { _geo = geo; }

    /// <summary>
    /// <c>GET /api/geo/metrics?level=state&amp;parent=US&amp;metric=teams</c> -> one row per region.
    /// <para><c>level</c>: country | state | county.</para>
    /// <para><c>parent</c>: optional ISO-2 (for state queries) or state FIPS (for county queries).</para>
    /// <para><c>metric</c>: regions (default, child counts) | teams | venues. See <see cref="GeoMetricKind"/>.</para>
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Permissions.GeoRead)]
    public async Task<ActionResult<IReadOnlyList<GeoMetricDto>>> Get(
        [FromQuery] GeoMetricsLevel level = GeoMetricsLevel.State,
        [FromQuery] string? parent = null,
        [FromQuery] GeoMetricKind metric = GeoMetricKind.Regions,
        CancellationToken ct = default)
        => Ok(await _geo.GetMetricsAsync(level, parent, metric, ct));
}
