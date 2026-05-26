using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmPlayerStance : GameServerPacket
{
	public const int PacketOpCode = 31;
	private readonly int _playerObjectId;
	private readonly int _state;

	public SmPlayerStance(Player player, int state)
		: this(player.ObjectId, state)
	{
	}

	public SmPlayerStance(int playerObjectId, int state)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_PLAYER_STANCE(Player, int).
		_playerObjectId = playerObjectId;
		_state = state;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: SM_PLAYER_STANCE.writeImpl writes object id then state.
		buffer.WriteD(_playerObjectId);
		buffer.WriteC(_state);
	}
}
