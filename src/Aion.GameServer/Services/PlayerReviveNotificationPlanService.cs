using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum PlayerReviveType
{
	Duel,
	Skill,
	Rebirth,
	ItemSelf,
	Kisk,
}

public enum PlayerReviveNotificationPlanStatus
{
	WithRebirthMessage,
	NoRebirthMessage,
}

public sealed record PlayerReviveNotificationPlan(
	PlayerReviveNotificationPlanStatus Status,
	PlayerReviveType ReviveType,
	SmSystemMessage? RebirthMessage,
	bool ShouldSendRebirthMessage,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class PlayerReviveNotificationPlanService
{
	public static PlayerReviveNotificationPlan CreatePlan(PlayerReviveType reviveType)
	{
		// Java parity: services/player/PlayerReviveService - STR_REBIRTH_MASSAGE_ME is sent for
		// skillRevive, rebirthRevive, itemSelfRevive, and kiskRevive, but NOT for duelRevive.
		var sendRebirthMessage = reviveType != PlayerReviveType.Duel;

		if (!sendRebirthMessage)
		{
			return new PlayerReviveNotificationPlan(
				PlayerReviveNotificationPlanStatus.NoRebirthMessage,
				reviveType,
				RebirthMessage: null,
				ShouldSendRebirthMessage: false,
				$"PlayerReviveService.{reviveType.ToString().ToLowerInvariant()}Revive -> no STR_REBIRTH_MASSAGE_ME (duelRevive does not send it)"
			);
		}

		return new PlayerReviveNotificationPlan(
			PlayerReviveNotificationPlanStatus.WithRebirthMessage,
			reviveType,
			SmSystemMessage.RebirthMassageMe(),
			ShouldSendRebirthMessage: true,
			$"PlayerReviveService.{reviveType.ToString().ToLowerInvariant()}Revive -> sendPacket(STR_REBIRTH_MASSAGE_ME())"
		);
	}
}
