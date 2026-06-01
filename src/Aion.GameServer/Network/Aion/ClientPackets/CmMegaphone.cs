using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmMegaphone : GameClientPacket
{
	public CmMegaphone(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public string Message { get; private set; } = string.Empty;
	public int ItemObjectId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_MEGAPHONE.readImpl.
		Message = buffer.ReadS();
		ItemObjectId = buffer.ReadD();
	}
}
