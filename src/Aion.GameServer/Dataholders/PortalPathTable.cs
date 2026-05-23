using System.Collections.ObjectModel;

namespace Aion.GameServer.Dataholders;

public sealed class PortalPathTable
{
	private readonly IReadOnlyDictionary<int, IReadOnlyList<PortalPathSummary>> _dialogPathsByNpcId;
	private readonly IReadOnlyDictionary<int, int> _teleportDialogIdsByNpcId;
	private readonly IReadOnlyDictionary<int, IReadOnlyList<PortalPathSummary>> _usePathsByNpcId;
	private readonly IReadOnlyDictionary<string, PortalPathSummary> _scrollPathsByName;

	public PortalPathTable(
		IReadOnlyList<PortalPathSummary> dialogPaths,
		IReadOnlyDictionary<int, int> teleportDialogIdsByNpcId,
		IReadOnlyList<PortalPathSummary> usePaths,
		IReadOnlyList<PortalPathSummary> scrollPaths)
	{
		DialogPaths = dialogPaths;
		UsePaths = usePaths;
		ScrollPaths = scrollPaths;
		_teleportDialogIdsByNpcId = new ReadOnlyDictionary<int, int>(
			new Dictionary<int, int>(teleportDialogIdsByNpcId));
		_dialogPathsByNpcId = GroupByNpcId(dialogPaths);
		_usePathsByNpcId = GroupByNpcId(usePaths);
		_scrollPathsByName = new ReadOnlyDictionary<string, PortalPathSummary>(
			scrollPaths
				.Where(path => !string.IsNullOrWhiteSpace(path.ScrollName))
				.ToDictionary(path => path.ScrollName, StringComparer.Ordinal));
	}

	public IReadOnlyList<PortalPathSummary> DialogPaths { get; }

	public IReadOnlyList<PortalPathSummary> UsePaths { get; }

	public IReadOnlyList<PortalPathSummary> ScrollPaths { get; }

	public int Count => DialogCount + UseCount + ScrollCount;

	public int PathCount => DialogPaths.Count + UsePaths.Count + ScrollPaths.Count;

	public int DialogCount => _dialogPathsByNpcId.Count;

	public int UseCount => _usePathsByNpcId.Count;

	public int ScrollCount => _scrollPathsByName.Count;

	public IReadOnlyList<PortalPathSummary> GetDialogPaths(int npcId)
	{
		return _dialogPathsByNpcId.GetValueOrDefault(npcId) ?? Array.Empty<PortalPathSummary>();
	}

	public IReadOnlyList<PortalPathSummary> GetUsePaths(int npcId)
	{
		return _usePathsByNpcId.GetValueOrDefault(npcId) ?? Array.Empty<PortalPathSummary>();
	}

	public PortalPathSummary? GetPortalDialogPath(int npcId, int dialogActionId, string playerRace)
	{
		// Java parity: dataholders/Portal2Data.getPortalDialogPath returns an opposite-race path as a fallback.
		PortalPathSummary? matchingPortalPath = null;
		foreach (var path in GetDialogPaths(npcId))
		{
			if (path.Dialog != dialogActionId)
				continue;

			if (IsRaceMatch(path.Race, playerRace))
				return path;

			matchingPortalPath = path;
		}

		return matchingPortalPath;
	}

	public PortalPathSummary? GetPortalUsePath(int npcId, string playerRace)
	{
		// Java parity: dataholders/Portal2Data.getPortalUsePath mirrors dialog lookup without a dialog action id.
		PortalPathSummary? matchingPortalPath = null;
		foreach (var path in GetUsePaths(npcId))
		{
			if (IsRaceMatch(path.Race, playerRace))
				return path;

			matchingPortalPath = path;
		}

		return matchingPortalPath;
	}

	public bool IsPortalNpc(int npcId)
	{
		return _usePathsByNpcId.ContainsKey(npcId) || _dialogPathsByNpcId.ContainsKey(npcId);
	}

	public PortalPathSummary? GetPortalScroll(string name)
	{
		return _scrollPathsByName.GetValueOrDefault(name);
	}

	public int GetTeleportDialogId(int npcId)
	{
		return _teleportDialogIdsByNpcId.GetValueOrDefault(npcId, 1011);
	}

	private static bool IsRaceMatch(string portalRace, string playerRace)
	{
		return string.Equals(portalRace, "PC_ALL", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(portalRace, playerRace, StringComparison.OrdinalIgnoreCase);
	}

	private static IReadOnlyDictionary<int, IReadOnlyList<PortalPathSummary>> GroupByNpcId(
		IEnumerable<PortalPathSummary> paths)
	{
		return new ReadOnlyDictionary<int, IReadOnlyList<PortalPathSummary>>(
			paths
				.Where(path => path.NpcId != 0)
				.GroupBy(path => path.NpcId)
				.ToDictionary(group => group.Key, group => (IReadOnlyList<PortalPathSummary>)group.ToArray()));
	}
}

public sealed record PortalPathSummary(
	PortalPathSource Source,
	int NpcId,
	string ScrollName,
	int Dialog,
	int LocId,
	int SiegeId,
	string Race,
	int MinLevel,
	int MinRank,
	int Kinah,
	int TitleId,
	int ErrGroup,
	int ErrLevel)
{
	public IReadOnlyList<PortalQuestRequirementSummary> QuestRequirements { get; init; } =
		Array.Empty<PortalQuestRequirementSummary>();

	public IReadOnlyList<PortalItemRequirementSummary> ItemRequirements { get; init; } =
		Array.Empty<PortalItemRequirementSummary>();
}

public sealed record PortalQuestRequirementSummary(int QuestId, int QuestStep);

public sealed record PortalItemRequirementSummary(int ItemId, int ItemCount);

public enum PortalPathSource
{
	Use,
	Dialog,
	Scroll,
}
