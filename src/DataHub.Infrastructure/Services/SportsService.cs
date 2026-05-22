using DataHub.Core.DTOs.Sports;
using DataHub.Core.Entities.Sports;
using DataHub.Core.Interfaces;
using DataHub.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DataHub.Infrastructure.Services;

public class SportsService : ISportsService
{
    private readonly DataHubDbContext _db;

    public SportsService(DataHubDbContext db) => _db = db;

    // ---------------- Sports ----------------

    public async Task<IEnumerable<SportDto>> GetSportsAsync(bool includeInactive, CancellationToken ct = default)
    {
        var q = _db.Sports.AsNoTracking().AsQueryable();
        if (!includeInactive) q = q.Where(s => s.IsActive);
        return await q.OrderBy(s => s.SortOrder).ThenBy(s => s.Name)
            .Select(s => new SportDto(s.Id, s.Name, s.Slug, s.IconRef, s.SortOrder, s.IsActive))
            .ToListAsync(ct);
    }

    public async Task<SportDto?> GetSportAsync(Guid id, CancellationToken ct = default)
    {
        var s = await _db.Sports.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return s is null ? null : new SportDto(s.Id, s.Name, s.Slug, s.IconRef, s.SortOrder, s.IsActive);
    }

    public async Task<SportDto> CreateSportAsync(CreateSportRequest req, CancellationToken ct = default)
    {
        var s = new Sport
        {
            Name = req.Name,
            Slug = req.Slug,
            IconRef = req.IconRef,
            SortOrder = req.SortOrder,
        };
        _db.Sports.Add(s);
        await _db.SaveChangesAsync(ct);
        return new SportDto(s.Id, s.Name, s.Slug, s.IconRef, s.SortOrder, s.IsActive);
    }

    public async Task<SportDto?> UpdateSportAsync(Guid id, UpdateSportRequest req, CancellationToken ct = default)
    {
        var s = await _db.Sports.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return null;
        s.Name = req.Name;
        s.Slug = req.Slug;
        s.IconRef = req.IconRef;
        s.SortOrder = req.SortOrder;
        s.IsActive = req.IsActive;
        await _db.SaveChangesAsync(ct);
        return new SportDto(s.Id, s.Name, s.Slug, s.IconRef, s.SortOrder, s.IsActive);
    }

