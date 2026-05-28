using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmLookAtObject : GameServerPacket
{
	public const int PacketOpCode = 40;

	private readonly LookAtObjectSnapshot _snapshot;

	public SmLookAtObject(LookAtObjectSnapshot snapshot)
		: base(PacketOpCode)
	{
		_snapshot = snapshot;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_LOOKATOBJECT.writeImpl writes
		// visibleObject id, target id or 0, and visibleObject heading.
		buffer.WriteD(_snapshot.ObjectId);
		buffer.WriteD(_snapshot.TargetObjectId);
		buffer.WriteC(_snapshot.Heading);
	}
}

public sealed record LookAtObjectSnapshot(
	int ObjectId,
	int TargetObjectId,
	int Heading);
