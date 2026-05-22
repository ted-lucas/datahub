namespace DataHub.Core.Entities.Sports;

/// <summary>
/// Time-scoped facts about a Team for a given year (or arbitrary interval).
/// Carries the bitemporal window so we can render historical truth.
/// </summary>
public class TeamSeason : AuditableEntity
{
    public Guid TeamId { get; set; }
    public Team? Team { get; set; }

    public int Year { get; set; }

    public Guid LeagueId { get; set; }
    public League? League { get; set; }

    public Guid? ConferenceId { get; set; }
    public Conference? Conference { get; set; }

    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveTo { get; set; }
    public string? Notes { get; set; }
}
