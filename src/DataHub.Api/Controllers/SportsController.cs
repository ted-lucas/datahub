using DataHub.Core.Constants;
using DataHub.Core.DTOs.Sports;
using DataHub.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DataHub.Api.Controllers;

[ApiController]
[Route("api/sports")]
[Authorize]
public class SportsController : ControllerBase
{
    private readonly ISportsService _svc;
    public SportsController(ISportsService svc) => _svc = svc;

    [HttpGet]
    [Authorize(Policy = Permissions.SportsRead)]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false, CancellationToken ct = default)
        => Ok(await _svc.GetSportsAsync(includeInactive, ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.SportsRead)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var s = await _svc.GetSportAsync(id, ct);
        return s is null ? NotFound() : Ok(s);
    }

    [HttpPost]
    [Authorize(Policy = Permissions.SportsManage)]
    public async Task<IActionResult> Create([FromBody] CreateSportRequest req, CancellationToken ct)
    {
        var dto = await _svc.CreateSportAsync(req, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.SportsManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSportRequest req, CancellationToken ct)
    {
        var dto = await _svc.UpdateSportAsync(id, req, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.SportsManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => (await _svc.DeleteSportAsync(id, ct)) ? NoContent() : NotFound();

    // ---- Levels nested under a Sport ----

    [HttpGet("{sportId:guid}/levels")]
    [Authorize(Policy = Permissions.SportsRead)]
    public async Task<IActionResult> GetLevels(Guid sportId, [FromQuery] bool includeInactive = false, CancellationToken ct = default)
        => Ok(await _svc.GetLevelsAsync(sportId, includeInactive, ct));

    [HttpPost("{sportId:guid}/levels")]
    [Authorize(Policy = Permissions.SportsManage)]
    public async Task<IActionResult> CreateLevel(Guid sportId, [FromBody] CreateSportLevelRequest req, CancellationToken ct)
    {
        var dto = await _svc.CreateLevelAsync(sportId, req, ct);
        return dto is null ? NotFound(new { error = "Sport not found" }) : CreatedAtAction(nameof(SportLevelsController.Get), "SportLevels", new { id = dto.Id }, dto);
    }
}

[ApiController]
[Route("api/sport-levels")]
[Authorize]
public class SportLevelsController : ControllerBase
{
    private readonly ISportsService _svc;
    public SportLevelsController(ISportsService svc) => _svc = svc;

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.SportsRead)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var l = await _svc.GetLevelAsync(id, ct);
        return l is null ? NotFound() : Ok(l);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.SportsManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSportLevelRequest req, CancellationToken ct)
    {
        var l = await _svc.UpdateLevelAsync(id, req, ct);
        return l is null ? NotFound() : Ok(l);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.SportsManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => (await _svc.DeleteLevelAsync(id, ct)) ? NoContent() : NotFound();

    [HttpGet("{sportLevelId:guid}/leagues")]
    [Authorize(Policy = Permissions.SportsRead)]
    public async Task<IActionResult> GetLeagues(Guid sportLevelId, [FromQuery] bool includeInactive = false, CancellationToken ct = default)
        => Ok(await _svc.GetLeaguesAsync(sportLevelId, includeInactive, ct));

    [HttpPost("{sportLevelId:guid}/leagues")]
    [Authorize(Policy = Permissions.SportsManage)]
    public async Task<IActionResult> CreateLeague(Guid sportLevelId, [FromBody] CreateLeagueRequest req, CancellationToken ct)
    {
        var dto = await _svc.CreateLeagueAsync(sportLevelId, req, ct);
        return dto is null
            ? NotFound(new { error = "SportLevel not found" })
            : CreatedAtAction(nameof(LeaguesController.Get), "Leagues", new { id = dto.Id }, dto);
    }
}

[ApiController]
[Route("api/leagues")]
[Authorize]
public class LeaguesController : ControllerBase
{
    private readonly ISportsService _svc;
    public LeaguesController(ISportsService svc) => _svc = svc;

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.SportsRead)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var l = await _svc.GetLeagueAsync(id, ct);
        return l is null ? NotFound() : Ok(l);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.SportsManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLeagueRequest req, CancellationToken ct)
    {
        var l = await _svc.UpdateLeagueAsync(id, req, ct);
        return l is null ? NotFound() : Ok(l);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.SportsManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => (await _svc.DeleteLeagueAsync(id, ct)) ? NoContent() : NotFound();

    // Conferences nested under a League

    [HttpGet("{leagueId:guid}/conferences")]
    [Authorize(Policy = Permissions.SportsRead)]
    public async Task<IActionResult> GetConferences(Guid leagueId, [FromQuery] bool includeInactive = false, CancellationToken ct = default)
        => Ok(await _svc.GetConferencesAsync(leagueId, includeInactive, ct));

    [HttpPost("{leagueId:guid}/conferences")]
    [Authorize(Policy = Permissions.SportsManage)]
    public async Task<IActionResult> CreateConference(Guid leagueId, [FromBody] CreateConferenceRequest req, CancellationToken ct)
    {
        var dto = await _svc.CreateConferenceAsync(leagueId, req, ct);
        return dto is null
            ? NotFound(new { error = "League not found" })
            : CreatedAtAction(nameof(ConferencesController.Get), "Conferences", new { id = dto.Id }, dto);
    }

    // Teams nested under a League

    [HttpGet("{leagueId:guid}/teams")]
    [Authorize(Policy = Permissions.SportsRead)]
    public async Task<IActionResult> GetTeams(Guid leagueId, [FromQuery] string? state = null, [FromQuery] bool includeInactive = false, CancellationToken ct = default)
        => Ok(await _svc.GetTeamsAsync(leagueId, state, includeInactive, ct: ct));

    [HttpPost("{leagueId:guid}/teams")]
    [Authorize(Policy = Permissions.SportsManage)]
    public async Task<IActionResult> CreateTeam(Guid leagueId, [FromBody] CreateTeamRequest req, CancellationToken ct)
    {
        var dto = await _svc.CreateTeamAsync(leagueId, req, ct);
        return dto is null
            ? NotFound(new { error = "League not found" })
            : CreatedAtAction(nameof(TeamsController.Get), "Teams", new { id = dto.Id }, dto);
    }
}

