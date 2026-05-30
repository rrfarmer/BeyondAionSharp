using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum ClassChangeSetClassBroadcastPlanStatus
{
	PlanCreated,
}

public sealed record ClassChangeSetClassBroadcastPlan(
	ClassChangeSetClassBroadcastPlanStatus Status,
	int PlayerObjectId,
	int PlayerLevel,
	SmActionAnimation ClassChangeAnimationPacket,
	IReadOnlyList<GameServerPacket> BroadcastPacketsInOrder,
	bool ShouldBroadcastAnimation,
	bool ShouldBroadcastPlayerInfo,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class ClassChangeSetClassBroadcastPlanService
{
	public static ClassChangeSetClassBroadcastPlan CreatePlan(int playerObjectId, int playerLevel)
	{
		// Java parity: services/ClassChangeService.setClass broadcast effects (after live state mutation).
		// Live player.getCommonData().setPlayerClass, updateStatsTemplate, upgradePlayer, learnNewSkills
		// and updateDaeva side effects are deferred.
		var classChangeAnimationPacket = new SmActionAnimation(
			playerObjectId,
			SmActionAnimation.ClassChange,
			playerLevel);

		return new ClassChangeSetClassBroadcastPlan(
			ClassChangeSetClassBroadcastPlanStatus.PlanCreated,
			playerObjectId,
			playerLevel,
			classChangeAnimationPacket,
			[classChangeAnimationPacket],
			ShouldBroadcastAnimation: true,
			ShouldBroadcastPlayerInfo: true,
			$"ClassChangeService.setClass -> broadcastPacket(SM_ACTION_ANIMATION CLASS_CHANGE level={playerLevel}, true) -> broadcastPacket(SM_PLAYER_INFO, true) [SM_PLAYER_INFO requires live player state]"
		);
	}
}
