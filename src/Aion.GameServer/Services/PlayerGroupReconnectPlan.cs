using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed record PlayerGroupReconnectResult(
	bool Reconnected,
	PlayerGroupReconnectPacketPlan? PacketPlan)
{
	public static PlayerGroupReconnectResult NotFound()
	{
		return new PlayerGroupReconnectResult(false, null);
	}
}

public sealed record PlayerGroupReconnectPacketPlan(
	int TeamId,
	int ReconnectingPlayerObjectId,
	bool SendGroupInfoToReconnectingPlayer,
	IReadOnlyList<PlayerGroupMemberInfoIntent> MemberInfoIntents);

public sealed record PlayerGroupMemberInfoIntent(
	int RecipientObjectId,
	int SubjectObjectId,
	PlayerGroupEvent Event);
