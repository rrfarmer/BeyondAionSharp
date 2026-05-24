using System.Collections.ObjectModel;

namespace Aion.GameServer.Dataholders;

public sealed class NpcSkillTable
{
	private readonly IReadOnlyDictionary<int, NpcSkillListSummary> _skillListsByNpcId;

	public NpcSkillTable(IReadOnlyList<NpcSkillListSummary> skillLists)
	{
		SkillLists = skillLists;
		var skillListsByNpcId = new Dictionary<int, NpcSkillListSummary>();
		foreach (var skillList in skillLists)
		{
			foreach (var npcId in skillList.NpcIds)
			{
				// Java parity: dataholders/NpcSkillData.afterUnmarshal keeps the first list and logs duplicate npc_ids.
				skillListsByNpcId.TryAdd(npcId, skillList);
			}
		}

		_skillListsByNpcId = new ReadOnlyDictionary<int, NpcSkillListSummary>(skillListsByNpcId);
	}

	public IReadOnlyList<NpcSkillListSummary> SkillLists { get; }

	public int Count => _skillListsByNpcId.Count;

	public NpcSkillListSummary? GetNpcSkillList(int npcId)
	{
		// Java parity: dataholders/NpcSkillData.getNpcSkillList.
		return _skillListsByNpcId.GetValueOrDefault(npcId);
	}

	public IReadOnlyCollection<NpcSkillListSummary> GetAllNpcSkillTemplates()
	{
		// Java parity: dataholders/NpcSkillData.getAllNpcSkillTemplates returns indexed list values.
		return _skillListsByNpcId.Values.ToArray();
	}
}

public sealed record NpcSkillListSummary(
	IReadOnlyList<int> NpcIds,
	IReadOnlyList<NpcSkillTemplateSummary> Skills);

public sealed record NpcSkillTemplateSummary(
	int SkillId,
	int SkillLevel,
	int Probability,
	int MinHp,
	int MaxHp,
	int MaxTime,
	int MinTime,
	string Conjunction,
	int Cooldown,
	bool IsPostSpawn,
	int Priority,
	int NextSkillTime,
	int NextChainId,
	int ChainId,
	int MaxChainTime,
	string Target,
	NpcSkillSpawnSummary? Spawn,
	NpcSkillConditionSummary? Condition = null);

public sealed record NpcSkillSpawnSummary(
	int NpcId,
	int Delay,
	int MinDistance,
	int MaxDistance,
	int MinCount,
	int MaxCount);

public sealed record NpcSkillConditionSummary(
	string ConditionType,
	int HpBelow,
	int Range,
	int NpcId,
	int Delay,
	bool CanDie,
	int DespawnTime);
