using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmSubzoneChange : GameClientPacket
{
	public CmSubzoneChange(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public byte Unknown { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_SUBZONE_CHANGE.readImpl.
		Unknown = buffer.ReadC();
	}
}
