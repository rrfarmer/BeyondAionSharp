using Aion.Commons.Network;
using Aion.GameServer.Model.Legion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmLegionUpdateTitle : GameServerPacket
{
	public const int PacketOpCode = 114; // Java parity: ServerPacketsOpcodes addPacketOpcode(114, SM_LEGION_UPDATE_TITLE.class).

	private readonly int _playerObjectId;
	private readonly int _legionId;
	private readonly string _legionName;
	private readonly string _rank;

	public SmLegionUpdateTitle(int playerObjectId, int legionId, string legionName, string rank)
		: base(PacketOpCode)
	{
		_playerObjectId = playerObjectId;
		_legionId = legionId;
		_legionName = legionName;
		_rank = rank;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_LEGION_UPDATE_TITLE.writeImpl.
		buffer.WriteD(_playerObjectId);
		buffer.WriteD(_legionId);
		buffer.WriteS(_legionName);
		buffer.WriteC(LegionRanks.GetRankId(_rank));
	}
}
