using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmAutoGroup : GameClientPacket
{
	public CmAutoGroup(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int InstanceMaskId { get; private set; }
	public byte WindowId { get; private set; }
	public byte EntryRequestId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_AUTO_GROUP.readImpl.
		InstanceMaskId = buffer.ReadD();
		WindowId = buffer.ReadC();
		EntryRequestId = buffer.ReadC();
	}
}
