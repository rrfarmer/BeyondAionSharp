using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmChatGroupInfo : GameClientPacket
{
	public CmChatGroupInfo(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public string PlayerName { get; private set; } = string.Empty;

	public int Unknown { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_CHAT_GROUP_INFO.readImpl.
		PlayerName = buffer.ReadS();
		Unknown = buffer.ReadD();
	}
}
