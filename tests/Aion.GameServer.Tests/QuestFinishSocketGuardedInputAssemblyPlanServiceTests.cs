using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class QuestFinishSocketGuardedInputAssemblyPlanServiceTests
{
	private const int PlayerObjectId = 42;
	private const int NpcObjectId = 9001;
	private const int SelectedQuestReward1 = 8;
	private const int SelectedQuestAutoReward = 108;

	[Theory]
	[InlineData(0)]
	[InlineData(PlayerObjectId)]
	public void CreatePlan_AssemblesInputOnlyAfterJavaSelfTargetReportableGuard(int targetObjectId)
	{
		var player = new Player
		{
			ObjectId = PlayerObjectId,
			PlayerClass = "RANGER",
			Quests = [new PlayerQuestState(1001, "REWARD", QuestVars: 0, Flags: 0, CompleteCount: 0)],
		};

		var plan = QuestFinishSocketGuardedInputAssemblyPlanService.CreatePlan(
			player,
			CreateDialogSelect(targetObjectId, SelectedQuestAutoReward, questId: 1001),
			CreateLookupTable("""
				<quests>
					<quest id="1001" can_report="true" reward_repeat_count="1">
						<rewards gold="55" />
					</quest>
				</quests>
				"""));

		Assert.Equal(QuestFinishSocketGuardedInputAssemblyStatus.InputAssembled, plan.Status);
		Assert.Equal(QuestDialogAutoRewardGuardStatus.Planned, plan.GuardPlan.Status);
		Assert.NotNull(plan.GuardPlan.StaticMetadata);
		Assert.NotNull(plan.InputPlan);
		Assert.Equal(QuestFinishSocketInputAssemblyStatus.Ready, plan.InputPlan.Status);
		Assert.Equal(55, plan.InputPlan.RewardProjection?.NonItemProjection?.Kinah);
	}

	[Fact]
	public void CreatePlan_RejectsNpcTargetBeforeRewardProjectionLikeJavaBranchSplit()
	{
		var plan = QuestFinishSocketGuardedInputAssemblyPlanService.CreatePlan(
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
				"""));

		Assert.Equal(QuestFinishSocketGuardedInputAssemblyStatus.GuardRejected, plan.Status);
		Assert.Equal(QuestDialogAutoRewardGuardStatus.NonSelfTarget, plan.GuardPlan.Status);
		Assert.Null(plan.GuardPlan.StaticMetadata);
		Assert.Null(plan.InputPlan);
	}

	[Fact]
	public void CreatePlan_RejectsMissingQuestTemplateBeforeInputAssembly()
	{
		var plan = QuestFinishSocketGuardedInputAssemblyPlanService.CreatePlan(
			new Player
			{
				ObjectId = PlayerObjectId,
				PlayerClass = "RANGER",
				Quests = [new PlayerQuestState(1001, "REWARD", QuestVars: 0, Flags: 0, CompleteCount: 0)],
			},
			CreateDialogSelect(0, SelectedQuestAutoReward, questId: 1001),
			new QuestFinishRewardProjectionLookupTable([]));

		Assert.Equal(QuestFinishSocketGuardedInputAssemblyStatus.GuardRejected, plan.Status);
		Assert.Equal(QuestDialogAutoRewardGuardStatus.MissingQuestTemplate, plan.GuardPlan.Status);
		Assert.Null(plan.InputPlan);
	}

	[Fact]
	public void CreatePlan_RejectsNonReportableQuestBeforeInputAssembly()
	{
		var plan = QuestFinishSocketGuardedInputAssemblyPlanService.CreatePlan(
			new Player
			{
				ObjectId = PlayerObjectId,
				PlayerClass = "RANGER",
				Quests = [new PlayerQuestState(1001, "REWARD", QuestVars: 0, Flags: 0, CompleteCount: 0)],
			},
			CreateDialogSelect(0, SelectedQuestAutoReward, questId: 1001),
			CreateLookupTable("""
				<quests>
					<quest id="1001" can_report="false">
						<rewards gold="55" />
					</quest>
				</quests>
				"""));

		Assert.Equal(QuestFinishSocketGuardedInputAssemblyStatus.GuardRejected, plan.Status);
		Assert.Equal(QuestDialogAutoRewardGuardStatus.NotReportableQuest, plan.GuardPlan.Status);
		Assert.NotNull(plan.GuardPlan.StaticMetadata);
		Assert.Null(plan.InputPlan);
	}

	[Fact]
	public void CreatePlan_RejectsNonAutoRewardActionBeforeInputAssembly()
	{
		var plan = QuestFinishSocketGuardedInputAssemblyPlanService.CreatePlan(
			new Player
			{
				ObjectId = PlayerObjectId,
				PlayerClass = "RANGER",
				Quests = [new PlayerQuestState(1001, "REWARD", QuestVars: 0, Flags: 0, CompleteCount: 0)],
			},
			CreateDialogSelect(0, SelectedQuestReward1, questId: 1001),
			CreateLookupTable("""
				<quests>
					<quest id="1001" can_report="true">
						<rewards gold="55" />
					</quest>
				</quests>
				"""));

		Assert.Equal(QuestFinishSocketGuardedInputAssemblyStatus.GuardRejected, plan.Status);
		Assert.Equal(QuestDialogAutoRewardGuardStatus.NotAutoRewardDialogAction, plan.GuardPlan.Status);
		Assert.NotNull(plan.GuardPlan.StaticMetadata);
		Assert.Null(plan.InputPlan);
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
}
