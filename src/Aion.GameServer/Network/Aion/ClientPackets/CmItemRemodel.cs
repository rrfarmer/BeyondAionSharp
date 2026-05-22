using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmItemRemodel : GameClientPacket
{
	public CmItemRemodel(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int NpcObjectId { get; private set; }

	public int KeepItemObjectId { get; private set; }

	public int ExtractItemObjectId { get; private set; }

	public int Unknown { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_ITEM_REMODEL.readImpl.
		NpcObjectId = buffer.ReadD();
		KeepItemObjectId = buffer.ReadD();
		ExtractItemObjectId = buffer.ReadD();
		Unknown = buffer.ReadD();
	}
}
