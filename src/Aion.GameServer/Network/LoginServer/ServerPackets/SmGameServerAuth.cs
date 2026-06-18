using Aion.Commons.Network;
using Aion.GameServer.Configuration;

namespace Aion.GameServer.Network.LoginServer.ServerPackets;

public sealed class SmGameServerAuth : LoginServerPacket
{
	private readonly GameServerOptions _options;

	public SmGameServerAuth(GameServerOptions options)
	{
		_options = options;
	}

	protected override void WritePayload(PacketBuffer buffer)
	{
		// Java parity: gameserver/network/loginserver/serverpackets/SM_GS_AUTH.writeImpl.
		var network = _options.Network;
		// The login server hands this address to clients in the server-list as the game-server connect target.
		// 0.0.0.0 (the default client.connect_address / a bind-all socket_address) is a valid LISTEN target but
		// unreachable as a CONNECT target, so a client would fail with "Cannot connect to server". Real Aion
		// auto-discovers the host's address here; default the unspecified address to loopback so same-machine play
		// works out of the box. For remote clients, set gameserver.network.client.connect_address to the reachable
		// (LAN/public) IP — a non-0.0.0.0 value is advertised verbatim.
		var advertisedAddress = network.ClientConnectEndPoint.Address;
		if (advertisedAddress.Equals(System.Net.IPAddress.Any))
			advertisedAddress = System.Net.IPAddress.Loopback;
		var addressBytes = advertisedAddress.GetAddressBytes();

		buffer.WriteC(0x00);
		buffer.WriteC(network.GameServerId);
		buffer.WriteS(network.LoginPassword);
		buffer.WriteC(addressBytes.Length);
		buffer.WriteB(addressBytes);
		buffer.WriteH(network.ClientConnectEndPoint.Port);
		buffer.WriteC(network.MinimumAccessLevel);
		buffer.WriteD(network.MaxOnlinePlayers);
	}
}
