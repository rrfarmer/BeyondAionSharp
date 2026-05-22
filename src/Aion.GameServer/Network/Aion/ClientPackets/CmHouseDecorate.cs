using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmHouseDecorate : GameClientPacket
{
	public CmHouseDecorate(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int ObjectId { get; private set; }

	public int TemplateId { get; private set; }

	public int LineNumber { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_HOUSE_DECORATE.readImpl.
		ObjectId = buffer.ReadD();
		TemplateId = buffer.ReadD();
		LineNumber = buffer.ReadH();
	}
}
