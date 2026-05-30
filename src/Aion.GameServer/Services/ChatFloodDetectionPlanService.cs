using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum ChatFloodDetectionPlanStatus
{
	NotFlooding,
	IsFlooding,
}

public sealed record ChatFloodDetectionPlan(
	ChatFloodDetectionPlanStatus Status,
	int CurrentFloodMsgCount,
	int FloodMsgLimit,
	bool IsFlooding,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class ChatFloodDetectionPlanService
{
	public static ChatFloodDetectionPlan CreatePlan(int currentFloodMsgCount, int floodMsgLimit)
	{
		// Java parity: services/player/PlayerChatService.isFlooding.
		// player.setLastMessageTime() is live and deferred; this only models the count comparison.
		var isFlooding = currentFloodMsgCount > floodMsgLimit;

		return new ChatFloodDetectionPlan(
			isFlooding ? ChatFloodDetectionPlanStatus.IsFlooding : ChatFloodDetectionPlanStatus.NotFlooding,
			currentFloodMsgCount,
			floodMsgLimit,
			isFlooding,
			isFlooding
				? $"PlayerChatService.isFlooding -> floodMsgCount({currentFloodMsgCount}) > FLOOD_MSG({floodMsgLimit}) -> true"
				: $"PlayerChatService.isFlooding -> floodMsgCount({currentFloodMsgCount}) <= FLOOD_MSG({floodMsgLimit}) -> false"
		);
	}
}
