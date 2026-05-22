using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

internal static class HouseObjectPacketWriter
{
	private const byte UseItemTypeId = 1;
	private const byte NpcTypeId = 7;

	public static void WritePlacedObject(PacketBuffer buffer, PlacedHouseObjectSummary obj)
	{
		// Java parity: network/aion/serverpackets/SM_HOUSE_OBJECT.writeImpl.
		buffer.WriteD(obj.AddressId);
		buffer.WriteD(obj.OwnerPlayerId);
		buffer.WriteD(obj.ObjectId);
		buffer.WriteD(obj.ObjectId);
		buffer.WriteD(obj.TemplateId);
		buffer.WriteF(obj.X);
		buffer.WriteF(obj.Y);
		buffer.WriteF(obj.Z);
		buffer.WriteH(obj.Rotation);
		buffer.WriteD(obj.CooldownSeconds);
		buffer.WriteD(obj.ExpirationSeconds);
		WriteDyeInfo(buffer, obj.Color);
		buffer.WriteD(0);
		buffer.WriteC(obj.TypeId);
		if (obj.TypeId == UseItemTypeId && obj.UsageData is { Length: > 0 })
			buffer.WriteB(obj.UsageData);
		else if (obj.TypeId == NpcTypeId)
			buffer.WriteD(obj.NpcObjectId);
	}

	public static void WriteDyeInfo(PacketBuffer buffer, int? rgb)
	{
		// Java parity: network/PacketWriteHelper.writeDyeInfo.
		if (!rgb.HasValue)
		{
			buffer.WriteD(0);
			return;
		}

		buffer.WriteC(1);
		buffer.WriteC((rgb.Value & 0xFF0000) >> 16);
		buffer.WriteC((rgb.Value & 0xFF00) >> 8);
		buffer.WriteC(rgb.Value & 0xFF);
	}
}
