namespace DataHub.Core.Entities.Sports;

/// <summary>
/// A conference or division within a League. Self-referencing so conferences can contain
/// sub-divisions (e.g., MLB: National League → NL Central).
/// </summary>
public class Conference : AuditableEntity
{
    public Guid LeagueId { get; set; }
    public League? League { get; set; }

    public Guid? ParentConferenceId { get; set; }
    public Conference? ParentConference { get; set; }

    public string Name { get; set; } = string.Empty;

    public ICollection<Conference> ChildConferences { get; set; } = new List<Conference>();
    public ICollection<Team> Teams { get; set; } = new List<Team>();
}
