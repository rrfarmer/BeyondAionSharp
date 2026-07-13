using Aion.Commons.Network;

namespace Aion.LoginServer.Network.GameServer.ServerPackets;

public sealed class SmHddBanList : GsServerPacket
{
	private readonly IReadOnlyDictionary<string, DateTime> _bannedList;

	public SmHddBanList(IReadOnlyDictionary<string, DateTime> bannedList)
	{
		_bannedList = bannedList;
	}

	protected override void WritePayload(PacketBuffer buffer)
	{
		buffer.WriteC(10);
		buffer.WriteD(_bannedList.Count);
		foreach (var entry in _bannedList)
		{
			buffer.WriteS(entry.Key);
			buffer.WriteQ(new DateTimeOffset(entry.Value).ToUnixTimeMilliseconds());
		}
	}
}
