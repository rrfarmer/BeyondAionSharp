using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmFriendSetMemo : GameClientPacket
{
	public CmFriendSetMemo(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public string TargetName { get; private set; } = string.Empty;

	public string Memo { get; private set; } = string.Empty;

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_FRIEND_SET_MEMO.readImpl.
		TargetName = buffer.ReadS();
		Memo = buffer.ReadS();
	}
}
