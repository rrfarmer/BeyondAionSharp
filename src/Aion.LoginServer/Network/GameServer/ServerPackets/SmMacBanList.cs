using Aion.Commons.Network;
using Aion.Commons.Database;
using Aion.LoginServer.Model;

namespace Aion.LoginServer.Network.GameServer.ServerPackets;

public sealed class SmMacBanList : GsServerPacket
{
	private readonly IReadOnlyCollection<BannedMacEntry> _bannedList;

	public SmMacBanList(IReadOnlyCollection<BannedMacEntry> bannedList)
	{
		_bannedList = bannedList;
	}

	protected override void WritePayload(PacketBuffer buffer)
	{
		buffer.WriteC(9);
		buffer.WriteD(_bannedList.Count);
		foreach (var entry in _bannedList)
		{
			buffer.WriteS(entry.Mac);
			buffer.WriteQ(DatabaseTimestamp.ToUnixTimeMilliseconds(entry.Time));
			buffer.WriteS(entry.Details);
		}
	}
}
