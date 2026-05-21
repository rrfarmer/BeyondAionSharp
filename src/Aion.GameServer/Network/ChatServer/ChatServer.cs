using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.ChatServer.ServerPackets;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Network.ChatServer;

public sealed class ChatServer : IAsyncDisposable
{
	private readonly ILogger<ChatServer> _logger;
	private readonly GameServerOptions _options;
	private readonly SemaphoreSlim _sendLock = new(1, 1);
	private readonly ConcurrentDictionary<int, Func<byte[], Task>> _playerAuthCallbacks = new();
	private readonly CancellationTokenSource _shutdownTokenSource = new();
	private TcpClient? _client;
	private NetworkStream? _stream;
	private Task? _readerTask;
	private ChatServerState _state = ChatServerState.Disconnected;
	private bool _closed;

	public ChatServer(ILogger<ChatServer> logger, GameServerOptions options)
	{
		_logger = logger;
		_options = options;
	}

	public ChatServerState State => _state;

	public bool IsAuthed => _state == ChatServerState.Authed;

	public IPEndPoint? PublicEndPoint { get; private set; }

	public async Task StartAsync(CancellationToken cancellationToken = default)
	{
		// Java parity: gameserver/network/chatserver/ChatServer connects and sends SM_CS_AUTH.
		if (_readerTask != null)
			throw new InvalidOperationException("Chat-server connector has already been started.");

		var endpoint = _options.Network.ChatEndPoint;
		_client = new TcpClient();
		await _client.ConnectAsync(endpoint.Address, endpoint.Port, cancellationToken);
		_stream = _client.GetStream();
		_state = ChatServerState.Connected;

		await SendPacketAsync(new SmChatServerAuth(_options), cancellationToken);
		_readerTask = Task.Run(() => ReadLoopAsync(_shutdownTokenSource.Token), CancellationToken.None);
		_logger.LogInformation("Connected to chat server at {Endpoint}", endpoint);
	}

	public async Task SendPacketAsync(ChatServerPacket packet, CancellationToken cancellationToken = default)
	{
		var stream = _stream ?? throw new InvalidOperationException("Chat-server connector is not connected.");
		var frame = packet.SerializeFrame();
		await _sendLock.WaitAsync(cancellationToken);
		try
		{
			await stream.WriteAsync(frame, cancellationToken);
			await stream.FlushAsync(cancellationToken);
		}
		finally
		{
			_sendLock.Release();
		}
	}

	public async Task StopAsync()
	{
		CloseConnection();
		if (_readerTask != null)
			await Task.WhenAny(_readerTask, Task.Delay(TimeSpan.FromSeconds(2)));
	}

	private async Task ReadLoopAsync(CancellationToken cancellationToken)
	{
		try
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				var packet = await ReadPacketAsync(cancellationToken);
				if (packet == null)
					break;

				await ProcessPacketAsync(packet);
			}
		}
		catch (OperationCanceledException)
		{
		}
		catch (IOException) when (cancellationToken.IsCancellationRequested)
		{
		}
		catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
		{
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error on chat-server bridge");
		}
		finally
		{
			CloseConnection();
		}
	}

	public async Task<bool> SendPlayerLoginRequestAsync(
		Player player,
		string accountName,
		Func<byte[], Task> sendChatInit,
		CancellationToken cancellationToken = default)
	{
		// Java parity: network/chatserver/ChatServer.sendPlayerLoginRequest.
		if (!IsAuthed)
			return false;

		_playerAuthCallbacks[player.ObjectId] = sendChatInit;
		await SendPacketAsync(
			new SmPlayerAuth(player.ObjectId, accountName, player.Name, ToRaceId(player.Race), player.AccessLevel),
			cancellationToken);
		return true;
	}

	private async Task ProcessPacketAsync(PacketBuffer packet)
	{
		// Java parity: chatserver bridge response packet dispatch.
		var opcode = packet.ReadC();
		if (opcode == 0x00 && _state == ChatServerState.Connected)
		{
			ProcessAuthResponse(packet);
			return;
		}

		if (opcode == 0x01 && _state == ChatServerState.Authed)
		{
			await ProcessPlayerAuthResponseAsync(packet);
			return;
		}

		_logger.LogWarning("Unknown chat-server packet 0x{Opcode:X2} in state {State}", opcode, _state);
	}

	private void ProcessAuthResponse(PacketBuffer packet)
	{
		// Java parity: network/chatserver/clientpackets/CM_CS_AUTH_RESPONSE.readImpl/runImpl.
		var response = packet.ReadC();
		if (response == 0)
		{
			var ipLength = packet.ReadC();
			var ipBytes = packet.ReadB(ipLength);
			PublicEndPoint = new IPEndPoint(new IPAddress(ipBytes), packet.ReadH());
			_state = ChatServerState.Authed;
			_logger.LogInformation("Authenticated with chat server; public chat endpoint {Endpoint}", PublicEndPoint);
			return;
		}

		_logger.LogWarning("Chat-server rejected game-server auth with response {Response}", response);
		CloseConnection();
	}

	private async Task ProcessPlayerAuthResponseAsync(PacketBuffer packet)
	{
		// Java parity: network/chatserver/clientpackets/CM_CS_PLAYER_AUTH_RESPONSE.readImpl/runImpl.
		var playerId = packet.ReadD();
		var tokenLength = packet.ReadC();
		var token = packet.ReadB(tokenLength);
		if (_playerAuthCallbacks.TryRemove(playerId, out var callback))
			await callback(token);
	}

	private async Task<PacketBuffer?> ReadPacketAsync(CancellationToken cancellationToken)
	{
		var header = await ReadExactOrNullAsync(2, cancellationToken);
		if (header == null)
			return null;

		var frameLength = BinaryPrimitives.ReadUInt16LittleEndian(header);
		if (frameLength < 3)
			return null;

		var payload = await ReadExactOrNullAsync(frameLength - 2, cancellationToken);
		return payload == null ? null : new PacketBuffer(payload, strictReads: false);
	}

	private async Task<byte[]?> ReadExactOrNullAsync(int length, CancellationToken cancellationToken)
	{
		var buffer = new byte[length];
		var offset = 0;
		while (offset < length)
		{
			var read = await _stream!.ReadAsync(buffer.AsMemory(offset, length - offset), cancellationToken);
			if (read == 0)
				return null;
			offset += read;
		}

		return buffer;
	}

	private void CloseConnection()
	{
		if (_closed)
			return;

		_closed = true;
		_state = ChatServerState.Disconnected;
		_playerAuthCallbacks.Clear();
		_shutdownTokenSource.Cancel();

		try
		{
			_stream?.Close();
			_client?.Close();
		}
		catch
		{
		}
	}

	private static int ToRaceId(string race)
	{
		// Java parity: model/Race.raceId.
		return string.Equals(race, "ASMODIANS", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
	}

	public async ValueTask DisposeAsync()
	{
		await StopAsync();
		_shutdownTokenSource.Dispose();
		_sendLock.Dispose();
		_stream?.Dispose();
		_client?.Dispose();
	}
}
