namespace Aion.GameServer.Dataholders;

public sealed class AutoGroupTable
{
	private readonly IReadOnlyDictionary<int, AutoGroupSummary> _autoGroupsByMaskId;
	private readonly IReadOnlyDictionary<int, IReadOnlyList<int>> _recruitableInstanceMaskIdsByPortalNpcId;
	private readonly IReadOnlyList<int> _recruitableInstanceMaskIds;
	private static readonly int[] JavaAutoGroupTypeMaskOrder =
	[
		1,
		2,
		3,
		21,
		22,
		23,
		24,
		25,
		26,
		27,
		28,
		29,
		30,
		31,
		32,
		33,
		34,
		35,
		38,
		39,
		40,
		41,
		42,
		43,
		44,
		45,
		101,
		102,
		103,
		107,
		108,
		109,
		111,
	];

	public AutoGroupTable(IEnumerable<AutoGroupSummary> autoGroups)
	{
		// Java parity: dataholders/AutoGroupData.afterUnmarshal indexes auto_group entries
		// by mask id and portal NPC ids, then clears the JAXB source list.
		var summaries = autoGroups.ToArray();
		_autoGroupsByMaskId = summaries
			.GroupBy(autoGroup => autoGroup.MaskId)
			.ToDictionary(group => group.Key, group => group.Last());

		var byPortalNpcId = new Dictionary<int, List<int>>();
		foreach (var autoGroup in summaries.Where(autoGroup => autoGroup.IsRecruitableInstance))
		{
			foreach (var npcId in autoGroup.NpcIds)
			{
				if (!byPortalNpcId.TryGetValue(npcId, out var maskIds))
				{
					maskIds = [];
					byPortalNpcId[npcId] = maskIds;
				}

				maskIds.Add(autoGroup.MaskId);
			}
		}

		_recruitableInstanceMaskIdsByPortalNpcId = byPortalNpcId.ToDictionary(
			pair => pair.Key,
			pair => (IReadOnlyList<int>)pair.Value.AsReadOnly());
		_recruitableInstanceMaskIds = byPortalNpcId.Values
			.SelectMany(maskIds => maskIds)
			.Distinct()
			.ToArray();
	}

	public int Count => _autoGroupsByMaskId.Count;

	public AutoGroupSummary? GetTemplateByInstanceMaskId(int maskId)
	{
		return _autoGroupsByMaskId.TryGetValue(maskId, out var autoGroup) ? autoGroup : null;
	}

	public AutoGroupSummary? GetAutoGroupForNpc(int playerLevel, int portalNpcId)
	{
		// Java parity: model/autogroup/AutoGroupType.getAutoGroup(level, npcId)
		// iterates enum constants in declaration order.
		foreach (var maskId in JavaAutoGroupTypeMaskOrder)
		{
			if (_autoGroupsByMaskId.TryGetValue(maskId, out var autoGroup)
				&& playerLevel >= autoGroup.MinLevel
				&& playerLevel <= autoGroup.MaxLevel
				&& autoGroup.NpcIds.Contains(portalNpcId))
			{
				return autoGroup;
			}
		}

		return null;
	}

	public IReadOnlyList<int>? GetRecruitableInstanceMaskIds(int portalNpcId)
	{
		return _recruitableInstanceMaskIdsByPortalNpcId.TryGetValue(portalNpcId, out var maskIds) ? maskIds : null;
	}

	public IReadOnlyList<int> GetRecruitableInstanceMaskIds()
	{
		return _recruitableInstanceMaskIds;
	}
}

public sealed record AutoGroupSummary(
	int MaskId,
	int InstanceMapId,
	int NameId,
	int TitleId,
	int MinLevel,
	int MaxLevel,
	bool RegisterQuick,
	bool RegisterGroup,
	bool RegisterNew,
	IReadOnlyList<int> NpcIds)
{
	public bool IsRecruitableInstance => MaskId >= 302 && MaskId < 400
		|| InstanceMapId is 300600000 or 300220000;

	public bool IsPeriodicInstance => MaskId is 1 or 2 or 3 or 107 or 108 or 109 or 111;

	public bool IsHarmonyArena => MaskId is 33 or 34 or 35 or 41;

	public bool IsTrainingHarmonyArena => MaskId is 45 or 101 or 102 or 103;

	public int MaximumJoinTimeMilliseconds => MaskId switch
	{
		1 or 2 or 3 => 420000,
		21 or 22 or 23 or 24 or 25 or 26 or 27 or 28 or 29 or 30 or 31 or 32 or 33 or 34 or 35 or 38 or 39 or 40 or 41 or 42 or 43 or 44 or 45 or 101 or 102 or 103 => 110000,
		107 or 108 or 109 => 230000,
		111 => 170000,
		_ => 0,
	};

	public byte DifficultyId => MaskId switch
	{
		21 or 24 or 27 or 30 or 33 or 101 => 1,
		22 or 25 or 28 or 31 or 34 or 42 or 102 => 2,
		23 or 26 or 29 or 32 or 35 or 103 => 3,
		39 or 40 or 41 or 43 or 44 or 45 => 4,
		_ => 0,
	};
}
