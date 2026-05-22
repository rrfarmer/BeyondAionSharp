using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmHouseUpdate : GameServerPacket
{
	public const int PacketOpCode = 61;
	private const int CharacterNameMaxLength = 25;
	private const int SignNoticeMaxLength = 64;
	private const int HasOwner = 1 << 0;
	private const int BiddingAllowed = 1 << 2;
	private const int HouseDecorLineCount = 19;

	private readonly Player _player;
	private readonly PlayerHouse _house;
	private readonly int _houseTypeId;

	public SmHouseUpdate(Player player, PlayerHouse house, HousingTemplateTable? housingTemplates = null)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_HOUSE_UPDATE.
		_player = player;
		_house = house;
		_houseTypeId = housingTemplates?.GetHouseTypeId(house.BuildingId) ?? 0;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: SM_HOUSE_UPDATE.writeImpl.
		buffer.WriteH(1);
		buffer.WriteH(0);
		buffer.WriteH(1);
		WriteCommonInfo(buffer);
	}

	private void WriteCommonInfo(PacketBuffer buffer)
	{
		// Java parity: network/aion/serverpackets/AbstractHouseInfoPacket.writeCommonInfo.
		buffer.WriteD(0);
		buffer.WriteD(_house.AddressId);
		buffer.WriteD(_player.ObjectId);
		buffer.WriteD(_houseTypeId);
		buffer.WriteC(1);
		buffer.WriteD(_house.BuildingId);
		buffer.WriteC(_house.IsInactive ? BiddingAllowed : HasOwner | BiddingAllowed);
		buffer.WriteC(_house.DoorState);
		buffer.WriteS(Truncate(_player.Name, CharacterNameMaxLength));
		buffer.WriteD(_player.LegionId);
		buffer.WriteC(_house.ShowOwnerName ? 1 : 0);
		buffer.WriteS(Truncate(_house.SignNotice ?? string.Empty, SignNoticeMaxLength));

		for (var i = 0; i < HouseDecorLineCount; i++)
			buffer.WriteD(0);

		buffer.WriteD(0);
		buffer.WriteD(0);
		buffer.WriteC(0);
		WriteLegionEmblem(buffer);
	}

	private void WriteLegionEmblem(PacketBuffer buffer)
	{
		if (_player.LegionId <= 0 || _player.LegionName.Length == 0)
		{
			for (var i = 0; i < 6; i++)
				buffer.WriteC(0);
			return;
		}

		buffer.WriteC(_player.LegionEmblemId);
		buffer.WriteC(_player.LegionEmblemType);
		buffer.WriteC(_player.LegionEmblemColorA);
		buffer.WriteC(_player.LegionEmblemColorR);
		buffer.WriteC(_player.LegionEmblemColorG);
		buffer.WriteC(_player.LegionEmblemColorB);
	}

	private static string Truncate(string value, int maxLength)
	{
		return value.Length <= maxLength ? value : value[..maxLength];
	}
}
