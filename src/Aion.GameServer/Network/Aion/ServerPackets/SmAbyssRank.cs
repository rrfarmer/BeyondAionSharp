using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmAbyssRank : GameServerPacket
{
	public const int PacketOpCode = 237;

	private readonly PlayerAbyssRank _rank;

	public SmAbyssRank(PlayerAbyssRank rank)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_ABYSS_RANK(Player).
		_rank = rank;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_ABYSS_RANK.writeImpl.
		buffer.WriteQ(_rank.Ap);
		buffer.WriteD(_rank.Gp);
		buffer.WriteD(_rank.Rank);
		buffer.WriteD(_rank.RankingListPosition);
		buffer.WriteD(0);
		buffer.WriteD(_rank.AllKill);
		buffer.WriteD(_rank.MaxRank);
		buffer.WriteD(_rank.DailyKill);
		buffer.WriteQ(_rank.DailyAp);
		buffer.WriteD(_rank.DailyGp);
		buffer.WriteD(_rank.WeeklyKill);
		buffer.WriteQ(_rank.WeeklyAp);
		buffer.WriteD(_rank.WeeklyGp);
		buffer.WriteD(_rank.LastKill);
		buffer.WriteQ(_rank.LastAp);
		buffer.WriteD(_rank.LastGp);
		buffer.WriteC(0);
	}
}
