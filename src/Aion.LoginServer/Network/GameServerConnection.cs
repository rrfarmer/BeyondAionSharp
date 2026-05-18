using System.Buffers.Binary;
using System.Net.Sockets;
using Aion.Commons.Network;
using Aion.Commons.Network.Server;
using Aion.LoginServer.Network.GameServer;
using Aion.LoginServer.Network.GameServer.ClientPackets;
using Aion.LoginServer.Network.GameServer.ServerPackets;
using Aion.LoginServer.Services;
using Microsoft.Extensions.Logging;

namespace Aion.LoginServer.Network;

public sealed class GameServerConnection : BaseClientConnection
{
	private readonly IGameServerRegistry _registry;
	private GameServerConnectionState _state = GameServerConnectionState.Connected;

	public GameServerConnection(ILogger logger, TcpClient client, string clientId, IGameServerRegistry registry)
		: base(logger, client, clientId)
	{
		_registry = registry;
	}

	protected override async Task<PacketBuffer?> ReadPacketAsync()
	{
		var header = await ReadExactOrNullAsync(2);
		if (header == null)
			return null;

		var frameLength = BinaryPrimitives.ReadUInt16LittleEndian(header);
		if (frameLength < 3)
			return null;

		var payload = await ReadExactOrNullAsync(frameLength - 2);
		return payload == null ? null : new PacketBuffer(payload);
	}

	protected override async Task ProcessPacketAsync(PacketBuffer packet)
	{
		var parsed = GsClientPacketFactory.Create(packet, _state);
		switch (parsed)
		{
			case CmGameServerAuth auth:
				var request = new GameServerAuthRequest(auth.GameServerId, auth.Password, auth.Ip, auth.Port, auth.MinAccessLevel, auth.MaxPlayers);
				var response = _registry.RegisterGameServer(request, _client.Client.RemoteEndPoint?.ToString() ?? string.Empty);
				if (response == GsAuthResponse.AUTHED)
					_state = GameServerConnectionState.Authed;
				await SendPacketAsync(new SmGameServerAuthResponse(response, _registry.GetGameServers().Count));
				if (response != GsAuthResponse.AUTHED)
					await CloseAsync();
				break;
			case CmGameServerPong:
				_logger.LogDebug("Received gameserver pong from {ClientId}", _clientId);
				break;
			case null:
				_logger.LogWarning("Unknown gameserver packet from {ClientId} in state {State}", _clientId, _state);
				break;
			default:
				_logger.LogDebug("Parsed gameserver packet 0x{Opcode:X2} in state {State}", parsed.OpCode, _state);
				break;
		}
	}

	private async Task SendPacketAsync(GsServerPacket packet)
	{
		var frame = packet.SerializeFrame();
		await WriteAsync(frame, 0, frame.Length);
	}

	private async Task<byte[]?> ReadExactOrNullAsync(int length)
	{
		var buffer = new byte[length];
		var offset = 0;
		while (offset < length)
		{
			var read = await ReadAsync(buffer, offset, length - offset, TimeSpan.FromSeconds(30));
			if (read == 0)
				return null;
			offset += read;
		}

		return buffer;
	}
}
