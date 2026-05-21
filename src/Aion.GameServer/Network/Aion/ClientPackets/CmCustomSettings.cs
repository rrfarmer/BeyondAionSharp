using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmCustomSettings : GameClientPacket
{
	public CmCustomSettings(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int Display { get; private set; }

	public int Deny { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_CUSTOM_SETTINGS.readImpl.
		Display = buffer.ReadH();
		Deny = buffer.ReadH();
	}
}
