using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmBuilderCommand : GameClientPacket
{
	public CmBuilderCommand(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public string Command { get; private set; } = string.Empty;

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/AbstractGmCommandPacket.readImpl (command = readS()).
		Command = buffer.ReadS();
	}
}