[ApiController]
[Route("api/conferences")]
[Authorize]
public class ConferencesController : ControllerBase
{
    private readonly ISportsService _svc;
    public ConferencesController(ISportsService svc) => _svc = svc;

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.SportsRead)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var c = await _svc.GetConferenceAsync(id, ct);
        return c is null ? NotFound() : Ok(c);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.SportsManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateConferenceRequest req, CancellationToken ct)
    {
        var c = await _svc.UpdateConferenceAsync(id, req, ct);
        return c is null ? NotFound() : Ok(c);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.SportsManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => (await _svc.DeleteConferenceAsync(id, ct)) ? NoContent() : NotFound();
}

[ApiController]
[Route("api/teams")]
[Authorize]
public class TeamsController : ControllerBase
{
    private readonly ISportsService _svc;
    public TeamsController(ISportsService svc) => _svc = svc;

    [HttpGet]
    [Authorize(Policy = Permissions.SportsRead)]
    public async Task<IActionResult> Query(
        [FromQuery] Guid? leagueId = null,
        [FromQuery] string? state = null,
        [FromQuery] bool includeInactive = false,
        [FromQuery] long? from = null,
        [FromQuery] long? to = null,
        [FromQuery] string? g = null,
        CancellationToken ct = default)
    {
        // Time window (epoch-ms, UTC) → calendar years for the active-during filter.
        // We ignore `g` server-side: year resolution is sufficient because Team only
        // tracks Founded/Closed at year grain. Frontend keeps `g` for URL fidelity.
        _ = g;
        int? activeFromYear = from.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(from.Value).UtcDateTime.Year : null;
        int? activeToYear = to.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(to.Value).UtcDateTime.Year : null;
        return Ok(await _svc.GetTeamsAsync(leagueId, state, includeInactive, activeFromYear, activeToYear, ct));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.SportsRead)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var t = await _svc.GetTeamAsync(id, ct);
        return t is null ? NotFound() : Ok(t);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.SportsManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTeamRequest req, CancellationToken ct)
    {
        var t = await _svc.UpdateTeamAsync(id, req, ct);
        return t is null ? NotFound() : Ok(t);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.SportsManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => (await _svc.DeleteTeamAsync(id, ct)) ? NoContent() : NotFound();
}

[ApiController]
[Route("api/venues")]
[Authorize]
public class VenuesController : ControllerBase
{
    private readonly ISportsService _svc;
    public VenuesController(ISportsService svc) => _svc = svc;

    [HttpGet]
    [Authorize(Policy = Permissions.SportsRead)]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false, CancellationToken ct = default)
        => Ok(await _svc.GetVenuesAsync(includeInactive, ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.SportsRead)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var v = await _svc.GetVenueAsync(id, ct);
        return v is null ? NotFound() : Ok(v);
    }

    [HttpPost]
    [Authorize(Policy = Permissions.SportsManage)]
    public async Task<IActionResult> Create([FromBody] CreateVenueRequest req, CancellationToken ct)
    {
        var dto = await _svc.CreateVenueAsync(req, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.SportsManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateVenueRequest req, CancellationToken ct)
    {
        var v = await _svc.UpdateVenueAsync(id, req, ct);
        return v is null ? NotFound() : Ok(v);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.SportsManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => (await _svc.DeleteVenueAsync(id, ct)) ? NoContent() : NotFound();
}
