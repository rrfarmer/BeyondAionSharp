using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmChatMessageWhisper : GameClientPacket
{
	public CmChatMessageWhisper(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public string RecipientName { get; private set; } = string.Empty;

	public string Message { get; private set; } = string.Empty;

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_CHAT_MESSAGE_WHISPER.readImpl.
		RecipientName = buffer.ReadS();
		Message = buffer.ReadS();
	}
}
