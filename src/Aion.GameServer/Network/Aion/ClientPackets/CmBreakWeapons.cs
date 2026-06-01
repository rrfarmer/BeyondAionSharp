using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmBreakWeapons : GameClientPacket
{
	public CmBreakWeapons(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int NpcObjectId { get; private set; }
	public int WeaponObjectId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_BREAK_WEAPONS.readImpl.
		NpcObjectId = buffer.ReadD();
		WeaponObjectId = buffer.ReadD();
	}
}
