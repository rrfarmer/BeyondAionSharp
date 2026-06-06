using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Tests;

public sealed class QuestNpcStartJavaHandlerExtractorTests
{
	[Fact]
	public void ExtractsLiteralNpcIdWithInheritedQuestId()
	{
		const string javaSource = """
			public class _4725CeaselessAttack extends AbstractQuestHandler {
				public _4725CeaselessAttack() {
					super(4725);
				}

				public void register() {
					qe.registerQuestNpc(799403).addOnQuestStart(questId);
					qe.registerQuestNpc(799404).addOnTalkEvent(questId);
				}
			}
			""";
		var extractor = new QuestNpcStartJavaHandlerExtractor();

		var result = extractor.Extract(javaSource, "game-server/data/handlers/quest/chantra_dredgion/_4725CeaselessAttack.java");

		Assert.Empty(result.Unresolved);
		Assert.Collection(
			result.Sources,
			source => AssertSource(source, 799403, 4725, QuestNpcRegistrationEventKind.OnQuestStart),
			source => AssertSource(source, 799404, 4725, QuestNpcRegistrationEventKind.OnTalkEvent));
	}

	[Fact]
	public void ExtractsScalarConstantsAndArrayIndexes()
	{
		const string javaSource = """
			public class _30772InvestigateTheGate extends AbstractQuestHandler {
				private static final int START_NPC_ID = 804869;
				private final static int questStartNpcId = 203631;
				private int[] npcIds = { 799530, 730375 };

				public _30772InvestigateTheGate() {
					super(30772);
				}

				public void register() {
					qe.registerQuestNpc(START_NPC_ID).addOnQuestStart(questId);
					qe.registerQuestNpc(questStartNpcId).addOnQuestStart(questId);
					qe.registerQuestNpc(npcIds[0]).addOnQuestStart(questId);
					qe.registerQuestNpc(npcIds[1]).addOnTalkEvent(questId);
				}
			}
			""";
		var extractor = new QuestNpcStartJavaHandlerExtractor();

		var result = extractor.Extract(javaSource, "game-server/data/handlers/quest/sample.java");

		Assert.Empty(result.Unresolved);
		Assert.Collection(
			result.Sources,
			source => AssertSource(source, 804869, 30772),
			source => AssertSource(source, 203631, 30772),
			source => AssertSource(source, 799530, 30772),
			source => AssertSource(source, 730375, 30772, QuestNpcRegistrationEventKind.OnTalkEvent));
	}

	[Fact]
	public void ReportsUnsupportedExpressionsInsteadOfGuessing()
	{
		const string javaSource = """
			public class DynamicQuest extends AbstractQuestHandler {
				public DynamicQuest() {
					super(1234);
				}

				public void register() {
					qe.registerQuestNpc(npcIds[i]).addOnQuestStart(questId);
					qe.registerQuestNpc(799403).addOnTalkEvent(dynamicQuestId);
				}
			}
			""";
		var extractor = new QuestNpcStartJavaHandlerExtractor();

		var result = extractor.Extract(javaSource, "game-server/data/handlers/quest/dynamic.java");

		Assert.Empty(result.Sources);
		Assert.Collection(
			result.Unresolved,
			unresolved =>
			{
				Assert.Equal("npcIds[i]", unresolved.NpcExpression);
				Assert.Equal("questId", unresolved.QuestExpression);
				Assert.Contains("Unsupported expression", unresolved.Reason, StringComparison.Ordinal);
				Assert.True(unresolved.LineNumber > 0);
			},
			unresolved =>
			{
				Assert.Equal("799403", unresolved.NpcExpression);
				Assert.Equal("dynamicQuestId", unresolved.QuestExpression);
				Assert.Contains("Unsupported expression", unresolved.Reason, StringComparison.Ordinal);
			});
	}

	[Fact]
	public void ExtractsIteratorAndForEachValuesFromStaticIntegerSets()
	{
		const string javaSource = """
			public class _18806HeartofRock extends AbstractQuestHandler {
				private static final Set<Integer> butlers;

				static {
					butlers = new HashSet<>();
					butlers.add(810017);
					butlers.add(810018);
				}

				public _18806HeartofRock() {
					super(18806);
				}

				public void register() {
					Iterator<Integer> iter = butlers.iterator();
					while (iter.hasNext()) {
						int butlerId = iter.next();
						qe.registerQuestNpc(butlerId).addOnQuestStart(questId);
					}
					for (int secondButlerId : butlers)
						qe.registerQuestNpc(secondButlerId).addOnQuestStart(questId);
				}
			}
			""";
		var extractor = new QuestNpcStartJavaHandlerExtractor();

		var result = extractor.Extract(javaSource, "game-server/data/handlers/quest/oriel/_18806HeartofRock.java");

		Assert.Empty(result.Unresolved);
		Assert.Collection(
			result.Sources,
			source => AssertSource(source, 810017, 18806),
			source => AssertSource(source, 810018, 18806),
			source => AssertSource(source, 810017, 18806),
			source => AssertSource(source, 810018, 18806));
	}

	[Fact]
	public void ExtractedHandlerSourcesCanPopulateQuestNpcStartTable()
	{
		const string javaSource = """
			public class _30363FoolsRushIn extends AbstractQuestHandler {
				public _30363FoolsRushIn() {
					super(30363);
				}

				public void register() {
					qe.registerQuestNpc(278151).addOnQuestStart(questId);
					qe.registerQuestNpc(278151).addOnTalkEvent(questId);
				}
			}
			""";
		var extractor = new QuestNpcStartJavaHandlerExtractor();
		var table = new QuestNpcStartTable();

		foreach (var source in extractor.Extract(javaSource, "game-server/data/handlers/quest/abyssal_splinter/_30363FoolsRushIn.java").Sources)
		{
			if (source.EventKind == QuestNpcRegistrationEventKind.OnTalkEvent)
				table.RegisterOnTalkEvent(source);
			else
				table.RegisterOnQuestStart(source);
		}

		Assert.Equal([30363], table.GetQuestNpc(278151).OnQuestStart.Order());
		Assert.Equal([30363], table.GetQuestNpc(278151).OnTalkEvent);
		Assert.Equal(2, table.Sources.Count);
	}

	private static void AssertSource(
		QuestNpcStartRegistrationSource source,
		int npcId,
		int questId,
		QuestNpcRegistrationEventKind eventKind = QuestNpcRegistrationEventKind.OnQuestStart)
	{
		Assert.Equal(npcId, source.NpcId);
		Assert.Equal(questId, source.QuestId);
		Assert.Equal(QuestNpcStartRegistrationSourceKind.JavaHandler, source.SourceKind);
		Assert.Equal(QuestNpcStartRegistration.DefaultQuestRange, source.QuestRange);
		Assert.Equal(eventKind, source.EventKind);
	}
}
