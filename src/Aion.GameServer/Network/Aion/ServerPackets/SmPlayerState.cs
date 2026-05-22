using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmPlayerState : GameServerPacket
{
	public const int PacketOpCode = 68;

	private readonly int _playerObjectId;
	private readonly int _visualState;
	private readonly int _seeState;

	public SmPlayerState(Player player)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_PLAYER_STATE(Creature).
		_playerObjectId = player.ObjectId;
		_visualState = player.VisualState;
		_seeState = player.SeeState;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_PLAYER_STATE.writeImpl.
		buffer.WriteD(_playerObjectId);
		buffer.WriteC(_visualState);
		buffer.WriteC(_seeState);
		buffer.WriteC(_visualState == PlayerVisualStates.Blinking ? 0x01 : 0x00);
	}
}
