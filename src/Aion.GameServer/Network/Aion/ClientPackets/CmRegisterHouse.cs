using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmRegisterHouse : GameClientPacket
{
	public CmRegisterHouse(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public long BidKinah { get; private set; }

	public long ClientFixedValue { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_REGISTER_HOUSE.readImpl.
		BidKinah = buffer.ReadQ();
		ClientFixedValue = buffer.ReadQ();
	}
}
