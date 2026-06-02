using System.Collections.ObjectModel;
using Aion.GameServer.World;

namespace Aion.GameServer.Dataholders;

public sealed class InstanceExitTable
{
	private readonly IReadOnlyDictionary<int, IReadOnlyList<InstanceExitSummary>> _exitsByInstanceWorldId;

	public InstanceExitTable(IReadOnlyList<InstanceExitSummary> exits)
	{
		Exits = exits;
		_exitsByInstanceWorldId = new ReadOnlyDictionary<int, IReadOnlyList<InstanceExitSummary>>(
			exits
				.GroupBy(exit => exit.InstanceWorldId)
				.ToDictionary(group => group.Key, group => (IReadOnlyList<InstanceExitSummary>)group.ToArray()));
	}

	public IReadOnlyList<InstanceExitSummary> Exits { get; }

	public int Count => Exits.Count;

	public InstanceExitSummary? GetInstanceExit(int worldId, string race)
	{
		// Java parity: dataholders/InstanceExitData.getInstanceExit returns the first PC_ALL or exact-race exit.
		foreach (var exit in _exitsByInstanceWorldId.GetValueOrDefault(worldId) ?? Array.Empty<InstanceExitSummary>())
		{
			if (string.Equals(exit.Race, "PC_ALL", StringComparison.Ordinal)
				|| string.Equals(exit.Race, race, StringComparison.Ordinal))
			{
				return exit;
			}
		}

		return null;
	}
}

public sealed record InstanceExitSummary(
	int InstanceWorldId,
	int ExitWorldId,
	string Race,
	float X,
	float Y,
	float Z,
	byte Heading)
{
	public WorldPosition ToWorldPosition()
	{
		return new WorldPosition(ExitWorldId, X, Y, Z, Heading);
	}
}
