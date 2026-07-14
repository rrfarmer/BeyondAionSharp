using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Network.ChatServer.ServerPackets;
using Aion.GameServer.Services.Ban;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Network.ChatServer;

public sealed class ChatServer : IAsyncDisposable
{
	private readonly ILogger<ChatServer> _logger;
	private readonly GameServerOptions _options;
	private readonly OutboundLinkRetryDelays _retryDelays;
	private readonly SemaphoreSlim _sendLock = new(1, 1);
	private readonly object _lifecycleLock = new();
	private readonly ConcurrentDictionary<int, PlayerAuthCallbackRegistration> _playerAuthCallbacks = new();
	private CancellationTokenSource? _lifetimeTokenSource;
	private Task? _supervisorTask;
	private ConnectionSession? _session;
	private volatile ChatServerState _state = ChatServerState.Disconnected;
	private int _sessionGeneration;
	private bool _stopRequested;

	public ChatServer(ILogger<ChatServer> logger, GameServerOptions options)
		: this(logger, options, null)
	{
	}

	internal ChatServer(
		ILogger<ChatServer> logger,
		GameServerOptions options,
		OutboundLinkRetryDelays? retryDelays)
	{
		_logger = logger;
		_options = options;
		_retryDelays = retryDelays ?? OutboundLinkRetryDelays.JavaDefaults;
		_instance = this;
	}

	// Java parity: network/chatserver/ChatServer is a singleton (SingletonHolder.instance). The C# transport is
	// DI-constructed, so the most-recently-constructed instance is exposed as the singleton bridge for faithful callers.
	private static ChatServer? _instance;

	// Java parity: ChatServer.getInstance().
	public static ChatServer GetInstance()
	{
		return _instance ?? throw new InvalidOperationException("ChatServer has not been initialized.");
	}

	// Java parity: ChatServer.getPublicIP() — raw bytes of the public chat endpoint address (empty when down).
	public byte[] GetPublicIP()
	{
		return PublicEndPoint?.Address.GetAddressBytes() ?? Array.Empty<byte>();
	}

	// Java parity: ChatServer.getPublicPort().
	public int GetPublicPort()
	{
		return PublicEndPoint?.Port ?? 0;
	}

	// Java parity: ChatServer.sendPlayerLoginRequest(Player) — sends SM_CS_PLAYER_AUTH when the bridge is up.
	public void SendPlayerLoginRequest(Player player)
	{
		if (!IsAuthed)
			return;
		var accountName = player.GetClientConnection()?.GetAccount()?.GetName() ?? string.Empty;
		var packet = new SmPlayerAuth(player.ObjectId, accountName, player.GetName(true), ToRaceId(player.Race.ToString()), player.AccessLevel);
		OutboundLinkSendObserver.Observe(
			() => SendPacketAsync(packet), _logger, "chat server", packet.GetType().Name);
	}

	// Java parity: ChatServer.sendPlayerLogout(Player).
	public void SendPlayerLogout(Player player)
	{
		OutboundLinkSendObserver.Observe(
			() => SendPlayerLogoutAsync(player.ObjectId), _logger, "chat server", nameof(SmPlayerLogout));
	}

	// Java parity: ChatServer.sendPlayerGagPacket(int playerObjId, long gagTime).
	public void SendPlayerGagPacket(int playerObjId, long gagTime)
	{
		if (!IsAuthed)
			return;
		var packet = new ServerPackets.SmPlayerGag(playerObjId, gagTime);
		OutboundLinkSendObserver.Observe(
			() => SendPacketAsync(packet), _logger, "chat server", packet.GetType().Name);
	}

	public ChatServerState State => _state;

	public bool IsAuthed => _state == ChatServerState.Authed;

	public IPEndPoint? PublicEndPoint { get; private set; }

	public Task StartAsync(CancellationToken cancellationToken = default)
	{
		// Java parity: ChatServer.connect() owns retry scheduling for the lifetime of the GameServer.
		cancellationToken.ThrowIfCancellationRequested();
		lock (_lifecycleLock)
		{
			if (_supervisorTask != null)
				throw new InvalidOperationException("Chat-server connector has already been started.");
			if (_stopRequested)
				throw new InvalidOperationException("Chat-server connector has been stopped.");

			_lifetimeTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			_supervisorTask = Task.Run(
				() => SuperviseConnectionsAsync(_lifetimeTokenSource.Token),
				CancellationToken.None);
		}
		return Task.CompletedTask;
	}

