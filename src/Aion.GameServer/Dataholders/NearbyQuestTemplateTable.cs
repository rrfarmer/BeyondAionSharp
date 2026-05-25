namespace Aion.GameServer.Dataholders;

public sealed class NearbyQuestTemplateTable
{
	private readonly Dictionary<int, NearbyQuestTemplateSummary> _templates;

	public NearbyQuestTemplateTable(IEnumerable<NearbyQuestTemplateSummary> templates)
	{
		// Java parity: dataholders/QuestsData.afterUnmarshal indexes QuestTemplate by quest id.
		_templates = templates.ToDictionary(template => template.QuestId);
	}

	public int Count => _templates.Count;

	public bool TryGetQuest(int questId, out NearbyQuestTemplateSummary? template)
	{
		return _templates.TryGetValue(questId, out template);
	}
}

public sealed record NearbyQuestTemplateSummary(
	int QuestId,
	int MinLevelPermitted = 0,
	int MaxLevelPermitted = 0,
	string RacePermitted = "",
	IReadOnlySet<string>? ClassPermitted = null,
	string GenderPermitted = "",
	int RequiredRank = 0,
	int MaxRepeatCount = 1,
	bool IsTimeBased = false,
	bool HasXmlStartConditions = false,
	bool HasInventoryItems = false,
	int CombineSkill = 0,
	int NpcFactionId = 0,
	IReadOnlyList<NearbyQuestXmlStartCondition>? XmlStartConditions = null,
	bool HasUnsupportedXmlStartConditionElements = false)
{
	public IReadOnlySet<string> ClassPermitted { get; } = ClassPermitted ?? new HashSet<string>(StringComparer.Ordinal);

	public IReadOnlyList<NearbyQuestXmlStartCondition> XmlStartConditions { get; } =
		XmlStartConditions ?? Array.Empty<NearbyQuestXmlStartCondition>();
}

public sealed record NearbyQuestXmlStartCondition(
	IReadOnlyList<NearbyQuestFinishedCondition>? Finished = null,
	IReadOnlySet<int>? Unfinished = null,
	IReadOnlySet<int>? NoAcquired = null,
	IReadOnlySet<int>? Acquired = null,
	IReadOnlySet<int>? Equipped = null,
	int RequiredTitle = 0,
	bool HasUnsupportedElements = false)
{
	public IReadOnlyList<NearbyQuestFinishedCondition> Finished { get; } =
		Finished ?? Array.Empty<NearbyQuestFinishedCondition>();

	public IReadOnlySet<int> Unfinished { get; } =
		Unfinished ?? new HashSet<int>();

	public IReadOnlySet<int> NoAcquired { get; } =
		NoAcquired ?? new HashSet<int>();

	public IReadOnlySet<int> Acquired { get; } =
		Acquired ?? new HashSet<int>();

	public IReadOnlySet<int> Equipped { get; } =
		Equipped ?? new HashSet<int>();

	public bool IsOptional => Finished.Count != 0;
}

public sealed record NearbyQuestFinishedCondition(int QuestId, int Reward = -1);
