using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmTargetUpdate : GameServerPacket
{
	public const int PacketOpCode = 81;

	private readonly int _playerObjectId;
	private readonly int _targetObjectId;

	public SmTargetUpdate(Player player)
		: this(player.ObjectId, player.TargetObjectId)
	{
		// Java parity: network/aion/serverpackets/SM_TARGET_UPDATE(Player).
	}

	public SmTargetUpdate(int playerObjectId, int targetObjectId)
		: base(PacketOpCode)
	{
		_playerObjectId = playerObjectId;
		_targetObjectId = targetObjectId;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: SM_TARGET_UPDATE.writeImpl writes player id and target id,
		// using 0 when player.getTarget() is null.
		buffer.WriteD(_playerObjectId);
		buffer.WriteD(_targetObjectId);
	}
}
