using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmHouseRender : GameServerPacket
{
	public const int PacketOpCode = 271;

	private readonly Player _player;
	private readonly PlayerHouse _house;
	private readonly HousingTemplateTable? _housingTemplates;

	public SmHouseRender(Player player, PlayerHouse house, HousingTemplateTable? housingTemplates = null)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_HOUSE_RENDER.
		_player = player;
		_house = house;
		_housingTemplates = housingTemplates;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: SM_HOUSE_RENDER.writeImpl delegates to AbstractHouseInfoPacket.writeCommonInfo.
		HouseInfoPacketWriter.WriteCommonInfo(buffer, _player, _house, _housingTemplates);
	}
}