	public Task SendPacketAsync(ChatServerPacket packet, CancellationToken cancellationToken = default)
	{
		ConnectionSession session;
		lock (_lifecycleLock)
		{
			session = _session ?? throw new InvalidOperationException("Chat-server connector is not connected.");
			if (session.State != ChatServerState.Authed)
				throw new InvalidOperationException("Chat-server connector is not authenticated.");
		}
		return SendPacketAsync(session, packet, cancellationToken);
	}

	public async Task StopAsync()
	{
		Task? supervisorTask;
		ConnectionSession? session;
		lock (_lifecycleLock)
		{
			_stopRequested = true;
			_lifetimeTokenSource?.Cancel();
			supervisorTask = _supervisorTask;
			session = _session;
		}

		session?.Close();
		if (supervisorTask != null)
			await supervisorTask;
		else
			ResetDisconnectedState();
	}

	private async Task SuperviseConnectionsAsync(CancellationToken cancellationToken)
	{
		while (!cancellationToken.IsCancellationRequested)
		{
			ConnectionSession? session = null;
			TimeSpan retryDelay;
			try
			{
				session = await ConnectSessionAsync(cancellationToken);
				await SendPacketAsync(session, new SmChatServerAuth(_options), cancellationToken);
				_logger.LogInformation("Connected to chat server at {Endpoint}", _options.Network.ChatEndPoint);
				await ReadLoopAsync(session);
				retryDelay = session.WasAuthed ? _retryDelays.AuthedReconnect : _retryDelays.PreAuthReconnect;
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				break;
			}
			catch (SocketException ex)
			{
				retryDelay = session == null
					? _retryDelays.SocketFailure
					: session.WasAuthed ? _retryDelays.AuthedReconnect : _retryDelays.PreAuthReconnect;
				_logger.LogInformation(ex,
					"Could not connect to chat server at {Endpoint}; trying again in {Delay}",
					_options.Network.ChatEndPoint, retryDelay);
			}
			catch (IOException ex)
			{
				retryDelay = session == null
					? _retryDelays.IoFailure
					: session.WasAuthed ? _retryDelays.AuthedReconnect : _retryDelays.PreAuthReconnect;
				_logger.LogWarning(ex, "Chat-server bridge I/O failed; trying again in {Delay}", retryDelay);
			}
			catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				retryDelay = session == null
					? _retryDelays.IoFailure
					: session.WasAuthed ? _retryDelays.AuthedReconnect : _retryDelays.PreAuthReconnect;
				_logger.LogError(ex, "Error on chat-server bridge; trying again in {Delay}", retryDelay);
			}
			finally
			{
				if (session != null)
					DisconnectSession(session);
			}

			if (cancellationToken.IsCancellationRequested)
				break;

			try
			{
				await Task.Delay(retryDelay, cancellationToken);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				break;
			}
		}
	}

	private async Task<ConnectionSession> ConnectSessionAsync(CancellationToken cancellationToken)
	{
		var endpoint = _options.Network.ChatEndPoint;
		var client = new TcpClient();
		ConnectionSession? session = null;
		try
		{
			await client.ConnectAsync(endpoint.Address, endpoint.Port, cancellationToken);
			session = new ConnectionSession(
				Interlocked.Increment(ref _sessionGeneration),
				client,
				client.GetStream(),
				cancellationToken);

			lock (_lifecycleLock)
			{
				if (_stopRequested || cancellationToken.IsCancellationRequested)
				{
					session.Close();
					throw new OperationCanceledException(cancellationToken);
				}
				_session = session;
				_state = ChatServerState.Connected;
			}

			return session;
		}
		catch
		{
			if (session != null)
				DisconnectSession(session);
			else
				client.Dispose();
			throw;
		}
	}

	private async Task ReadLoopAsync(ConnectionSession session)
	{
		while (!session.Token.IsCancellationRequested)
		{
			var packet = await ReadPacketAsync(session, session.Token);
			if (packet == null)
				break;

			try
			{
				await ProcessPacketAsync(session, packet);
			}
			catch (OperationCanceledException) when (session.Token.IsCancellationRequested)
			{
				throw;
			}
			catch (OutboundLinkTransportException)
			{
				throw;
			}
			catch (Exception ex)
			{
				// Java CsClientPacket.run catches handler failures per packet; the TCP session remains usable.
				_logger.LogWarning(ex, "Error handling a chat-server packet on session {Generation}", session.Generation);
			}
		}
	}

	public Task<bool> SendPlayerLoginRequestAsync(
		Player player,
		string accountName,
		Func<byte[], Task> sendChatInit,
		CancellationToken cancellationToken = default)
	{
		return SendPlayerLoginRequestAsync(
			player.ObjectId,
			accountName,
			player.GetName(true),
			ToRaceId(player.Race.ToString()),
			player.AccessLevel,
			sendChatInit,
			cancellationToken);
	}

	internal async Task<bool> SendPlayerLoginRequestAsync(
		int playerObjectId,
		string accountName,
		string playerName,
		int raceId,
		byte accessLevel,
		Func<byte[], Task> sendChatInit,
		CancellationToken cancellationToken = default)
	{
		// Java parity: network/chatserver/ChatServer.sendPlayerLoginRequest.
		if (!IsAuthed)
			return false;

		var registration = new PlayerAuthCallbackRegistration(sendChatInit);
		_playerAuthCallbacks[playerObjectId] = registration;
		try
		{
			await SendPacketAsync(
				new SmPlayerAuth(playerObjectId, accountName, playerName, raceId, accessLevel),
				cancellationToken);
			return true;
		}
		catch
		{
			((ICollection<KeyValuePair<int, PlayerAuthCallbackRegistration>>)_playerAuthCallbacks).Remove(
				new KeyValuePair<int, PlayerAuthCallbackRegistration>(playerObjectId, registration));
			throw;
		}
	}

	public async Task<bool> SendPlayerLogoutAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: network/chatserver/ChatServer.sendPlayerLogout.
		_playerAuthCallbacks.TryRemove(playerObjectId, out _);
		if (!IsAuthed)
			return false;

		await SendPacketAsync(new SmPlayerLogout(playerObjectId), cancellationToken);
		return true;
	}

	private async Task ProcessPacketAsync(ConnectionSession session, PacketBuffer packet)
	{
		// Java parity: chatserver bridge response packet dispatch.
		var opcode = packet.ReadC();
		if (opcode == 0x00 && session.State == ChatServerState.Connected)
		{
			ProcessAuthResponse(session, packet);
			return;
		}

		if (opcode == 0x01 && session.State == ChatServerState.Authed)
		{
			await ProcessPlayerAuthResponseAsync(packet);
			return;
		}

		_logger.LogWarning("Unknown chat-server packet 0x{Opcode:X2} in state {State}", opcode, session.State);
	}

	private void ProcessAuthResponse(ConnectionSession session, PacketBuffer packet)
	{
		// Java parity: network/chatserver/clientpackets/CM_CS_AUTH_RESPONSE.readImpl/runImpl.
		var response = packet.ReadC();
		if (response == 0)
		{
			var ipLength = packet.ReadC();
			var ipBytes = packet.ReadB(ipLength);
			var publicEndPoint = new IPEndPoint(new IPAddress(ipBytes), packet.ReadH());
			lock (_lifecycleLock)
			{
				if (!ReferenceEquals(_session, session) || session.IsClosed)
					return;
				session.State = ChatServerState.Authed;
				session.WasAuthed = true;
				PublicEndPoint = publicEndPoint;
				_state = ChatServerState.Authed;
			}
			_logger.LogInformation("Authenticated with chat server; public chat endpoint {Endpoint}", PublicEndPoint);
			return;
		}

		_logger.LogWarning("Chat-server rejected game-server auth with response {Response}", response);
		session.Close();
	}

	private async Task ProcessPlayerAuthResponseAsync(PacketBuffer packet)
	{
		// Java parity: network/chatserver/clientpackets/CM_CS_PLAYER_AUTH_RESPONSE.readImpl/runImpl.
		var playerId = packet.ReadD();
		var tokenLength = packet.ReadC();
		var token = packet.ReadB(tokenLength);
		if (_playerAuthCallbacks.TryRemove(playerId, out var callback))
		{
			await callback.Callback(token);
			return;
		}

		// Java parity: CM_CS_PLAYER_AUTH_RESPONSE.runImpl resolves the current World player, sends the
		// chat token to that player's Aion client, then reapplies any remaining gag to the Chat server.
		// The production CM_CHAT_AUTH path uses this branch; callbacks are retained only for the optional
		// C# async API above.
		var player = global::Aion.GameServer.World.World.GetInstance().GetPlayer(playerId);
		if (player == null)
			return;

		PacketSendUtility.SendPacket(player, new SM_CHAT_INIT(token));
		if (ChatBanService.IsBanned(player))
			SendPlayerGagPacket(player.ObjectId, ChatBanService.GetBanMinutes(player) * 60_000L);
	}

	private async Task SendPacketAsync(
		ConnectionSession session,
		ChatServerPacket packet,
		CancellationToken cancellationToken)
	{
		var frame = packet.SerializeFrame();
		using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, session.Token);
		var sendToken = linkedTokenSource.Token;
		var lockTaken = false;
		try
		{
			await _sendLock.WaitAsync(sendToken);
			lockTaken = true;
			lock (_lifecycleLock)
			{
				if (!ReferenceEquals(_session, session) || session.IsClosed)
					throw new OutboundLinkTransportException(
						"Chat-server connection changed before the packet could be sent.");
			}

			await session.Stream.WriteAsync(frame, sendToken);
			await session.Stream.FlushAsync(sendToken);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (OutboundLinkTransportException)
		{
			throw;
		}
		catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
		{
			session.Close();
			throw new OutboundLinkTransportException("Chat-server packet send failed.", ex);
		}
		finally
		{
			if (lockTaken)
				_sendLock.Release();
		}
	}

	private async Task<PacketBuffer?> ReadPacketAsync(ConnectionSession session, CancellationToken cancellationToken)
	{
		var header = await ReadExactOrNullAsync(session, 2, cancellationToken);
		if (header == null)
			return null;

		var frameLength = BinaryPrimitives.ReadUInt16LittleEndian(header);
		if (!IsSupportedFrameLength(frameLength))
			return null;

		var payload = await ReadExactOrNullAsync(session, frameLength - 2, cancellationToken);
		return payload == null ? null : new PacketBuffer(payload, strictReads: false);
	}

	internal static bool IsSupportedFrameLength(int frameLength)
	{
		return ChatFrameLimits.IsValid(frameLength);
	}

	private static async Task<byte[]?> ReadExactOrNullAsync(
		ConnectionSession session,
		int length,
		CancellationToken cancellationToken)
	{
		var buffer = new byte[length];
		var offset = 0;
		while (offset < length)
		{
			var read = await session.Stream.ReadAsync(buffer.AsMemory(offset, length - offset), cancellationToken);
			if (read == 0)
				return null;
			offset += read;
		}

		return buffer;
	}

	private void DisconnectSession(ConnectionSession session)
	{
		session.Close();
		var wasCurrent = false;
		lock (_lifecycleLock)
		{
			if (ReferenceEquals(_session, session))
			{
				_session = null;
				_state = ChatServerState.Disconnected;
				PublicEndPoint = null;
				wasCurrent = true;
			}
		}

		if (wasCurrent)
			_playerAuthCallbacks.Clear();
		session.Dispose();
	}

	private void ResetDisconnectedState()
	{
		lock (_lifecycleLock)
		{
			_state = ChatServerState.Disconnected;
			PublicEndPoint = null;
		}
		_playerAuthCallbacks.Clear();
	}

	private static int ToRaceId(string race)
	{
		// Java parity: model/Race.raceId.
		return string.Equals(race, "ASMODIANS", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
	}

	public async ValueTask DisposeAsync()
	{
		await StopAsync();
		_lifetimeTokenSource?.Dispose();
		_sendLock.Dispose();
	}

	private sealed class ConnectionSession : IDisposable
	{
		private readonly CancellationTokenSource _tokenSource;
		private int _closed;

		public ConnectionSession(
			int generation,
			TcpClient client,
			NetworkStream stream,
			CancellationToken lifetimeToken)
		{
			Generation = generation;
			Client = client;
			Stream = stream;
			_tokenSource = CancellationTokenSource.CreateLinkedTokenSource(lifetimeToken);
		}

		public int Generation { get; }
		public TcpClient Client { get; }
		public NetworkStream Stream { get; }
		public CancellationToken Token => _tokenSource.Token;
		public ChatServerState State { get; set; } = ChatServerState.Connected;
		public bool WasAuthed { get; set; }
		public bool IsClosed => Volatile.Read(ref _closed) != 0;

		public void Close()
		{
			if (Interlocked.Exchange(ref _closed, 1) != 0)
				return;

			_tokenSource.Cancel();
			try
			{
				Stream.Close();
				Client.Close();
			}
			catch
			{
			}
		}

		public void Dispose()
		{
			Close();
			_tokenSource.Dispose();
			Stream.Dispose();
			Client.Dispose();
		}
	}

	private sealed class OutboundLinkTransportException : Exception
	{
		public OutboundLinkTransportException(string message, Exception? innerException = null)
			: base(message, innerException)
		{
		}
	}

	private sealed class PlayerAuthCallbackRegistration
	{
		public PlayerAuthCallbackRegistration(Func<byte[], Task> callback)
		{
			Callback = callback;
		}

		public Func<byte[], Task> Callback { get; }
	}
}
