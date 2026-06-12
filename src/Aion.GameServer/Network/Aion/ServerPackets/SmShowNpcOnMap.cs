using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmShowNpcOnMap : GameServerPacket
{
	public const int PacketOpCode = 89;

	private readonly int _npcId;
	private readonly int _worldId;
	private readonly int _instanceId;
	private readonly float _x;
	private readonly float _y;
	private readonly float _z;

	public SmShowNpcOnMap(Player player, int npcId, int spawnWorldId, float x, float y, float z)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_SHOW_NPC_ON_MAP.writeImpl.
		_npcId = npcId;
		_worldId = spawnWorldId;
		_x = x;
		_y = y;
		_z = z;

		// Java parity: instanceId adjusted for channel vs instance.
		// If spawn is on same map as player: use player's instance/channel; otherwise use spawn worldId as-is.
		if (player.GetPosition().WorldId == spawnWorldId)
		{
			var worldMap = player.GetPosition();
			_instanceId = worldMap.InstanceId > 1
				? worldMap.InstanceId  // player is in a personal/shared instance
				: spawnWorldId + worldMap.InstanceId - 1; // mapid + channelId (instanceId-1)
		}
		else
		{
			_instanceId = spawnWorldId;
		}
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: SM_SHOW_NPC_ON_MAP.writeImpl: writeD(npcid) writeD(worldid) writeD(instanceId) writeF(x) writeF(y) writeF(z).
		buffer.WriteD(_npcId);
		buffer.WriteD(_worldId);
		buffer.WriteD(_instanceId);
		buffer.WriteF(_x);
		buffer.WriteF(_y);
		buffer.WriteF(_z);
	}
}
