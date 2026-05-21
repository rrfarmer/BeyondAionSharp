using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmFriendAdd : GameClientPacket
{
	public CmFriendAdd(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public string TargetName { get; private set; } = string.Empty;

	public string Message { get; private set; } = string.Empty;

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_FRIEND_ADD.readImpl.
		TargetName = buffer.ReadS();
		Message = buffer.ReadS();
	}
}
