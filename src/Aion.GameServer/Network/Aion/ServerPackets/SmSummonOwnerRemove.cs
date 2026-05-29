using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmSummonOwnerRemove : GameServerPacket
{
	public const int PacketOpCode = 154;
	private readonly int _summonObjectId;

	public SmSummonOwnerRemove(int summonObjectId) : base(PacketOpCode)
	{
		_summonObjectId = summonObjectId;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_SUMMON_OWNER_REMOVE.writeImpl.
		buffer.WriteD(_summonObjectId);
	}
}
