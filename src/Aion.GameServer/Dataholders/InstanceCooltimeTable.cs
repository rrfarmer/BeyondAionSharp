using System.Collections.ObjectModel;

namespace Aion.GameServer.Dataholders;

public sealed class InstanceCooltimeTable
{
	private readonly IReadOnlyDictionary<int, InstanceCooltimeSummary> _templatesByWorldId;

	public InstanceCooltimeTable(IReadOnlyList<InstanceCooltimeSummary> templates)
	{
		Templates = templates;
		_templatesByWorldId = new ReadOnlyDictionary<int, InstanceCooltimeSummary>(
			templates.ToDictionary(template => template.WorldId));
	}

	public IReadOnlyList<InstanceCooltimeSummary> Templates { get; }

	public int Count => Templates.Count;

	public InstanceCooltimeSummary? GetInstanceCooltimeByWorldId(int worldId)
	{
		return _templatesByWorldId.GetValueOrDefault(worldId);
	}

	public int GetMaxMemberCount(int worldId, string race)
	{
		// Java parity: InstanceCooltimeData.getMaxMemberCount returns light capacity only for Race.ELYOS.
		var template = GetInstanceCooltimeByWorldId(worldId);
		if (template == null)
			return 0;

		return string.Equals(race, "ELYOS", StringComparison.OrdinalIgnoreCase)
			? template.MaxMemberLight
			: template.MaxMemberDark;
	}
}

public sealed record InstanceCooltimeSummary(
	int Id,
	int WorldId,
	string Race,
	int MaxCount,
	int MaxMemberLight = 0,
	int MaxMemberDark = 0);
