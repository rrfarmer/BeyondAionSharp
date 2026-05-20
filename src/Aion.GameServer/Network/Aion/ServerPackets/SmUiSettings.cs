using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmUiSettings : GameServerPacket
{
	public const int PacketOpCode = 30;
	private const int SettingsPayloadLength = 0x1c00;

	private readonly byte[] _data;
	private readonly int _type;

	public SmUiSettings(byte[] data, int type)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_UI_SETTINGS(byte[], int).
		_data = data;
		_type = type;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_UI_SETTINGS.writeImpl.
		buffer.WriteC(_type);
		buffer.WriteH(SettingsPayloadLength);
		buffer.WriteB(_data);
		if (SettingsPayloadLength > _data.Length)
			buffer.WriteB(new byte[SettingsPayloadLength - _data.Length]);
	}
}
