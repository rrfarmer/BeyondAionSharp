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
}

public sealed record InstanceCooltimeSummary(int Id, int WorldId, string Race, int MaxCount);
