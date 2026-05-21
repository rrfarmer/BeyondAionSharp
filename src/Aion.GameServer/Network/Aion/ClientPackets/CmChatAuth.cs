using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmChatAuth : GameClientPacket
{
	public CmChatAuth(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int ObjectId { get; private set; }

	public byte[] MacAddress { get; private set; } = Array.Empty<byte>();

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_CHAT_AUTH.readImpl.
		ObjectId = buffer.ReadD();
		MacAddress = buffer.ReadB(6);
	}
}
