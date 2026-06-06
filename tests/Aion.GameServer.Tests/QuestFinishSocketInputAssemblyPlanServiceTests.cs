using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class QuestFinishSocketInputAssemblyPlanServiceTests
{
	private const int SelectedQuestReward1 = 8;
	private const int SelectedQuestNoReward = 23;
	private const int SelectedQuestAutoReward = 108;

	[Fact]
	public void CreatePlan_PreparesRewardProjectionForReportableAutoRewardWithoutExecutingFinish()
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
			Quests = [new PlayerQuestState(1001, "REWARD", QuestVars: 0, Flags: 0, CompleteCount: 0)],
		};

		var plan = QuestFinishSocketInputAssemblyPlanService.CreatePlan(
			player,
			CreateDialogSelect(SelectedQuestAutoReward, questId: 1001),
			table);

		Assert.Equal(QuestFinishSocketInputAssemblyStatus.Ready, plan.Status);
		Assert.Equal(QuestFinishRewardGroupCorrectionStatus.DefaultedFirstRewardGroup, plan.RewardGroupCorrectionStatus);
		Assert.Equal(0, plan.QuestState?.RewardGroup);
		Assert.Equal(55, plan.RewardProjection?.NonItemProjection?.Kinah);
		Assert.Equal(SelectedQuestNoReward, plan.RewardProjection?.DialogActionId);
		Assert.Equal("RANGER", plan.RewardProjection?.PlayerClass);
		Assert.Empty(plan.Diagnostics);
	}

	[Fact]
	public void CreatePlan_RejectsNonAutoRewardDialogActionsBeforeProjectionLookup()
	{
		var plan = QuestFinishSocketInputAssemblyPlanService.CreatePlan(
			new Player(),
			CreateDialogSelect(SelectedQuestReward1, questId: 1001),
			new QuestFinishRewardProjectionLookupTable([]));

		Assert.Equal(QuestFinishSocketInputAssemblyStatus.NotQuestAutoRewardAction, plan.Status);
		Assert.Null(plan.QuestState);
		Assert.Null(plan.RewardProjection);
	}

	[Fact]
	public void CreatePlan_ReturnsMissingQuestStateBeforeRewardProjection()
	{
		var table = CreateLookupTable("""<quests><quest id="1001"><rewards gold="1" /></quest></quests>""");

		var plan = QuestFinishSocketInputAssemblyPlanService.CreatePlan(
			new Player(),
			CreateDialogSelect(SelectedQuestAutoReward, questId: 1001),
			table);

		Assert.Equal(QuestFinishSocketInputAssemblyStatus.MissingQuestState, plan.Status);
		Assert.Null(plan.QuestState);
		Assert.Null(plan.RewardProjection);
	}

	[Fact]
	public void CreatePlan_ReturnsQuestStateNotRewardLikeJavaFinishGuard()
	{
		var table = CreateLookupTable("""<quests><quest id="1001"><rewards gold="1" /></quest></quests>""");
		var questState = new PlayerQuestState(1001, "START", QuestVars: 0, Flags: 0, CompleteCount: 0);
		var player = new Player { Quests = [questState] };

		var plan = QuestFinishSocketInputAssemblyPlanService.CreatePlan(
			player,
			CreateDialogSelect(SelectedQuestAutoReward, questId: 1001),
			table);

		Assert.Equal(QuestFinishSocketInputAssemblyStatus.QuestStateNotReward, plan.Status);
		Assert.Same(questState, plan.QuestState);
		Assert.Null(plan.RewardProjection);
	}

	[Fact]
	public void CreatePlan_ReturnsMissingRewardProjectionWhenStaticLookupHasNoQuest()
	{
		var player = new Player
		{
			PlayerClass = "RANGER",
			Quests = [new PlayerQuestState(1001, "REWARD", QuestVars: 0, Flags: 0, CompleteCount: 0)],
		};

		var plan = QuestFinishSocketInputAssemblyPlanService.CreatePlan(
			player,
			CreateDialogSelect(SelectedQuestAutoReward, questId: 1001),
			new QuestFinishRewardProjectionLookupTable([]));

		Assert.Equal(QuestFinishSocketInputAssemblyStatus.MissingRewardProjection, plan.Status);
		Assert.Equal(QuestFinishRewardProjectionLookupStatus.MissingQuestTemplate, plan.RewardProjectionLookupStatus);
		Assert.Equal(QuestFinishRewardGroupCorrectionStatus.Unchanged, plan.RewardGroupCorrectionStatus);
		Assert.Equal(1001, plan.QuestState?.QuestId);
		Assert.Null(plan.QuestState?.RewardGroup);
		Assert.Null(plan.RewardProjection);
	}

	[Fact]
	public void CreatePlan_ReturnsProjectionDiagnosticsForMissingPlayerClass()
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
		var player = new Player
		{
			PlayerClass = "",
			Quests = [new PlayerQuestState(1002, "REWARD", QuestVars: 0, Flags: 0, CompleteCount: 0)],
		};

		var plan = QuestFinishSocketInputAssemblyPlanService.CreatePlan(
			player,
			CreateDialogSelect(SelectedQuestAutoReward, questId: 1002),
			table);

		Assert.Equal(QuestFinishSocketInputAssemblyStatus.ProjectionDiagnostics, plan.Status);
		Assert.Contains(QuestFinishRewardProjectionLookupDiagnostic.MissingPlayerClassForClassSelectableReward, plan.Diagnostics);
		Assert.NotNull(plan.RewardProjection);
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
}
