using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmDistributionSettings : GameClientPacket
{
	public CmDistributionSettings(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	// Java parity: CM_DISTRIBUTION_SETTINGS.readImpl fields.
	public int LootRule { get; private set; } // 0=FreeForAll, 1=RoundRobin, 2=Leader
	public int Misc { get; private set; }
	public int CommonItemAbove { get; private set; }
	public int SuperiorItemAbove { get; private set; }
	public int HeroicItemAbove { get; private set; }
	public int FabledItemAbove { get; private set; }
	public int EthernalItemAbove { get; private set; }
	public int MythicItemAbove { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_DISTRIBUTION_SETTINGS.readImpl.
		buffer.ReadD(); // isLeague (unused)
		LootRule = buffer.ReadD();
		Misc = buffer.ReadD();
		CommonItemAbove = buffer.ReadD();
		SuperiorItemAbove = buffer.ReadD();
		HeroicItemAbove = buffer.ReadD();
		FabledItemAbove = buffer.ReadD();
		EthernalItemAbove = buffer.ReadD();
		MythicItemAbove = buffer.ReadD();
		buffer.ReadD(); // unk
	}
}
