using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum PlayerDeathEmotionFanoutPlanStatus
{
	Planned,
}

public enum PlayerDeathEmotionFanoutPlanStep
{
	NotifyDeathObservers,
	BroadcastDieEmotion,
	StopKnownCreaturesHatingOwner,
}

public sealed record PlayerDeathKnownCreatureAggroCleanupIntent(int CreatureObjectId, int HatedOwnerObjectId, bool ShouldStopHating);

public sealed record PlayerDeathEmotionFanoutPlan(
	PlayerDeathEmotionFanoutPlanStatus Status,
	int OwnerObjectId,
	int LastAttackerObjectId,
	int EmotionTargetObjectId,
	EmotionType EmotionType,
	int EmotionTypeId,
	int EmotionActionId,
	int SmEmotionPacketOpcode,
	bool UsesBroadcastPacketAndReceive,
	bool NotifiesDeathObservers,
	bool MutatesKnownCreatureAggro,
	bool SentPackets,
	IReadOnlyList<PlayerDeathKnownCreatureAggroCleanupIntent> KnownCreatureAggroCleanupIntents,
	IReadOnlyList<PlayerDeathEmotionFanoutPlanStep> Steps,
	string JavaSource,
	bool IsLive
);

public static class PlayerDeathEmotionFanoutPlanService
{
	public static PlayerDeathEmotionFanoutPlan CreatePlan(int ownerObjectId, int lastAttackerObjectId, IEnumerable<int> knownCreatureObjectIds)
	{
		// Java parity:
		// CreatureController.onDie notifies observers, broadcasts SM_EMOTION(DIE),
		// then asks every known Creature aggro list to stop hating the owner.
		var targetObjectId = ownerObjectId == lastAttackerObjectId ? 0 : lastAttackerObjectId;
		var cleanupIntents = knownCreatureObjectIds
			.Select(creatureObjectId => new PlayerDeathKnownCreatureAggroCleanupIntent(creatureObjectId, ownerObjectId, ShouldStopHating: true))
			.ToArray();

		return new PlayerDeathEmotionFanoutPlan(
			PlayerDeathEmotionFanoutPlanStatus.Planned,
			ownerObjectId,
			lastAttackerObjectId,
			targetObjectId,
			EmotionType.Die,
			(int)EmotionType.Die,
			EmotionActionId: 0,
			SmEmotion.PacketOpCode,
			UsesBroadcastPacketAndReceive: true,
			NotifiesDeathObservers: true,
			MutatesKnownCreatureAggro: false,
			SentPackets: false,
			cleanupIntents,
			new[]
			{
				PlayerDeathEmotionFanoutPlanStep.NotifyDeathObservers,
				PlayerDeathEmotionFanoutPlanStep.BroadcastDieEmotion,
				PlayerDeathEmotionFanoutPlanStep.StopKnownCreaturesHatingOwner,
			},
			"com.aionemu.gameserver.controllers.CreatureController.onDie -> notifyDeathObservers(lastAttacker); PacketSendUtility.broadcastPacketAndReceive(owner, new SM_EMOTION(owner, EmotionType.DIE, 0, owner.equals(lastAttacker) ? 0 : lastAttacker.getObjectId())); knownList creatures stopHating(owner)",
			IsLive: false
		);
	}
}
