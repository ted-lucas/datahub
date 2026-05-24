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

    [HttpPut("{id:guid}/geometry")]
    [Authorize(Policy = Permissions.GeoManage)]
    public async Task<IActionResult> SetGeometry(Guid id, [FromBody] SetGeometryRequest req, CancellationToken ct)
        => await _geo.SetCountryGeometryAsync(id, req.GeoJson, ct) ? NoContent() : NotFound();

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

    [HttpPut("{id:guid}/geometry")]
    [Authorize(Policy = Permissions.GeoManage)]
    public async Task<IActionResult> SetGeometry(Guid id, [FromBody] SetGeometryRequest req, CancellationToken ct)
        => await _geo.SetStateGeometryAsync(id, req.GeoJson, ct) ? NoContent() : NotFound();

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

    [HttpPut("{id:guid}/geometry")]
    [Authorize(Policy = Permissions.GeoManage)]
    public async Task<IActionResult> SetGeometry(Guid id, [FromBody] SetGeometryRequest req, CancellationToken ct)
        => await _geo.SetCountyGeometryAsync(id, req.GeoJson, ct) ? NoContent() : NotFound();
}