    public async Task<bool> DeleteSportAsync(Guid id, CancellationToken ct = default)
    {
        var s = await _db.Sports.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return false;
        // Soft delete: mark inactive instead of removing rows (preserves history).
        s.IsActive = false;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ---------------- SportLevels ----------------

    public async Task<IEnumerable<SportLevelDto>> GetLevelsAsync(Guid sportId, bool includeInactive, CancellationToken ct = default)
    {
        var q = _db.SportLevels.AsNoTracking().Where(l => l.SportId == sportId);
        if (!includeInactive) q = q.Where(l => l.IsActive);
        return await q.OrderBy(l => l.SortOrder).ThenBy(l => l.Name)
            .Select(l => new SportLevelDto(l.Id, l.SportId, l.Name, l.SortOrder, l.IsActive))
            .ToListAsync(ct);
    }

    public async Task<SportLevelDto?> GetLevelAsync(Guid id, CancellationToken ct = default)
    {
        var l = await _db.SportLevels.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return l is null ? null : new SportLevelDto(l.Id, l.SportId, l.Name, l.SortOrder, l.IsActive);
    }

    public async Task<SportLevelDto?> CreateLevelAsync(Guid sportId, CreateSportLevelRequest req, CancellationToken ct = default)
    {
        if (!await _db.Sports.AnyAsync(s => s.Id == sportId, ct)) return null;
        var l = new SportLevel { SportId = sportId, Name = req.Name, SortOrder = req.SortOrder };
        _db.SportLevels.Add(l);
        await _db.SaveChangesAsync(ct);
        return new SportLevelDto(l.Id, l.SportId, l.Name, l.SortOrder, l.IsActive);
    }

    public async Task<SportLevelDto?> UpdateLevelAsync(Guid id, UpdateSportLevelRequest req, CancellationToken ct = default)
    {
        var l = await _db.SportLevels.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (l is null) return null;
        l.Name = req.Name;
        l.SortOrder = req.SortOrder;
        l.IsActive = req.IsActive;
        await _db.SaveChangesAsync(ct);
        return new SportLevelDto(l.Id, l.SportId, l.Name, l.SortOrder, l.IsActive);
    }

    public async Task<bool> DeleteLevelAsync(Guid id, CancellationToken ct = default)
    {
        var l = await _db.SportLevels.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (l is null) return false;
        l.IsActive = false;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ---------------- Leagues ----------------

    public async Task<IEnumerable<LeagueDto>> GetLeaguesAsync(Guid sportLevelId, bool includeInactive, CancellationToken ct = default)
    {
        var q = _db.Leagues.AsNoTracking().Where(l => l.SportLevelId == sportLevelId);
        if (!includeInactive) q = q.Where(l => l.IsActive);
        return await q.OrderBy(l => l.Name)
            .Select(l => new LeagueDto(l.Id, l.SportLevelId, l.Name, l.Abbreviation, l.Country, l.FoundedYear, l.IsActive))
            .ToListAsync(ct);
    }

    public async Task<LeagueDto?> GetLeagueAsync(Guid id, CancellationToken ct = default)
    {
        var l = await _db.Leagues.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return l is null ? null : new LeagueDto(l.Id, l.SportLevelId, l.Name, l.Abbreviation, l.Country, l.FoundedYear, l.IsActive);
    }

    public async Task<LeagueDto?> CreateLeagueAsync(Guid sportLevelId, CreateLeagueRequest req, CancellationToken ct = default)
    {
        if (!await _db.SportLevels.AnyAsync(sl => sl.Id == sportLevelId, ct)) return null;
        var l = new League
        {
            SportLevelId = sportLevelId,
            Name = req.Name,
            Abbreviation = req.Abbreviation,
            Country = req.Country,
            FoundedYear = req.FoundedYear,
        };
        _db.Leagues.Add(l);
        await _db.SaveChangesAsync(ct);
        return new LeagueDto(l.Id, l.SportLevelId, l.Name, l.Abbreviation, l.Country, l.FoundedYear, l.IsActive);
    }

    public async Task<LeagueDto?> UpdateLeagueAsync(Guid id, UpdateLeagueRequest req, CancellationToken ct = default)
    {
        var l = await _db.Leagues.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (l is null) return null;
        l.Name = req.Name;
        l.Abbreviation = req.Abbreviation;
        l.Country = req.Country;
        l.FoundedYear = req.FoundedYear;
        l.IsActive = req.IsActive;
        await _db.SaveChangesAsync(ct);
        return new LeagueDto(l.Id, l.SportLevelId, l.Name, l.Abbreviation, l.Country, l.FoundedYear, l.IsActive);
    }

    public async Task<bool> DeleteLeagueAsync(Guid id, CancellationToken ct = default)
    {
        var l = await _db.Leagues.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (l is null) return false;
        l.IsActive = false;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ---------------- Conferences ----------------

    public async Task<IEnumerable<ConferenceDto>> GetConferencesAsync(Guid leagueId, bool includeInactive, CancellationToken ct = default)
    {
        var q = _db.Conferences.AsNoTracking().Where(c => c.LeagueId == leagueId);
        if (!includeInactive) q = q.Where(c => c.IsActive);
        return await q.OrderBy(c => c.Name)
            .Select(c => new ConferenceDto(c.Id, c.LeagueId, c.ParentConferenceId, c.Name, c.IsActive))
            .ToListAsync(ct);
    }

    public async Task<ConferenceDto?> GetConferenceAsync(Guid id, CancellationToken ct = default)
    {
        var c = await _db.Conferences.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return c is null ? null : new ConferenceDto(c.Id, c.LeagueId, c.ParentConferenceId, c.Name, c.IsActive);
    }

    public async Task<ConferenceDto?> CreateConferenceAsync(Guid leagueId, CreateConferenceRequest req, CancellationToken ct = default)
    {
        if (!await _db.Leagues.AnyAsync(l => l.Id == leagueId, ct)) return null;
        var c = new Conference
        {
            LeagueId = leagueId,
            Name = req.Name,
            ParentConferenceId = req.ParentConferenceId,
        };
        _db.Conferences.Add(c);
        await _db.SaveChangesAsync(ct);
        return new ConferenceDto(c.Id, c.LeagueId, c.ParentConferenceId, c.Name, c.IsActive);
    }

    public async Task<ConferenceDto?> UpdateConferenceAsync(Guid id, UpdateConferenceRequest req, CancellationToken ct = default)
    {
        var c = await _db.Conferences.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return null;
        c.Name = req.Name;
        c.ParentConferenceId = req.ParentConferenceId;
        c.IsActive = req.IsActive;
        await _db.SaveChangesAsync(ct);
        return new ConferenceDto(c.Id, c.LeagueId, c.ParentConferenceId, c.Name, c.IsActive);
    }

    public async Task<bool> DeleteConferenceAsync(Guid id, CancellationToken ct = default)
    {
        var c = await _db.Conferences.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return false;
        c.IsActive = false;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ---------------- Venues ----------------

    public async Task<IEnumerable<VenueDto>> GetVenuesAsync(bool includeInactive, CancellationToken ct = default)
    {
        var q = _db.Venues.AsNoTracking().AsQueryable();
        if (!includeInactive) q = q.Where(v => v.IsActive);
        return await q.OrderBy(v => v.Name).Select(VenueProjection).ToListAsync(ct);
    }

    public async Task<VenueDto?> GetVenueAsync(Guid id, CancellationToken ct = default)
    {
        var v = await _db.Venues.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return v is null ? null : ToDto(v);
    }

    public async Task<VenueDto> CreateVenueAsync(CreateVenueRequest req, CancellationToken ct = default)
    {
        var v = new Venue
        {
            Name = req.Name,
            Address = req.Address,
            City = req.City,
            State = req.State,
            Country = req.Country,
            Lat = req.Lat,
            Lon = req.Lon,
            Capacity = req.Capacity,
            OpenedYear = req.OpenedYear,
            ClosedYear = req.ClosedYear,
        };
        _db.Venues.Add(v);
        await _db.SaveChangesAsync(ct);
        return ToDto(v);
    }

    public async Task<VenueDto?> UpdateVenueAsync(Guid id, UpdateVenueRequest req, CancellationToken ct = default)
    {
        var v = await _db.Venues.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (v is null) return null;
        v.Name = req.Name;
        v.Address = req.Address;
        v.City = req.City;
        v.State = req.State;
        v.Country = req.Country;
        v.Lat = req.Lat;
        v.Lon = req.Lon;
        v.Capacity = req.Capacity;
        v.OpenedYear = req.OpenedYear;
        v.ClosedYear = req.ClosedYear;
        v.IsActive = req.IsActive;
        await _db.SaveChangesAsync(ct);
        return ToDto(v);
    }

    public async Task<bool> DeleteVenueAsync(Guid id, CancellationToken ct = default)
    {
        var v = await _db.Venues.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (v is null) return false;
        v.IsActive = false;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static readonly System.Linq.Expressions.Expression<Func<Venue, VenueDto>> VenueProjection = v =>
        new VenueDto(v.Id, v.Name, v.Address, v.City, v.State, v.Country, v.Lat, v.Lon, v.Capacity, v.OpenedYear, v.ClosedYear, v.IsActive);

    private static VenueDto ToDto(Venue v) =>
        new(v.Id, v.Name, v.Address, v.City, v.State, v.Country, v.Lat, v.Lon, v.Capacity, v.OpenedYear, v.ClosedYear, v.IsActive);

    // ---------------- Teams ----------------

    public async Task<IEnumerable<TeamDto>> GetTeamsAsync(Guid? leagueId, string? state, bool includeInactive, CancellationToken ct = default)
    {
        var q = _db.Teams.AsNoTracking().AsQueryable();
        if (leagueId.HasValue) q = q.Where(t => t.LeagueId == leagueId.Value);
        if (!string.IsNullOrWhiteSpace(state)) q = q.Where(t => t.State == state);
        if (!includeInactive) q = q.Where(t => t.IsActive);
        return await q.OrderBy(t => t.Name).Select(t => ToDtoExpr(t)).ToListAsync(ct);
    }

    public async Task<TeamDto?> GetTeamAsync(Guid id, CancellationToken ct = default)
    {
        var t = await _db.Teams.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return t is null ? null : ToDtoMaterialized(t);
    }

    public async Task<TeamDto?> CreateTeamAsync(Guid leagueId, CreateTeamRequest req, CancellationToken ct = default)
    {
        if (!await _db.Leagues.AnyAsync(l => l.Id == leagueId, ct)) return null;
        var t = new Team
        {
            LeagueId = leagueId,
            ConferenceId = req.ConferenceId,
            VenueId = req.VenueId,
            Name = req.Name,
            City = req.City,
            State = req.State,
            Country = req.Country,
            FoundedYear = req.FoundedYear,
            PrimaryColor = req.PrimaryColor,
            SecondaryColor = req.SecondaryColor,
            LogoRef = req.LogoRef,
        };
        _db.Teams.Add(t);
        await _db.SaveChangesAsync(ct);
        return ToDtoMaterialized(t);
    }

    public async Task<TeamDto?> UpdateTeamAsync(Guid id, UpdateTeamRequest req, CancellationToken ct = default)
    {
        var t = await _db.Teams.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return null;
        t.Name = req.Name;
        t.ConferenceId = req.ConferenceId;
        t.VenueId = req.VenueId;
        t.City = req.City;
        t.State = req.State;
        t.Country = req.Country;
        t.FoundedYear = req.FoundedYear;
        t.PrimaryColor = req.PrimaryColor;
        t.SecondaryColor = req.SecondaryColor;
        t.LogoRef = req.LogoRef;
        t.IsActive = req.IsActive;
        await _db.SaveChangesAsync(ct);
        return ToDtoMaterialized(t);
    }

    public async Task<bool> DeleteTeamAsync(Guid id, CancellationToken ct = default)
    {
        var t = await _db.Teams.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return false;
        t.IsActive = false;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static TeamDto ToDtoExpr(Team t) =>
        new(t.Id, t.LeagueId, t.ConferenceId, t.VenueId, t.Name, t.City, t.State, t.Country,
            t.FoundedYear, t.PrimaryColor, t.SecondaryColor, t.LogoRef, t.IsActive);

    private static TeamDto ToDtoMaterialized(Team t) => ToDtoExpr(t);
}
