using System.Buffers.Binary;
using System.Net.Sockets;
using Aion.ChatServer.Configuration;
using Aion.ChatServer.Models;
using Aion.ChatServer.Network.Packets.GameServer;
using Aion.ChatServer.Services;
using Aion.Commons.Network;
using Aion.Commons.Network.Server;
using Microsoft.Extensions.Logging;

namespace Aion.ChatServer.Network.Handlers;

public sealed class GsConnection : BaseClientConnection
{
	private readonly IGameServerService _gameServerService;
	private readonly IChatService _chatService;
	private readonly ChatServerOptions _options;
	private readonly SemaphoreSlim _sendLock = new(1, 1);
	private readonly SemaphoreSlim _closeLock = new(1, 1);
	private GameServerConnectionState _state = GameServerConnectionState.Connected;

	public GsConnection(
		ILogger logger,
		TcpClient client,
		string clientId,
		IGameServerService gameServerService,
		IChatService chatService,
		ChatServerOptions options)
		: base(logger, client, clientId)
	{
		_gameServerService = gameServerService;
		_chatService = chatService;
		_options = options;
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
		return payload == null ? null : new PacketBuffer(payload, strictReads: false);
	}

	protected override async Task ProcessPacketAsync(PacketBuffer packet)
	{
		var parsed = GsPacketFactory.Create(packet, _state);
		if (parsed == null)
		{
			_logger.LogWarning("Unknown chat gameserver packet from {ClientId} in state {State}", _clientId, _state);
			return;
		}

		try
		{
			await DispatchPacketAsync(parsed);
		}
		catch (Exception ex) when (ex is not GameServerTransportException)
		{
			// Java GsClientPacket.run() isolates packet-handler failures from the socket dispatcher.
			_logger.LogWarning(ex, "Error handling chat gameserver packet 0x{Opcode:X2} from {ClientId}", parsed.OpCode, _clientId);
		}
	}

	private async Task DispatchPacketAsync(GsClientPacket parsed)
	{
		switch (parsed)
		{
			case CmChatServerAuth auth:
				await HandleAuthAsync(auth);
				break;
			case CmPlayerAuth playerAuth:
				await HandlePlayerAuthAsync(playerAuth);
				break;
			case CmPlayerLogout logout:
				await HandlePlayerLogoutAsync(logout);
				break;
			case CmPlayerGag gag:
				_chatService.GagPlayer(gag.PlayerId, gag.GagTimeMillis);
				break;
			default:
				_logger.LogDebug("Parsed chat gameserver packet 0x{Opcode:X2} in state {State}", parsed.OpCode, _state);
				break;
		}
	}

	public async Task SendPacketAsync(GsServerPacket packet)
	{
		await _sendLock.WaitAsync();
		try
		{
			if (!_isConnected)
				return;

			var frame = packet.SerializeFrame();
			try
			{
				await WriteAsync(frame, 0, frame.Length);
			}
			catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
			{
				throw new GameServerTransportException("Failed to write a chat gameserver packet.", ex);
			}
		}
		finally
		{
			_sendLock.Release();
		}
	}

	public override async Task CloseAsync()
	{
		await _closeLock.WaitAsync();
		try
		{
			if (!_isConnected)
				return;

			if (_state == GameServerConnectionState.Authed)
				_gameServerService.SetOffline();
			_state = GameServerConnectionState.Disconnected;

			await _sendLock.WaitAsync();
			try
			{
				await base.CloseAsync();
			}
			finally
			{
				_sendLock.Release();
			}
		}
		finally
		{
			_closeLock.Release();
		}
	}

	private async Task HandleAuthAsync(CmChatServerAuth packet)
	{
		var response = _gameServerService.RegisterGameServer(packet.GameServerId, packet.Password);
		if (response == GsAuthResponse.Authed)
			_state = GameServerConnectionState.Authed;

		await SendPacketAsync(new SmGameServerAuthResponse(response, _options));
		if (response != GsAuthResponse.Authed)
			await CloseAsync();
	}

	private async Task HandlePlayerAuthAsync(CmPlayerAuth packet)
	{
		var race = RaceExtensions.FromId(packet.RaceId);
		if (race == null)
		{
			_logger.LogWarning("Received chat player auth for unsupported race id {RaceId}", packet.RaceId);
			return;
		}

		var client = _chatService.RegisterPlayer(packet.PlayerId, packet.AccountName, packet.Nickname, race.Value, packet.AccessLevel);
		await SendPacketAsync(new SmPlayerAuthResponse(client.ClientId, client.Token));
	}

	private async Task HandlePlayerLogoutAsync(CmPlayerLogout packet)
	{
		var client = _chatService.PlayerLogout(packet.PlayerId);
		if (client?.Connection != null)
			await client.Connection.CloseAsync();
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

	private sealed class GameServerTransportException : IOException
	{
		public GameServerTransportException(string message, Exception innerException)
			: base(message, innerException)
		{
		}
	}
}
