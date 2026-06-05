using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Tests;

public sealed class QuestHandlerAvailabilityTableTests
{
	[Fact]
	public void TryReadJavaHandlerQuestId_MatchesJavaQuestHandlerLoaderConcretePublicHandlers()
	{
		const string source = """
			public class _38000CallOfTheAlabasterOrder extends AbstractQuestHandler {
				public _38000CallOfTheAlabasterOrder() {
					super(38000);
				}
			}
			""";

		Assert.True(QuestHandlerAvailabilityTable.TryReadJavaHandlerQuestId(source, out var questId));
		Assert.Equal(38000, questId);
	}

	[Fact]
	public void TryReadJavaHandlerQuestId_ResolvesNamedConstructorConstant()
	{
		const string source = """
			public class _48001CallOfTheCrusade extends AbstractQuestHandler {
				private static final int QUEST_ID = 48001;

				public _48001CallOfTheCrusade() {
					super(QUEST_ID);
				}
			}
			""";

		Assert.True(QuestHandlerAvailabilityTable.TryReadJavaHandlerQuestId(source, out var questId));
		Assert.Equal(48001, questId);
	}

	[Theory]
	[InlineData("abstract public class Template extends AbstractQuestHandler { public Template() { super(1); } }")]
	[InlineData("class PackagePrivate extends AbstractQuestHandler { public PackagePrivate() { super(1); } }")]
	[InlineData("public class Helper { public Helper() { super(1); } }")]
	public void TryReadJavaHandlerQuestId_SkipsClassesJavaLoaderWouldSkip(string source)
	{
		Assert.False(QuestHandlerAvailabilityTable.TryReadJavaHandlerQuestId(source, out _));
	}

	[Fact]
	public void Load_CombinesJavaHandlerIdsAndXmlQuestScriptIds()
	{
		var tempRoot = Path.Combine(Path.GetTempPath(), "aion-handler-availability-" + Guid.NewGuid().ToString("N"));
		try
		{
			var handlerDirectory = Path.Combine(tempRoot, "handlers");
			Directory.CreateDirectory(handlerDirectory);
			var cacheFile = Path.Combine(tempRoot, "static_data.xml");
			File.WriteAllText(
				cacheFile,
				"""
				<static_data>
					<quest_scripts>
						<xml_quest id="1127" start_npc_ids="798008" />
						<work_order id="2900" combine_skill="40001" />
					</quest_scripts>
					<npcs>
						<npc id="700001" />
					</npcs>
				</static_data>
				""");
			File.WriteAllText(
				Path.Combine(handlerDirectory, "_38000CallOfTheAlabasterOrder.java"),
				"""
				public class _38000CallOfTheAlabasterOrder extends AbstractQuestHandler {
					public _38000CallOfTheAlabasterOrder() {
						super(38000);
					}
				}
				""");

			var table = QuestHandlerAvailabilityTable.Load(cacheFile, handlerDirectory);

			Assert.True(table.IsHaveHandler(1127));
			Assert.True(table.IsHaveHandler(2900));
			Assert.True(table.IsHaveHandler(38000));
			Assert.False(table.IsHaveHandler(700001));
		}
		finally
		{
			if (Directory.Exists(tempRoot))
				Directory.Delete(tempRoot, recursive: true);
		}
	}

	[Fact]
	public async Task StaticDataLoadFromCache_CarriesQuestHandlerAvailabilityIntoRuntimeData()
	{
		var tempRoot = Path.Combine(Path.GetTempPath(), "aion-static-handler-availability-" + Guid.NewGuid().ToString("N"));
		try
		{
			var handlerDirectory = Path.Combine(tempRoot, "handlers");
			Directory.CreateDirectory(handlerDirectory);
			var cacheFile = Path.Combine(tempRoot, "static_data.xml");
			File.WriteAllText(
				cacheFile,
				"""
				<static_data>
					<quest_scripts>
						<xml_quest id="1127" start_npc_ids="798008" />
					</quest_scripts>
				</static_data>
				""");
			File.WriteAllText(
				Path.Combine(handlerDirectory, "_38000CallOfTheAlabasterOrder.java"),
				"""
				public class _38000CallOfTheAlabasterOrder extends AbstractQuestHandler {
					public _38000CallOfTheAlabasterOrder() {
						super(38000);
					}
				}
				""");

			var staticData = await StaticData.LoadFromCacheAsync(cacheFile, Array.Empty<string>(), handlerDirectory);

			Assert.True(staticData.QuestHandlers.IsHaveHandler(1127));
			Assert.True(staticData.QuestHandlers.IsHaveHandler(38000));
		}
		finally
		{
			if (Directory.Exists(tempRoot))
				Directory.Delete(tempRoot, recursive: true);
		}
	}
}
