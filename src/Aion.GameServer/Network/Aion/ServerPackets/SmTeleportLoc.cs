using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.World;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmTeleportLoc : GameServerPacket
{
	public const int PacketOpCode = 20;

	private readonly WorldPosition _destination;
	private readonly bool _isInstance;
	private readonly TeleportAnimation _animation;

	public SmTeleportLoc(
		WorldPosition destination,
		TeleportAnimation animation,
		IReadOnlyList<WorldMapSummary> worldMaps)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_TELEPORT_LOC(int,int,float,float,float,byte,TeleportAnimation).
		_destination = destination;
		_animation = animation;
		_isInstance = worldMaps.FirstOrDefault(map => map.MapId == destination.WorldId).IsInstance;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteC(_animation.Id);
		buffer.WriteD(_destination.WorldId);
		buffer.WriteD(_isInstance ? _destination.InstanceId : _destination.WorldId);
		buffer.WriteF(_destination.X);
		buffer.WriteF(_destination.Y);
		buffer.WriteF(_destination.Z);
		buffer.WriteC(_destination.Heading);
	}
}
