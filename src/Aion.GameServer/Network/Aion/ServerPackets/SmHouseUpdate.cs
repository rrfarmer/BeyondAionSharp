using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmHouseUpdate : GameServerPacket
{
	public const int PacketOpCode = 61;

	private readonly Player _player;
	private readonly PlayerHouse _house;
	private readonly WorldHouse? _worldHouse;
	private readonly HousingTemplateTable? _housingTemplates;

	public SmHouseUpdate(Player player, PlayerHouse house, HousingTemplateTable? housingTemplates = null)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_HOUSE_UPDATE.
		_player = player;
		_house = house;
		_housingTemplates = housingTemplates;
	}

	public SmHouseUpdate(WorldHouse house, HousingTemplateTable? housingTemplates = null)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_HOUSE_UPDATE for World-spawned House snapshots.
		_player = null!;
		_house = null!;
		_worldHouse = house;
		_housingTemplates = housingTemplates;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: SM_HOUSE_UPDATE.writeImpl.
		buffer.WriteH(1);
		buffer.WriteH(0);
		buffer.WriteH(1);
		if (_worldHouse != null)
			HouseInfoPacketWriter.WriteCommonInfo(buffer, _worldHouse, _housingTemplates);
		else
			HouseInfoPacketWriter.WriteCommonInfo(buffer, _player, _house, _housingTemplates);
	}
}
