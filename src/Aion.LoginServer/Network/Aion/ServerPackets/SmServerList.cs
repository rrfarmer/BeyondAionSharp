using Aion.Commons.Network;
using Aion.LoginServer.Model;

namespace Aion.LoginServer.Network.Aion.ServerPackets;

public sealed class SmServerList : AionServerPacket
{
	private readonly IReadOnlyCollection<GameServerInfo> _servers;
	private readonly IReadOnlyDictionary<byte, int> _characterCountsByServer;
	private readonly byte _lastServer;

	public SmServerList(IReadOnlyCollection<GameServerInfo> servers, IReadOnlyDictionary<byte, int> characterCountsByServer, byte lastServer)
		: base(0x04)
	{
		_servers = servers;
		_characterCountsByServer = characterCountsByServer;
		_lastServer = lastServer;
	}

	protected override void WritePayload(PacketBuffer buffer)
	{
		var maxIdWithChars = (byte)0;
		buffer.WriteC(_servers.Count);
		buffer.WriteC(_lastServer);

		foreach (var server in _servers.OrderBy(s => s.Id))
		{
			if (server.Id > maxIdWithChars && _characterCountsByServer.ContainsKey(server.Id))
				maxIdWithChars = server.Id;

			buffer.WriteC(server.Id);
			buffer.WriteB(server.Ip);
			buffer.WriteH(server.Port);
			buffer.WriteH(0);
			buffer.WriteC(0);
			buffer.WriteC(0);
			buffer.WriteH(server.CurrentPlayers);
			buffer.WriteH(server.MaxPlayers);
			buffer.WriteC(server.IsOnline ? 1 : 0);
			buffer.WriteC(1);
			buffer.WriteC(0);
			buffer.WriteH(0);
			buffer.WriteC(0);
		}

		buffer.WriteH(maxIdWithChars + 1);
		buffer.WriteC(1);
		for (byte serverId = 1; serverId <= maxIdWithChars; serverId++)
			buffer.WriteC(_characterCountsByServer.GetValueOrDefault(serverId, 0));
		buffer.WriteB(new byte[13]);
	}
}
