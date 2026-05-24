using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmInviteToGroup : GameClientPacket
{
	public CmInviteToGroup(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public byte InviteType { get; private set; }

	public string PlayerName { get; private set; } = string.Empty;

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_INVITE_TO_GROUP.readImpl.
		InviteType = buffer.ReadC();
		PlayerName = buffer.ReadS();
	}
}
