using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmHouseObjects : GameServerPacket
{
	public const int PacketOpCode = 270;

	private readonly IReadOnlyList<PlacedHouseObjectSummary> _objects;

	public SmHouseObjects(IReadOnlyList<PlacedHouseObjectSummary> objects)
		: base(PacketOpCode)
	{
		_objects = objects;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_HOUSE_OBJECTS.writeImpl.
		buffer.WriteH(_objects.Count);
		foreach (var obj in _objects)
		{
			buffer.WriteD(obj.TemplateId);
			buffer.WriteF(obj.X);
			buffer.WriteF(obj.Y);
			buffer.WriteF(obj.Z);
		}
	}
}
