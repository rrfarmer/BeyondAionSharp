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
	int NpcFactionId = 0)
{
	public IReadOnlySet<string> ClassPermitted { get; } = ClassPermitted ?? new HashSet<string>(StringComparer.Ordinal);
}
