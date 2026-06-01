using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmWindstream : GameClientPacket
{
	public CmWindstream(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int TeleportId { get; private set; }
	public int Distance { get; private set; }
	public int State { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_WINDSTREAM.readImpl.
		TeleportId = buffer.ReadD();
		Distance = buffer.ReadD();
		State = buffer.ReadD();
	}
}
