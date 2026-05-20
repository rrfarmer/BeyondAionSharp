using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.World;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmChannelInfo : GameServerPacket
{
	public const int PacketOpCode = 229;

	private readonly int _currentChannel;
	private readonly int _instanceCount;

	public SmChannelInfo(WorldPosition position, IReadOnlyList<WorldMapSummary> worldMaps)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_CHANNEL_INFO(WorldPosition).
		var worldMap = worldMaps.FirstOrDefault(map => map.MapId == position.WorldId);
		if (worldMap.MapId == 0)
		{
			_currentChannel = 1;
			_instanceCount = 1;
			return;
		}

		_currentChannel = 0;
		_instanceCount = Math.Max(1, worldMap.TwinCount);
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_CHANNEL_INFO.writeImpl.
		buffer.WriteD(_currentChannel);
		buffer.WriteD(_instanceCount);
	}
}
