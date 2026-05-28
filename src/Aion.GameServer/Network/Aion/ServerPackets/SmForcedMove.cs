using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmForcedMove : GameServerPacket
{
	public const int PacketOpCode = 195;

	private readonly ForcedMoveSnapshot _snapshot;

	public SmForcedMove(ForcedMoveSnapshot snapshot)
		: base(PacketOpCode)
	{
		_snapshot = snapshot;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_FORCED_MOVE.writeImpl writes
		// source object id, target object id, an unknown byte 16, then x/y/z floats.
		buffer.WriteD(_snapshot.SourceObjectId);
		buffer.WriteD(_snapshot.TargetObjectId);
		buffer.WriteC(16);
		buffer.WriteF(_snapshot.X);
		buffer.WriteF(_snapshot.Y);
		buffer.WriteF(_snapshot.Z);
	}
}

public sealed record ForcedMoveSnapshot(
	int SourceObjectId,
	int TargetObjectId,
	float X,
	float Y,
	float Z);