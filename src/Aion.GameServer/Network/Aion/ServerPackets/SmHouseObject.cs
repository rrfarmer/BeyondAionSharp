using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmHouseObject : GameServerPacket
{
	public const int PacketOpCode = 268;

	private readonly PlacedHouseObjectSummary _object;

	public SmHouseObject(PlacedHouseObjectSummary obj)
		: base(PacketOpCode)
	{
		_object = obj;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		HouseObjectPacketWriter.WritePlacedObject(buffer, _object);
	}
}
