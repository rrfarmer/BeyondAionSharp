using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Tests;

public sealed class NearbyQuestTemplateTableTests
{
	[Fact]
	public void GetQuestsByNpcFaction_MatchesJavaNonTimeBasedFactionIndex()
	{
		var table = new NearbyQuestTemplateTable(
		[
			new NearbyQuestTemplateSummary(2001, NpcFactionId: 42),
			new NearbyQuestTemplateSummary(2002, NpcFactionId: 42, IsTimeBased: true, RepeatCycle: ["ALL"]),
			new NearbyQuestTemplateSummary(2003),
			new NearbyQuestTemplateSummary(2004, NpcFactionId: 43),
			new NearbyQuestTemplateSummary(2005, NpcFactionId: 42),
		]);

		Assert.Equal([2001, 2005], table.GetQuestsByNpcFaction(42).Select(template => template.QuestId));
		Assert.Equal([2004], table.GetQuestsByNpcFaction(43).Select(template => template.QuestId));
		Assert.Empty(table.GetQuestsByNpcFaction(99));
	}
}
