using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class QuestFinishSocketGuardedOperationCompositionPlanServiceTests
{
	private const int PlayerObjectId = 42;
	private const int NpcObjectId = 9001;
	private const int SelectedQuestAutoReward = 108;

	[Fact]
	public void CreatePlan_ComposesGuardedSelfAutoRewardInputIntoNonLiveOperationPlan()
	{
		var plan = QuestFinishSocketGuardedOperationCompositionPlanService.CreatePlan(
			new Player
			{
				ObjectId = PlayerObjectId,
				PlayerClass = "RANGER",
				Quests = [new PlayerQuestState(1001, "REWARD", QuestVars: 0x12, Flags: 0, CompleteCount: 0)],
			},
			CreateDialogSelect(targetObjectId: 0, SelectedQuestAutoReward, questId: 1001),
			CreateLookupTable("""
				<quests>
					<quest id="1001" can_report="true" reward_repeat_count="1">
						<rewards gold="55">
							<reward_item item_id="182400001" count="1" />
						</rewards>
					</quest>
				</quests>
				"""),
			PlayerNpcFactionsSnapshot.Empty,
			new DateTimeOffset(2026, 5, 26, 10, 0, 0, TimeSpan.Zero),
			CreateOptions("UTC"));

		Assert.Equal(QuestFinishSocketGuardedOperationCompositionStatus.Composed, plan.Status);
		Assert.Equal(QuestDialogAutoRewardGuardStatus.Planned, plan.GuardedInputPlan.GuardPlan.Status);
		Assert.Equal(QuestFinishSocketInputAssemblyStatus.Ready, plan.GuardedInputPlan.InputPlan?.Status);
		Assert.NotNull(plan.OperationCompositionPlan);
		Assert.Equal(QuestFinishSocketOperationCompositionStatus.Composed, plan.OperationCompositionPlan.Status);
		Assert.NotNull(plan.OperationCompositionPlan.OperationPlan);
		Assert.True(plan.OperationCompositionPlan.OperationPlan.Applied);
		Assert.All(plan.OperationCompositionPlan.OperationPlan.Descriptors, descriptor => Assert.False(descriptor.IsLive));
		Assert.Contains(
			plan.OperationCompositionPlan.OperationPlan.Descriptors,
			descriptor => descriptor.Action == QuestFinishOperationAction.NonItemRewardProjection
				&& descriptor.Count == 55);
		Assert.Contains(
			plan.OperationCompositionPlan.OperationPlan.Descriptors,
			descriptor => descriptor.Action == QuestFinishOperationAction.ItemRewardProjection
				&& descriptor.ItemId == 182400001);
		Assert.Contains(
			plan.OperationCompositionPlan.OperationPlan.Descriptors,
			descriptor => descriptor.Action == QuestFinishOperationAction.QuestStateMutation);
	}

	[Fact]
	public void CreatePlan_DoesNotComposeWhenJavaGuardRejectsNpcTarget()
	{
		var plan = QuestFinishSocketGuardedOperationCompositionPlanService.CreatePlan(
			new Player
			{
				ObjectId = PlayerObjectId,
				PlayerClass = "RANGER",
				Quests = [new PlayerQuestState(1001, "REWARD", QuestVars: 0, Flags: 0, CompleteCount: 0)],
			},
			CreateDialogSelect(NpcObjectId, SelectedQuestAutoReward, questId: 1001),
			CreateLookupTable("""
				<quests>
					<quest id="1001" can_report="true">
						<rewards gold="55" />
					</quest>
				</quests>
				"""),
			PlayerNpcFactionsSnapshot.Empty,
			new DateTimeOffset(2026, 5, 26, 10, 0, 0, TimeSpan.Zero),
			CreateOptions("UTC"));

		Assert.Equal(QuestFinishSocketGuardedOperationCompositionStatus.GuardRejected, plan.Status);
		Assert.Equal(QuestDialogAutoRewardGuardStatus.NonSelfTarget, plan.GuardedInputPlan.GuardPlan.Status);
		Assert.Null(plan.GuardedInputPlan.InputPlan);
		Assert.Null(plan.OperationCompositionPlan);
	}

	[Fact]
	public void CreatePlan_DoesNotComposeWhenInputAssemblyHasDiagnostics()
	{
		var plan = QuestFinishSocketGuardedOperationCompositionPlanService.CreatePlan(
			new Player
			{
				ObjectId = PlayerObjectId,
				PlayerClass = "",
				Quests = [new PlayerQuestState(1002, "REWARD", QuestVars: 0, Flags: 0, CompleteCount: 0)],
			},
			CreateDialogSelect(targetObjectId: 0, SelectedQuestAutoReward, questId: 1002),
			CreateLookupTable("""
				<quests>
					<quest id="1002" can_report="true" use_class_reward="2" reward_repeat_count="1">
						<rewards>
							<selectable_reward_item item_id="182400001" count="1" />
						</rewards>
						<ranger_selectable_reward item_id="100900001" count="2" />
					</quest>
				</quests>
				"""),
			PlayerNpcFactionsSnapshot.Empty,
			new DateTimeOffset(2026, 5, 26, 10, 0, 0, TimeSpan.Zero),
			CreateOptions("UTC"));

		Assert.Equal(QuestFinishSocketGuardedOperationCompositionStatus.InputNotReady, plan.Status);
		Assert.Equal(QuestDialogAutoRewardGuardStatus.Planned, plan.GuardedInputPlan.GuardPlan.Status);
		Assert.Equal(QuestFinishSocketInputAssemblyStatus.ProjectionDiagnostics, plan.GuardedInputPlan.InputPlan?.Status);
		Assert.NotNull(plan.OperationCompositionPlan);
		Assert.Equal(QuestFinishSocketOperationCompositionStatus.InputNotReady, plan.OperationCompositionPlan.Status);
		Assert.Null(plan.OperationCompositionPlan.OperationPlan);
	}

	private static QuestFinishRewardProjectionLookupTable CreateLookupTable(string xml)
	{
		return new QuestFinishRewardProjectionLookupTableXmlFactory().Create(xml);
	}

	private static CmDialogSelect CreateDialogSelect(int targetObjectId, int dialogActionId, int questId)
	{
		using var writer = new PacketBuffer();
		writer.WriteD(targetObjectId);
		writer.WriteH(dialogActionId);
		writer.WriteH(0);
		writer.WriteH(0);
		writer.WriteD(questId);
		writer.WriteH(0);
		var packet = new CmDialogSelect(56, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var reader = new PacketBuffer(writer.ToArray());
		packet.ReadFrom(reader);
		return packet;
	}

	private static GameServerOptions CreateOptions(string timeZoneId)
	{
		return new GameServerOptions
		{
			Core = new GameServerCoreOptions
			{
				TimeZoneId = timeZoneId,
			},
		};
	}
}
