using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmChatPlayerInfo : GameClientPacket
{
	public CmChatPlayerInfo(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public string PlayerName { get; private set; } = string.Empty;

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_CHAT_PLAYER_INFO.readImpl.
		PlayerName = buffer.ReadS();
	}
}
