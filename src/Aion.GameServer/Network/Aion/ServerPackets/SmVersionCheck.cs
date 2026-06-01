using Aion.Commons.Network;
using Aion.GameServer.Model;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmVersionCheck : GameServerPacket
{
	public const int PacketOpCode = 0;
	public const int InternalVersion = 207;

	public SmVersionCheck(int version, EventTheme cityDecoration)
		: base(PacketOpCode)
	{
		Version = version;
		CityDecoration = cityDecoration;
	}

	public int Version { get; }
	public EventTheme CityDecoration { get; }

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_VERSION_CHECK.writeImpl incompatible-client branch.
		if (Version != InternalVersion)
		{
			buffer.WriteC(1);
			return;
		}

		// Java success payload depends on dynamic config/time/chat-server/ratio/passport state and remains unported.
		throw new NotSupportedException("SM_VERSION_CHECK success payload is not ported yet.");
	}
}
