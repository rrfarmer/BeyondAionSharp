using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmPlayerSpawn : GameServerPacket
{
	public const int PacketOpCode = 15;

	private readonly Player _player;

	public SmPlayerSpawn(Player player)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_PLAYER_SPAWN(Player), baseline non-personal instance 1 path.
		_player = player;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		var position = _player.Position;
		buffer.WriteD(position.WorldId);
		buffer.WriteD(position.WorldId);
		buffer.WriteD(0);
		buffer.WriteC(0);
		buffer.WriteF(position.X);
		buffer.WriteF(position.Y);
		buffer.WriteF(position.Z);
		buffer.WriteC(position.Heading);
		buffer.WriteD(0);
		buffer.WriteD(0);
		buffer.WriteD(0);
		buffer.WriteC(0);
		buffer.WriteD(0);
		buffer.WriteC(0);
	}
}
