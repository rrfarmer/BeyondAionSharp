using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmHouseKick : GameClientPacket
{
	public CmHouseKick(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public byte Option { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_HOUSE_KICK.readImpl.
		Option = buffer.ReadC();
		_ = buffer.ReadH();
	}
}
