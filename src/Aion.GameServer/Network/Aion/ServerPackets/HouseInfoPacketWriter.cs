using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

internal static class HouseInfoPacketWriter
{
	private const int CharacterNameMaxLength = 25;
	private const int SignNoticeMaxLength = 64;
	private const int HasOwner = 1 << 0;
	private const int BiddingAllowed = 1 << 2;
	private const int HouseDecorLineCount = 19;

	public static void WriteCommonInfo(
		PacketBuffer buffer,
		Player player,
		PlayerHouse house,
		HousingTemplateTable? housingTemplates)
	{
		// Java parity: network/aion/serverpackets/AbstractHouseInfoPacket.writeCommonInfo.
		buffer.WriteD(0);
		buffer.WriteD(house.AddressId);
		buffer.WriteD(player.ObjectId);
		buffer.WriteD(housingTemplates?.GetHouseTypeId(house.BuildingId) ?? 0);
		buffer.WriteC(1);
		buffer.WriteD(house.BuildingId);
		buffer.WriteC(house.IsInactive ? BiddingAllowed : HasOwner | BiddingAllowed);
		buffer.WriteC(house.DoorState);
		buffer.WriteS(Truncate(player.Name, CharacterNameMaxLength));
		buffer.WriteD(player.LegionId);
		buffer.WriteC(house.ShowOwnerName ? 1 : 0);
		buffer.WriteS(Truncate(house.SignNotice ?? string.Empty, SignNoticeMaxLength));

		for (var i = 0; i < HouseDecorLineCount; i++)
			buffer.WriteD(0);

		buffer.WriteD(0);
		buffer.WriteD(0);
		buffer.WriteC(0);
		WriteLegionEmblem(buffer, player);
	}

	private static void WriteLegionEmblem(PacketBuffer buffer, Player player)
	{
		if (player.LegionId <= 0 || player.LegionName.Length == 0)
		{
			for (var i = 0; i < 6; i++)
				buffer.WriteC(0);
			return;
		}

		buffer.WriteC(player.LegionEmblemId);
		buffer.WriteC(player.LegionEmblemType);
		buffer.WriteC(player.LegionEmblemColorA);
		buffer.WriteC(player.LegionEmblemColorR);
		buffer.WriteC(player.LegionEmblemColorG);
		buffer.WriteC(player.LegionEmblemColorB);
	}

	private static string Truncate(string value, int maxLength)
	{
		return value.Length <= maxLength ? value : value[..maxLength];
	}
}
