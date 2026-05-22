using DataHub.Core.Entities.Sports;
using DataHub.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DataHub.Infrastructure.Seeding;

/// <summary>
/// Idempotent seeder for the Sports module. Seeds Baseball → Professional → MLB
/// and the 30 current MLB teams. Safe to run on every startup; existing rows are not modified.
/// </summary>
public static class SportsSeeder
{
    private const string SourceTag = "seed:mlb-initial";

    public static async Task SeedAsync(DataHubDbContext db, CancellationToken ct = default)
    {
        // ----- Sport: Baseball -----
        var baseball = await db.Sports.FirstOrDefaultAsync(s => s.Slug == "baseball", ct);
        if (baseball is null)
        {
            baseball = new Sport
            {
                Name = "Baseball",
                Slug = "baseball",
                SortOrder = 10,
                Source = SourceTag,
            };
            db.Sports.Add(baseball);
            await db.SaveChangesAsync(ct);
        }

        // ----- Level: Professional -----
        var pro = await db.SportLevels.FirstOrDefaultAsync(l => l.SportId == baseball.Id && l.Name == "Professional", ct);
        if (pro is null)
        {
            pro = new SportLevel
            {
                SportId = baseball.Id,
                Name = "Professional",
                SortOrder = 10,
                Source = SourceTag,
            };
            db.SportLevels.Add(pro);
            await db.SaveChangesAsync(ct);
        }

        // ----- League: MLB -----
        var mlb = await db.Leagues.FirstOrDefaultAsync(l => l.SportLevelId == pro.Id && l.Name == "Major League Baseball", ct);
        if (mlb is null)
        {
            mlb = new League
            {
                SportLevelId = pro.Id,
                Name = "Major League Baseball",
                Abbreviation = "MLB",
                Country = "USA",
                FoundedYear = 1903,
                Source = SourceTag,
            };
            db.Leagues.Add(mlb);
            await db.SaveChangesAsync(ct);
        }

        // ----- Conferences: AL / NL -----
        var al = await EnsureConference(db, mlb.Id, "American League", null, ct);
        var nl = await EnsureConference(db, mlb.Id, "National League", null, ct);

        // Divisions
        var alEast = await EnsureConference(db, mlb.Id, "AL East", al.Id, ct);
        var alCentral = await EnsureConference(db, mlb.Id, "AL Central", al.Id, ct);
        var alWest = await EnsureConference(db, mlb.Id, "AL West", al.Id, ct);
        var nlEast = await EnsureConference(db, mlb.Id, "NL East", nl.Id, ct);
        var nlCentral = await EnsureConference(db, mlb.Id, "NL Central", nl.Id, ct);
        var nlWest = await EnsureConference(db, mlb.Id, "NL West", nl.Id, ct);

        // ----- Teams (30 current MLB teams) -----
        var seedTeams = new (string Name, string City, string State, Guid ConferenceId, int Founded)[]
        {
            // AL East
            ("Orioles", "Baltimore", "MD", alEast.Id, 1901),
            ("Red Sox", "Boston", "MA", alEast.Id, 1901),
            ("Yankees", "New York", "NY", alEast.Id, 1901),
            ("Rays", "St. Petersburg", "FL", alEast.Id, 1998),
            ("Blue Jays", "Toronto", "ON", alEast.Id, 1977),

            // AL Central
            ("White Sox", "Chicago", "IL", alCentral.Id, 1901),
            ("Guardians", "Cleveland", "OH", alCentral.Id, 1901),
            ("Tigers", "Detroit", "MI", alCentral.Id, 1901),
            ("Royals", "Kansas City", "MO", alCentral.Id, 1969),
            ("Twins", "Minneapolis", "MN", alCentral.Id, 1901),

            // AL West
            ("Astros", "Houston", "TX", alWest.Id, 1962),
            ("Angels", "Anaheim", "CA", alWest.Id, 1961),
            ("Athletics", "Sacramento", "CA", alWest.Id, 1901),
            ("Mariners", "Seattle", "WA", alWest.Id, 1977),
            ("Rangers", "Arlington", "TX", alWest.Id, 1961),

            // NL East
            ("Braves", "Atlanta", "GA", nlEast.Id, 1871),
            ("Marlins", "Miami", "FL", nlEast.Id, 1993),
            ("Mets", "New York", "NY", nlEast.Id, 1962),
            ("Phillies", "Philadelphia", "PA", nlEast.Id, 1883),
            ("Nationals", "Washington", "DC", nlEast.Id, 1969),

            // NL Central
            ("Cubs", "Chicago", "IL", nlCentral.Id, 1876),
            ("Reds", "Cincinnati", "OH", nlCentral.Id, 1882),
            ("Brewers", "Milwaukee", "WI", nlCentral.Id, 1969),
            ("Pirates", "Pittsburgh", "PA", nlCentral.Id, 1882),
            ("Cardinals", "St. Louis", "MO", nlCentral.Id, 1882),

            // NL West
            ("Diamondbacks", "Phoenix", "AZ", nlWest.Id, 1998),
            ("Rockies", "Denver", "CO", nlWest.Id, 1993),
            ("Dodgers", "Los Angeles", "CA", nlWest.Id, 1883),
            ("Padres", "San Diego", "CA", nlWest.Id, 1969),
            ("Giants", "San Francisco", "CA", nlWest.Id, 1883),
        };

        var existingTeamNames = await db.Teams
            .Where(t => t.LeagueId == mlb.Id)
            .Select(t => t.Name)
            .ToListAsync(ct);

        foreach (var t in seedTeams)
        {
            if (existingTeamNames.Contains(t.Name)) continue;
            db.Teams.Add(new Team
            {
                LeagueId = mlb.Id,
                ConferenceId = t.ConferenceId,
                Name = t.Name,
                City = t.City,
                State = t.State,
                Country = "USA",
                FoundedYear = t.Founded,
                Source = SourceTag,
            });
        }
        await db.SaveChangesAsync(ct);
    }

    private static async Task<Conference> EnsureConference(
        DataHubDbContext db, Guid leagueId, string name, Guid? parentId, CancellationToken ct)
    {
        var existing = await db.Conferences
            .FirstOrDefaultAsync(c => c.LeagueId == leagueId && c.Name == name, ct);
        if (existing is not null) return existing;

        var conf = new Conference
        {
            LeagueId = leagueId,
            Name = name,
            ParentConferenceId = parentId,
            Source = SourceTag,
        };
        db.Conferences.Add(conf);
        await db.SaveChangesAsync(ct);
        return conf;
    }
}
