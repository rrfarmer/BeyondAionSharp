using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmClientCommandRoll : GameClientPacket
{
	public CmClientCommandRoll(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int MaxRoll { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_CLIENT_COMMAND_ROLL.readImpl.
		MaxRoll = buffer.ReadD();
	}
}
