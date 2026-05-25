using System.Collections.ObjectModel;

namespace Aion.GameServer.Dataholders;

public sealed class NpcFactionTable
{
	private readonly IReadOnlyDictionary<int, NpcFactionSummary> _factionsById;
	private readonly IReadOnlyDictionary<int, NpcFactionSummary> _factionsByNpcId;

	public NpcFactionTable(IReadOnlyList<NpcFactionSummary> factions)
	{
		// Java parity: dataholders/NpcFactionsData.afterUnmarshal indexes by faction id and registrar NPC id.
		Factions = factions;
		_factionsById = new ReadOnlyDictionary<int, NpcFactionSummary>(
			factions.ToDictionary(faction => faction.FactionId));
		_factionsByNpcId = new ReadOnlyDictionary<int, NpcFactionSummary>(
			factions
				.SelectMany(faction => faction.NpcIds.Select(npcId => new { npcId, faction }))
				.ToDictionary(entry => entry.npcId, entry => entry.faction));
	}

	public IReadOnlyList<NpcFactionSummary> Factions { get; }

	public int Count => Factions.Count;

	public NpcFactionSummary? GetNpcFactionById(int factionId)
	{
		return _factionsById.GetValueOrDefault(factionId);
	}

	public NpcFactionSummary? GetNpcFactionByNpcId(int npcId)
	{
		return _factionsByNpcId.GetValueOrDefault(npcId);
	}

	public bool IsMentorFaction(int factionId)
	{
		return GetNpcFactionById(factionId)?.IsMentor ?? false;
	}
}

// Java parity: model/templates/factions/NpcFactionTemplate.
public sealed record NpcFactionSummary(
	int FactionId,
	string Name,
	int NameId,
	string Category,
	int MinLevel,
	int MaxLevel,
	string Race,
	IReadOnlyList<int> NpcIds,
	int SkillPoints)
{
	public bool IsMentor => string.Equals(Category, "MENTOR", StringComparison.Ordinal);
}
