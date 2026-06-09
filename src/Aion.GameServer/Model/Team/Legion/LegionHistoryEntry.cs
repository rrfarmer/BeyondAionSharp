namespace Aion.GameServer.Model.Team.Legion;

/// <summary>Java parity: model/team/legion/LegionHistoryEntry (record; interns name/description).</summary>
public record LegionHistoryEntry
{
    public int Id { get; }
    public int EpochSeconds { get; }
    public LegionHistoryAction Action { get; }
    public string Name { get; }
    public string Description { get; }

    public LegionHistoryEntry(int id, int epochSeconds, LegionHistoryAction action, string name, string description)
    {
        this.Id = id;
        this.EpochSeconds = epochSeconds;
        this.Action = action;
        this.Name = string.Intern(name);
        this.Description = string.Intern(description);
    }
}
