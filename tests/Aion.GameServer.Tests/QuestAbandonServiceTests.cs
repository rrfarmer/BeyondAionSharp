using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class QuestAbandonServiceTests
{
	[Fact]
	public void Abandon_FirstTimeStartedQuestDeletesStateAndSendsAbandonPacket()
	{
		var player = PlayerWithQuest(new PlayerQuestState(1001, "START", QuestVars: 0x22, Flags: 2, CompleteCount: 0));

		var result = QuestAbandonService.Abandon(player, 1001, Template());

		Assert.Equal(QuestAbandonStatus.Deleted, result.Status);
		Assert.True(result.Mutated);
		Assert.Empty(player.Quests);
		Assert.True(result.NearbyQuestRefreshRequired);
		var packet = Assert.Single(result.Packets);
		Assert.Equal(Convert.FromHexString("03E903000000000000"), SerializeUnencryptedPayload(packet));
	}

	[Fact]
	public void Abandon_PreviouslyCompletedQuestResetsToCompleteLikeJava()
	{
		var player = PlayerWithQuest(new PlayerQuestState(1002, "START", QuestVars: 0x1122, Flags: 3, CompleteCount: 2));

		var result = QuestAbandonService.Abandon(player, 1002, Template(1002));

		Assert.Equal(QuestAbandonStatus.ResetToComplete, result.Status);
		var reset = Assert.Single(player.Quests);
		Assert.Equal("COMPLETE", reset.Status);
		Assert.Equal(0, reset.QuestVars);
		Assert.Equal(0, reset.Flags);
		Assert.Equal(2, reset.CompleteCount);
		Assert.Same(reset, result.FinalQuestState);
		Assert.True(result.NearbyQuestRefreshRequired);
		Assert.Equal(Convert.FromHexString("03EA03000000000000"), SerializeUnencryptedPayload(Assert.Single(result.Packets)));
	}

	[Theory]
	[InlineData("COMPLETE", QuestAbandonStatus.AlreadyComplete)]
	[InlineData("LOCKED", QuestAbandonStatus.Locked)]
	public void Abandon_CompleteAndLockedStatesDoNotMutate(string status, QuestAbandonStatus expected)
	{
		var original = new PlayerQuestState(1003, status, QuestVars: 7, Flags: 1, CompleteCount: 1);
		var player = PlayerWithQuest(original);

		var result = QuestAbandonService.Abandon(player, 1003, Template(1003));

		Assert.Equal(expected, result.Status);
		Assert.False(result.Mutated);
		Assert.Same(original, Assert.Single(player.Quests));
		Assert.Empty(result.Packets);
		Assert.False(result.NearbyQuestRefreshRequired);
	}

	[Fact]
	public void Abandon_CannotGiveupTemplateBlocksMutation()
	{
		var questState = new PlayerQuestState(1004, "START", QuestVars: 1, Flags: 0, CompleteCount: 0);
		var player = PlayerWithQuest(questState);

		var result = QuestAbandonService.Abandon(player, 1004, Template(1004, cannotGiveup: true));

		Assert.Equal(QuestAbandonStatus.CannotGiveup, result.Status);
		Assert.False(result.Mutated);
		Assert.Same(questState, Assert.Single(player.Quests));
		Assert.Empty(result.Packets);
	}

	[Fact]
	public void Abandon_TimerTemplateSendsTimerClearBeforeAbandon()
	{
		var player = PlayerWithQuest(new PlayerQuestState(1005, "START", QuestVars: 0, Flags: 0, CompleteCount: 0));

		var result = QuestAbandonService.Abandon(player, 1005, Template(1005, isTimer: true));

		Assert.Equal(QuestAbandonStatus.Deleted, result.Status);
		Assert.Equal(2, result.Packets.Count);
		Assert.Equal(Convert.FromHexString("04ED0300000000000000"), SerializeUnencryptedPayload(result.Packets[0]));
		Assert.Equal(Convert.FromHexString("03ED03000000000000"), SerializeUnencryptedPayload(result.Packets[1]));
	}

	[Fact]
	public void Abandon_MissingTemplateReturnsFalseLikeJava()
	{
		var questState = new PlayerQuestState(1006, "START", QuestVars: 0, Flags: 0, CompleteCount: 0);
		var player = PlayerWithQuest(questState);

		var result = QuestAbandonService.Abandon(player, 1006, template: null);

		Assert.Equal(QuestAbandonStatus.MissingTemplate, result.Status);
		Assert.False(result.Mutated);
		Assert.Same(questState, Assert.Single(player.Quests));
		Assert.Empty(result.Packets);
	}

	private static Player PlayerWithQuest(PlayerQuestState questState)
	{
		return new Player { Quests = [questState] };
	}

	private static NearbyQuestTemplateSummary Template(int questId = 1001, bool cannotGiveup = false, bool isTimer = false)
	{
		return new NearbyQuestTemplateSummary(questId, CannotGiveup: cannotGiveup, IsTimer: isTimer);
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}
}
