using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmHouseTeleport : GameClientPacket
{
	public CmHouseTeleport(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	// Java parity: CM_HOUSE_TELEPORT.readImpl: actionId + playerId1 (self) + playerId2 (friend target).
	// actionId: 1=own house, 2=friend's house, 3=random friend's house.
	public byte ActionId { get; private set; }
	public int PlayerId1 { get; private set; }
	public int PlayerId2 { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_HOUSE_TELEPORT.readImpl.
		ActionId = buffer.ReadC();
		PlayerId1 = buffer.ReadD();
		PlayerId2 = buffer.ReadD();
	}
}
