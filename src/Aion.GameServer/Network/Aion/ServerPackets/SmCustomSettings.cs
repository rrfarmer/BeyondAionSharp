using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmCustomSettings : GameServerPacket
{
	public const int PacketOpCode = 184;

	private readonly int _objectId;
	private readonly byte _unknown;
	private readonly int _display;
	private readonly int _deny;

	public SmCustomSettings(Player player)
		: this(player.ObjectId, 1, player.Settings.Display, player.Settings.Deny)
	{
		// Java parity: network/aion/serverpackets/SM_CUSTOM_SETTINGS(Player).
	}

	public SmCustomSettings(int objectId, byte unknown, int display, int deny)
		: base(PacketOpCode)
	{
		_objectId = objectId;
		_unknown = unknown;
		_display = display;
		_deny = deny;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_CUSTOM_SETTINGS.writeImpl.
		buffer.WriteD(_objectId);
		buffer.WriteC(_unknown);
		buffer.WriteH(_display);
		buffer.WriteH(_deny);
	}
}
