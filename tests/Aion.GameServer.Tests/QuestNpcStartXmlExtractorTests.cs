using System.Text;
using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Tests;

public sealed class QuestNpcStartXmlExtractorTests
{
	[Fact]
	public void ExtractsStartNpcIdsFromXmlQuestTemplates()
	{
		const string xml = """
			<quest_scripts>
				<report_to id="18506" start_npc_ids="799523" />
				<work_orders id="2900" start_npc_ids="203098 203099
					203100" />
			</quest_scripts>
			""";
		var extractor = new QuestNpcStartXmlExtractor();

		var sources = extractor.Extract(xml, "game-server/data/static_data/quest_script_data/sample.xml");

		Assert.Collection(
			sources,
			source => AssertSource(source, 799523, 18506),
			source => AssertSource(source, 203098, 2900),
			source => AssertSource(source, 203099, 2900),
			source => AssertSource(source, 203100, 2900));
		Assert.All(sources, source =>
		{
			Assert.Equal(QuestNpcStartRegistrationSourceKind.XmlQuest, source.SourceKind);
			Assert.Equal("game-server/data/static_data/quest_script_data/sample.xml", source.SourcePath);
			Assert.Equal(QuestNpcStartRegistration.DefaultQuestRange, source.QuestRange);
		});
	}

	[Fact]
	public void ReportToManyWithStartItemIdSkipsNpcStartRegistrationLikeJava()
	{
		const string xml = """
			<quest_scripts>
				<report_to_many id="2274" start_item_id="182203249" start_npc_ids="203622" />
				<report_to_many id="2275" start_item_id="0" start_npc_ids="203623" />
				<report_to_many id="2276" start_npc_ids="203624" />
			</quest_scripts>
			""";
		var extractor = new QuestNpcStartXmlExtractor();

		var sources = extractor.Extract(xml, "game-server/data/static_data/quest_script_data/altgard.xml");

		Assert.Collection(
			sources,
			source => AssertSource(source, 203623, 2275),
			source => AssertSource(source, 203624, 2276));
	}

	[Fact]
	public void ExtractedSourcesCanPopulateQuestNpcStartTable()
	{
		const string xml = """
			<quest_scripts>
				<monster_hunt id="2561" start_npc_ids="204753 295178" />
			</quest_scripts>
			""";
		var extractor = new QuestNpcStartXmlExtractor();
		var table = new QuestNpcStartTable();

		foreach (var source in extractor.Extract(xml, "game-server/data/static_data/quest_script_data/beluslan.xml"))
			table.RegisterOnQuestStart(source);

		Assert.Equal([2561], table.GetQuestNpc(204753).OnQuestStart.Order());
		Assert.Equal([2561], table.GetQuestNpc(295178).OnQuestStart.Order());
		Assert.Equal(2, table.Sources.Count);
	}

	[Fact]
	public void ExtractFromStreamUsesSameXmlAttributeRules()
	{
		const string xml = """
			<quest_scripts>
				<relic_rewards id="11279" start_npc_ids="799945 205553" />
			</quest_scripts>
			""";
		using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
		var extractor = new QuestNpcStartXmlExtractor();

		var sources = extractor.Extract(stream, "game-server/data/static_data/quest_script_data/inggison.xml");

		Assert.Collection(
			sources,
			source => AssertSource(source, 799945, 11279),
			source => AssertSource(source, 205553, 11279));
	}

	private static void AssertSource(QuestNpcStartRegistrationSource source, int npcId, int questId)
	{
		Assert.Equal(npcId, source.NpcId);
		Assert.Equal(questId, source.QuestId);
	}
}
