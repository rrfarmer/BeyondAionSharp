using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmUpgradeArcade : GameClientPacket
{
	public CmUpgradeArcade(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public byte Action { get; private set; }
	public int SessionId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_UPGRADE_ARCADE.readImpl.
		Action = buffer.ReadC();
		SessionId = buffer.ReadD();
	}
}
