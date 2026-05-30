using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum ChatBanRemainingMinutesPlanStatus
{
	NotBanned,
	BanExpired,
	BanActive,
}

public sealed record ChatBanRemainingMinutesPlan(
	ChatBanRemainingMinutesPlanStatus Status,
	int RemainingMinutes,
	SmSystemMessage? CanChatNowMessage,
	bool ShouldSendCanChatNow,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class ChatBanRemainingMinutesPlanService
{
	public static ChatBanRemainingMinutesPlan CreatePlan(long? banExpirationMillis, long currentTimeMillis)
	{
		// Java parity: services/ban/ChatBanService.getBanMinutes.
		// Live unbanPlayer side effects (task cancellation, ChatServer gag packet) are deferred.
		if (banExpirationMillis == null)
		{
			return new ChatBanRemainingMinutesPlan(
				ChatBanRemainingMinutesPlanStatus.NotBanned,
				RemainingMinutes: 0,
				CanChatNowMessage: null,
				ShouldSendCanChatNow: false,
				"ChatBanService.getBanMinutes -> chatBans.get(player.getObjectId()) == null -> return 0"
			);
		}

		var millisLeft = banExpirationMillis.Value - currentTimeMillis;
		if (millisLeft <= 0)
		{
			return new ChatBanRemainingMinutesPlan(
				ChatBanRemainingMinutesPlanStatus.BanExpired,
				RemainingMinutes: 0,
				SmSystemMessage.CanChatNow(),
				ShouldSendCanChatNow: true,
				"ChatBanService.getBanMinutes -> millisLeft <= 0 -> unbanPlayer -> STR_MSG_CAN_CHAT_NOW -> return 0"
			);
		}

		var remainingMinutes = (int)Math.Max(0, Math.Ceiling(millisLeft / 60000.0));

		return new ChatBanRemainingMinutesPlan(
			ChatBanRemainingMinutesPlanStatus.BanActive,
			remainingMinutes,
			CanChatNowMessage: null,
			ShouldSendCanChatNow: false,
			$"ChatBanService.getBanMinutes -> Math.max(0, Math.ceil({millisLeft} / 60000f)) = {remainingMinutes}"
		);
	}
}
