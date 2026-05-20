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
		var addressBytes = network.ClientConnectEndPoint.Address.GetAddressBytes();

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
