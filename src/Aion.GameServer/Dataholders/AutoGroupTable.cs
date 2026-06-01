namespace Aion.GameServer.Dataholders;

public sealed class AutoGroupTable
{
	private readonly IReadOnlyDictionary<int, AutoGroupSummary> _autoGroupsByMaskId;
	private readonly IReadOnlyDictionary<int, IReadOnlyList<int>> _recruitableInstanceMaskIdsByPortalNpcId;
	private readonly IReadOnlyList<int> _recruitableInstanceMaskIds;

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
}
