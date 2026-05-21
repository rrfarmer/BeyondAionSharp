using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmChatMessagePublic : GameClientPacket
{
	public CmChatMessagePublic(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public byte ChatType { get; private set; }

	public string Message { get; private set; } = string.Empty;

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_CHAT_MESSAGE_PUBLIC.readImpl.
		ChatType = buffer.ReadC();
		Message = buffer.ReadS();
	}
}
