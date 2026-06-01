using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmFusionWeapons : GameClientPacket
{
	public CmFusionWeapons(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int NpcObjectId { get; private set; }
	public int MainWeaponObjectId { get; private set; }
	public int FuseWeaponObjectId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_FUSION_WEAPONS.readImpl.
		NpcObjectId = buffer.ReadD();
		MainWeaponObjectId = buffer.ReadD();
		FuseWeaponObjectId = buffer.ReadD();
	}
}
