using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmAbyssRankUpdate : GameServerPacket
{
	public const int PacketOpCode = 136;

	private readonly int _action;
	private readonly int _playerObjectId;
	private readonly int _value;

	private SmAbyssRankUpdate(int action, int playerObjectId, int value)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_ABYSS_RANK_UPDATE(int, Player).
		_action = action;
		_playerObjectId = playerObjectId;
		_value = value;
	}

	public static SmAbyssRankUpdate RankChange(Player player)
	{
		return new SmAbyssRankUpdate(0, player.ObjectId, player.AbyssRank.Rank);
	}

	public static SmAbyssRankUpdate TeamObjectId(int playerObjectId, int teamObjectId)
	{
		return new SmAbyssRankUpdate(1, playerObjectId, teamObjectId);
	}

	public static SmAbyssRankUpdate MentorStatusChange(int playerObjectId, bool isMentor)
	{
		return new SmAbyssRankUpdate(2, playerObjectId, isMentor ? 1 : 0);
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_ABYSS_RANK_UPDATE.writeImpl.
		buffer.WriteC(_action);
		buffer.WriteD(_playerObjectId);
		buffer.WriteD(_value);
	}
}
