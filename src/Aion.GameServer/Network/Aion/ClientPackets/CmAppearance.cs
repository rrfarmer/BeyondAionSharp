using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmAppearance : GameClientPacket
{
	public CmAppearance(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public byte Type { get; private set; }

	public int ItemObjectId { get; private set; }

	public string NewName { get; private set; } = string.Empty;

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_APPEARANCE.readImpl.
		Type = buffer.ReadC();
		buffer.ReadC();
		buffer.ReadH();
		ItemObjectId = buffer.ReadD();
		if (Type is 0 or 1)
			NewName = buffer.ReadS();
	}
}
