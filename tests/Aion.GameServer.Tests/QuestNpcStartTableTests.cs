using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Tests;

public sealed class QuestNpcStartTableTests
{
	[Fact]
	public void RegisterQuestNpc_ReusesRegistrationAndTracksStartQuestIdsLikeJava()
	{
		var table = new QuestNpcStartTable();

		var questNpc = table.RegisterQuestNpc(203098);

		Assert.Equal(203098, questNpc.NpcId);
		Assert.Equal(QuestNpcStartRegistration.DefaultQuestRange, questNpc.QuestRange);
		Assert.True(questNpc.AddOnQuestStart(1192));
		Assert.False(questNpc.AddOnQuestStart(1192));
		Assert.True(questNpc.AddOnQuestStart(1194));
		Assert.Equal([1192, 1194], questNpc.OnQuestStart.Order());
		Assert.Same(questNpc, table.RegisterQuestNpc(203098, questRange: 35));
		Assert.Equal(QuestNpcStartRegistration.DefaultQuestRange, questNpc.QuestRange);
		Assert.Equal([1192, 1194], table.GetQuestNpc(203098).OnQuestStart.Order());
	}

	[Fact]
	public void GetQuestNpc_ReturnsUnregisteredEmptyRegistrationLikeJava()
	{
		var table = new QuestNpcStartTable();

		var missing = table.GetQuestNpc(999999);

		Assert.Equal(999999, missing.NpcId);
		Assert.Equal(QuestNpcStartRegistration.DefaultQuestRange, missing.QuestRange);
		Assert.Empty(missing.OnQuestStart);
		Assert.Empty(table.Registrations);
	}

	[Fact]
	public void RegisterOnQuestStart_RecordsSourceBoundaryForFutureHandlerAndXmlExtractors()
	{
		var table = new QuestNpcStartTable();
		var handlerSource = new QuestNpcStartRegistrationSource(
			NpcId: 278151,
			QuestId: 30363,
			SourceKind: QuestNpcStartRegistrationSourceKind.JavaHandler,
			SourcePath: "game-server/data/handlers/quest/abyssal_splinter/_30363FoolsRushIn.java");
		var xmlSource = new QuestNpcStartRegistrationSource(
			NpcId: 799530,
			QuestId: 28303,
			SourceKind: QuestNpcStartRegistrationSourceKind.XmlQuest,
			SourcePath: "game-server/data/static_data/quest_script_data/sample.xml",
			QuestRange: 30);

		Assert.True(table.RegisterOnQuestStart(handlerSource));
		Assert.False(table.RegisterOnQuestStart(handlerSource));
		Assert.True(table.RegisterOnQuestStart(xmlSource));

		Assert.Equal([30363], table.GetQuestNpc(278151).OnQuestStart.Order());
		Assert.Equal([28303], table.GetQuestNpc(799530).OnQuestStart.Order());
		Assert.Equal(30, table.GetQuestNpc(799530).QuestRange);
		Assert.Equal([handlerSource, handlerSource, xmlSource], table.Sources);
	}

	[Fact]
	public void RegisterQuestNpc_PreservesFirstRegisteredRangeLikeJava()
	{
		var table = new QuestNpcStartTable();

		var questNpc = table.RegisterQuestNpc(730000, questRange: 45);
		var reused = table.RegisterQuestNpc(730000, questRange: 10);

		Assert.Same(questNpc, reused);
		Assert.Equal(45, reused.QuestRange);
	}
}
