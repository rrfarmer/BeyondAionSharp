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
		WriteCommonInfo(buffer, HouseInfoView.From(player, house), housingTemplates);
	}

	public static void WriteCommonInfo(
		PacketBuffer buffer,
		WorldHouse house,
		HousingTemplateTable? housingTemplates)
	{
		// Java parity: network/aion/serverpackets/AbstractHouseInfoPacket.writeCommonInfo for spawned House snapshots.
		WriteCommonInfo(buffer, HouseInfoView.From(house), housingTemplates);
	}

	private static void WriteCommonInfo(
		PacketBuffer buffer,
		HouseInfoView house,
		HousingTemplateTable? housingTemplates)
	{
		buffer.WriteD(0);
		buffer.WriteD(house.AddressId);
		buffer.WriteD(house.OwnerObjectId);
		buffer.WriteD(housingTemplates?.GetHouseTypeId(house.BuildingId) ?? 0);
		buffer.WriteC(1);
		buffer.WriteD(house.BuildingId);
		buffer.WriteC(house.IsInactive ? BiddingAllowed : HasOwner | BiddingAllowed);
		buffer.WriteC(house.DoorState);
		buffer.WriteS(Truncate(house.OwnerName, CharacterNameMaxLength));
		buffer.WriteD(house.LegionId);
		buffer.WriteC(house.ShowOwnerName ? 1 : 0);
		buffer.WriteS(Truncate(house.SignNotice ?? string.Empty, SignNoticeMaxLength));

		// Java parity: PartType.values() packet order, including six wall/floor room decor lines.
		var defaultDecorIds = housingTemplates?.GetDefaultDecorIds(house.BuildingId) ?? Array.Empty<int>();
		for (var i = 0; i < HouseDecorLineCount; i++)
			buffer.WriteD(i < defaultDecorIds.Count ? defaultDecorIds[i] : 0);

		buffer.WriteD(0);
		buffer.WriteD(0);
		buffer.WriteC(0);
		WriteLegionEmblem(buffer, house);
	}

	private static void WriteLegionEmblem(PacketBuffer buffer, HouseInfoView house)
	{
		if (house.LegionId <= 0 || house.LegionName.Length == 0)
		{
			for (var i = 0; i < 6; i++)
				buffer.WriteC(0);
			return;
		}

		buffer.WriteC(house.LegionEmblemId);
		buffer.WriteC(house.LegionEmblemType);
		buffer.WriteC(house.LegionEmblemColorA);
		buffer.WriteC(house.LegionEmblemColorR);
		buffer.WriteC(house.LegionEmblemColorG);
		buffer.WriteC(house.LegionEmblemColorB);
	}

	private static string Truncate(string value, int maxLength)
	{
		return value.Length <= maxLength ? value : value[..maxLength];
	}

	private sealed record HouseInfoView(
		int AddressId,
		int BuildingId,
		int OwnerObjectId,
		string OwnerName,
		int LegionId,
		string LegionName,
		byte LegionEmblemId,
		byte LegionEmblemType,
		byte LegionEmblemColorA,
		byte LegionEmblemColorR,
		byte LegionEmblemColorG,
		byte LegionEmblemColorB,
		bool IsInactive,
		byte DoorState,
		bool ShowOwnerName,
		string? SignNotice)
	{
		public static HouseInfoView From(Player player, PlayerHouse house)
		{
			return new HouseInfoView(
				house.AddressId,
				house.BuildingId,
				player.ObjectId,
				player.Name,
				player.LegionId,
				player.LegionName,
				player.LegionEmblemId,
				player.LegionEmblemType,
				player.LegionEmblemColorA,
				player.LegionEmblemColorR,
				player.LegionEmblemColorG,
				player.LegionEmblemColorB,
				house.IsInactive,
				house.DoorState,
				house.ShowOwnerName,
				house.SignNotice);
		}

		public static HouseInfoView From(WorldHouse house)
		{
			return new HouseInfoView(
				house.AddressId,
				house.BuildingId,
				house.OwnerObjectId,
				house.OwnerName,
				house.LegionId,
				house.LegionName,
				house.LegionEmblemId,
				house.LegionEmblemType,
				house.LegionEmblemColorA,
				house.LegionEmblemColorR,
				house.LegionEmblemColorG,
				house.LegionEmblemColorB,
				house.IsInactive,
				house.DoorState,
				house.ShowOwnerName,
				house.SignNotice);
		}
	}
}
