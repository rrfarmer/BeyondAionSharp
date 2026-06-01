using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmPrivateStoreName : GameClientPacket
{
	public CmPrivateStoreName(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public string StoreName { get; private set; } = string.Empty;

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_PRIVATE_STORE_NAME.readImpl.
		StoreName = buffer.ReadS();
	}
}
