using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public sealed record SummonCreatedEmotionSnapshot(
	int SummonObjectId,
	int CreatureState,
	float MovementSpeed,
	int BaseAttackSpeed,
	int CurrentAttackSpeed
);

public enum SummonCreatePlanStatus
{
	PlanCreated,
	BlockedAlreadyHasFollower,
	BlockedInvalidSummonUpdateSnapshot,
}

public sealed record SummonCreatePlan(
	SummonCreatePlanStatus Status,
	SmSystemMessage? AlreadyHasFollowerMessage,
	SmSummonPanel? SummonPanelPacket,
	SmEmotion? ChangeSpeedEmotionPacket,
	SmSummonUpdate? SummonUpdatePacket,
	bool ShouldSendPanelToMaster,
	bool ShouldBroadcastEmotionFromSummon,
	bool ShouldBroadcastUpdateFromSummon,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class SummonCreatePlanService
{
	public static SummonCreatePlan CreatePlan(
		bool alreadyHasSummon,
		SummonPanelSnapshot? panelSnapshot,
		SummonCreatedEmotionSnapshot? emotionSnapshot,
		SummonUpdateSnapshot? updateSnapshot)
	{
		// Java parity: SummonsService.createSummon.
		// Live VisibleObjectSpawner.spawnSummon, master.setSummon, and the live Summon object lifecycle are deferred.
		if (alreadyHasSummon)
		{
			return new SummonCreatePlan(
				SummonCreatePlanStatus.BlockedAlreadyHasFollower,
				SmSystemMessage.SkillSummonAlreadyHaveAFollower(),
				SummonPanelPacket: null,
				ChangeSpeedEmotionPacket: null,
				SummonUpdatePacket: null,
				ShouldSendPanelToMaster: false,
				ShouldBroadcastEmotionFromSummon: false,
				ShouldBroadcastUpdateFromSummon: false,
				"SummonsService.createSummon -> master.getSummon() != null -> STR_SKILL_SUMMON_ALREADY_HAVE_A_FOLLOWER -> return null"
			);
		}

		SmSummonPanel? panelPacket = panelSnapshot != null ? new SmSummonPanel(panelSnapshot) : null;

		SmEmotion? emotionPacket = null;
		if (emotionSnapshot != null)
		{
			emotionPacket = new SmEmotion(
				emotionSnapshot.SummonObjectId,
				EmotionType.ChangeSpeed,
				emotionSnapshot.CreatureState,
				emotionSnapshot.MovementSpeed,
				emotionSnapshot.BaseAttackSpeed,
				emotionSnapshot.CurrentAttackSpeed);
		}

		SmSummonUpdate? updatePacket = null;
		if (updateSnapshot != null)
		{
			var updatePlan = SummonUpdatePacketPlanService.CreateBroadcastFromSummonPlan(updateSnapshot);
			if (updatePlan.Status != SummonUpdatePacketPlanStatus.PacketCreated)
			{
				return new SummonCreatePlan(
					SummonCreatePlanStatus.BlockedInvalidSummonUpdateSnapshot,
					AlreadyHasFollowerMessage: null,
					panelPacket,
					emotionPacket,
					SummonUpdatePacket: null,
					ShouldSendPanelToMaster: false,
					ShouldBroadcastEmotionFromSummon: false,
					ShouldBroadcastUpdateFromSummon: false,
					"SummonsService.createSummon -> PacketSendUtility.broadcastPacket(summon, new SM_SUMMON_UPDATE(summon)) -> invalid snapshot"
				);
			}

			updatePacket = updatePlan.Packet;
		}

		return new SummonCreatePlan(
			SummonCreatePlanStatus.PlanCreated,
			AlreadyHasFollowerMessage: null,
			panelPacket,
			emotionPacket,
			updatePacket,
			ShouldSendPanelToMaster: panelPacket != null,
			ShouldBroadcastEmotionFromSummon: emotionPacket != null,
			ShouldBroadcastUpdateFromSummon: updatePacket != null,
			"SummonsService.createSummon -> spawnSummon [live/deferred] -> SM_SUMMON_PANEL -> broadcastPacket(SM_EMOTION CHANGE_SPEED) -> broadcastPacket(SM_SUMMON_UPDATE)"
		);
	}
}
