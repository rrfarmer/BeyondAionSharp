using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Tests;

public sealed class QuestCompletionFollowUpJavaHandlerExtractorTests
{
	[Fact]
	public void Extract_ReadsLiteralDefaultOnQuestCompletedFollowUp()
	{
		var extractor = new QuestCompletionFollowUpJavaHandlerExtractor();

		var result = extractor.Extract(
			"""
			public class _1016FollowUp extends AbstractQuestHandler {
				public _1016FollowUp() {
					super(1016);
				}

				@Override
				public void register() {
					qe.registerOnQuestCompleted(questId);
				}

				@Override
				public void onQuestCompletedEvent(QuestEnv env) {
					defaultOnQuestCompletedEvent(env, 1015);
				}
			}
			""",
			"game-server/data/handlers/quest/test/_1016FollowUp.java");

		var registration = Assert.Single(result.Registrations);
		Assert.Equal(1016, registration.QuestId);
		Assert.Equal([1015], registration.PreQuestIds);
		Assert.Equal("game-server/data/handlers/quest/test/_1016FollowUp.java", registration.SourcePath);
	}

	[Fact]
	public void Extract_ReadsArrayDefaultOnQuestCompletedFollowUps()
	{
		var extractor = new QuestCompletionFollowUpJavaHandlerExtractor();

		var result = extractor.Extract(
			"""
			public class _14016AGateAgape extends AbstractQuestHandler {
				public _14016AGateAgape() {
					super(14016);
				}

				@Override
				public void register() {
					qe.registerOnQuestCompleted(questId);
				}

				@Override
				public void onQuestCompletedEvent(QuestEnv env) {
					int[] verteronQuests = { 14010, 14011, 14012 };
					defaultOnQuestCompletedEvent(env, verteronQuests);
				}
			}
			""",
			"game-server/data/handlers/quest/verteron/_14016AGateAgape.java");

		var registration = Assert.Single(result.Registrations);
		Assert.Equal(14016, registration.QuestId);
		Assert.Equal([14010, 14011, 14012], registration.PreQuestIds);
	}

	[Fact]
	public void Extract_SkipsHandlersWithoutRegisteredCompletionEvent()
	{
		var extractor = new QuestCompletionFollowUpJavaHandlerExtractor();

		var result = extractor.Extract(
			"""
			public class _1016FollowUp extends AbstractQuestHandler {
				public _1016FollowUp() {
					super(1016);
				}

				@Override
				public void onQuestCompletedEvent(QuestEnv env) {
					defaultOnQuestCompletedEvent(env, 1015);
				}
			}
			""",
			"game-server/data/handlers/quest/test/_1016FollowUp.java");

		Assert.Empty(result.Registrations);
	}
}
