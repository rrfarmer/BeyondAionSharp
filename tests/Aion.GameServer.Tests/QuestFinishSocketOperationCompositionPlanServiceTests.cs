using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class QuestFinishSocketOperationCompositionPlanServiceTests
{
	private const int SelectedQuestReward1 = 8;
	private const int SelectedQuestAutoReward = 108;

	[Fact]
	public void CreatePlan_ComposesReadySocketInputIntoNonLiveOperationPlan()
	{
		var table = CreateLookupTable("""
			<quests>
				<quest id="1001" can_report="true" reward_repeat_count="2">
					<rewards gold="55">
						<reward_item item_id="182400001" count="1" />
					</rewards>
				</quest>
			</quests>
			""");
		var player = new Player
		{
			PlayerClass = "RANGER",
			Quests = [new PlayerQuestState(1001, "REWARD", QuestVars: 0x12, Flags: 0, CompleteCount: 0)],
		};
		var inputPlan = QuestFinishSocketInputAssemblyPlanService.CreatePlan(
			player,
			CreateDialogSelect(SelectedQuestAutoReward, questId: 1001),
			table);

		var plan = QuestFinishSocketOperationCompositionPlanService.CreatePlan(
			inputPlan,
			PlayerNpcFactionsSnapshot.Empty,
			new DateTimeOffset(2026, 5, 26, 9, 0, 0, TimeSpan.Zero),
			CreateOptions("UTC"));

		Assert.Equal(QuestFinishSocketOperationCompositionStatus.Composed, plan.Status);
		Assert.Same(inputPlan, plan.InputPlan);
		Assert.NotNull(plan.OperationPlan);
		Assert.True(plan.OperationPlan.Applied);
		Assert.Equal("COMPLETE", plan.OperationPlan.QuestState?.Status);
		Assert.Equal(1, plan.OperationPlan.QuestState?.CompleteCount);
		Assert.All(plan.OperationPlan.Descriptors, descriptor => Assert.False(descriptor.IsLive));
		Assert.DoesNotContain(
			plan.OperationPlan.Descriptors,
			descriptor => descriptor.Action == QuestFinishOperationAction.RewardMutationPlaceholder);
		Assert.Contains(
			plan.OperationPlan.Descriptors,
			descriptor => descriptor.Action == QuestFinishOperationAction.NonItemRewardProjection
				&& descriptor.Count == 55);
		Assert.Contains(
			plan.OperationPlan.Descriptors,
			descriptor => descriptor.Action == QuestFinishOperationAction.ItemRewardProjection
				&& descriptor.ItemId == 182400001);
		Assert.Contains(
			plan.OperationPlan.Descriptors,
			descriptor => descriptor.Action == QuestFinishOperationAction.QuestStateMutation);
		Assert.True(
			IndexOf(plan.OperationPlan.Descriptors, QuestFinishOperationAction.NonItemRewardProjection)
				< IndexOf(plan.OperationPlan.Descriptors, QuestFinishOperationAction.QuestStateMutation));
	}

	[Fact]
	public void CreatePlan_DoesNotComposeNonReadySocketInput()
	{
		var inputPlan = QuestFinishSocketInputAssemblyPlanService.CreatePlan(
			new Player(),
			CreateDialogSelect(SelectedQuestReward1, questId: 1001),
			new QuestFinishRewardProjectionLookupTable([]));

		var plan = QuestFinishSocketOperationCompositionPlanService.CreatePlan(
			inputPlan,
			PlayerNpcFactionsSnapshot.Empty,
			new DateTimeOffset(2026, 5, 26, 9, 0, 0, TimeSpan.Zero),
			CreateOptions("UTC"));

		Assert.Equal(QuestFinishSocketOperationCompositionStatus.InputNotReady, plan.Status);
		Assert.Same(inputPlan, plan.InputPlan);
		Assert.Null(plan.OperationPlan);
	}

	[Fact]
	public void CreatePlan_DoesNotComposeProjectionDiagnostics()
	{
		var table = CreateLookupTable("""
			<quests>
				<quest id="1002" can_report="true" use_class_reward="2" reward_repeat_count="1">
					<rewards>
						<selectable_reward_item item_id="182400001" count="1" />
					</rewards>
					<ranger_selectable_reward item_id="100900001" count="2" />
				</quest>
			</quests>
			""");
		var inputPlan = QuestFinishSocketInputAssemblyPlanService.CreatePlan(
			new Player
			{
				PlayerClass = "",
				Quests = [new PlayerQuestState(1002, "REWARD", QuestVars: 0, Flags: 0, CompleteCount: 0)],
			},
			CreateDialogSelect(SelectedQuestAutoReward, questId: 1002),
			table);

		var plan = QuestFinishSocketOperationCompositionPlanService.CreatePlan(
			inputPlan,
			PlayerNpcFactionsSnapshot.Empty,
			new DateTimeOffset(2026, 5, 26, 9, 0, 0, TimeSpan.Zero),
			CreateOptions("UTC"));

		Assert.Equal(QuestFinishSocketInputAssemblyStatus.ProjectionDiagnostics, inputPlan.Status);
		Assert.Equal(QuestFinishSocketOperationCompositionStatus.InputNotReady, plan.Status);
		Assert.Null(plan.OperationPlan);
	}

	private static QuestFinishRewardProjectionLookupTable CreateLookupTable(string xml)
	{
		return new QuestFinishRewardProjectionLookupTableXmlFactory().Create(xml);
	}

	private static CmDialogSelect CreateDialogSelect(int dialogActionId, int questId)
	{
		using var writer = new PacketBuffer();
		writer.WriteD(0);
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

	private static int IndexOf(
		IReadOnlyList<QuestFinishOperationDescriptor> descriptors,
		QuestFinishOperationAction action)
	{
		for (var i = 0; i < descriptors.Count; i++)
		{
			if (descriptors[i].Action == action)
			{
				return i;
			}
		}

		return -1;
	}
}
