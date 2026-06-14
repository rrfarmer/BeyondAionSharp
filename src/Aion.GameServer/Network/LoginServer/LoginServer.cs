using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net.Sockets;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Data;
using Aion.GameServer.Network.LoginServer.ServerPackets;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Network.LoginServer;

public sealed class LoginServer : IAsyncDisposable
{
	private readonly ILogger<LoginServer> _logger;
	private readonly GameServerOptions _options;
	private readonly ICharacterSelectionRepository _characterSelectionRepository;
	private readonly SemaphoreSlim _sendLock = new(1, 1);
	private readonly CancellationTokenSource _shutdownTokenSource = new();
	private readonly ConcurrentDictionary<int, TaskCompletionSource<AccountAuthResult>> _pendingAccountAuthRequests = new();
	private TcpClient? _client;
	private NetworkStream? _stream;
	private Task? _readerTask;
	private LoginServerState _state = LoginServerState.Disconnected;
	private bool _closed;

	// Java parity: LoginServer is a singleton (SingletonHolder.instance). The C# transport is DI-constructed,
	// so the most-recently-constructed instance is exposed as the singleton bridge for faithful callers.
	private static LoginServer? _instance;

	public LoginServer(
		ILogger<LoginServer> logger,
		GameServerOptions options,
		ICharacterSelectionRepository? characterSelectionRepository = null)
	{
		_logger = logger;
		_options = options;
		_characterSelectionRepository = characterSelectionRepository ?? new EmptyCharacterSelectionRepository();
		_instance = this;
	}

	// Java parity: LoginServer.getInstance().
	public static LoginServer GetInstance()
	{
		return _instance ?? throw new InvalidOperationException("LoginServer has not been initialized.");
	}

	// Java parity: LoginServer.getGameServerCount().
	public int GetGameServerCount()
	{
		return GameServerCount;
	}

	// Java parity: LoginServer.sendPacket(LsServerPacket) - fires only when the bridge is up; returns true when sent,
	// false when down (callers use it as a boolean). The idiomatic async transport is bridged fire-and-forget.
	public bool SendPacket(LoginServerPacket packet)
	{
		if (_state == LoginServerState.Disconnected)
			return false;
		_ = SendPacketAsync(packet);
		return true;
	}

	// Java parity: LoginServer.sendBanPacket(byte, int, String, int, int).
	public void SendBanPacket(byte type, int accountId, string ip, int time, int adminObjId)
	{
		SendPacket(new ServerPackets.SM_BAN(type, accountId, ip, time, adminObjId));
	}

	public LoginServerState State => _state;

	public bool IsAuthed => _state == LoginServerState.Authed;

	public int GameServerCount { get; private set; }

	public async Task<AccountAuthResult> RequestAccountAuthAsync(
		int accountId,
		int loginOk,
		int playOk1,
		int playOk2,
		CancellationToken cancellationToken = default)
	{
		// Java parity: loginserver bridge SM_ACCOUNT_AUTH request from game server.
		if (!IsAuthed)
			throw new InvalidOperationException("Login-server connector is not authenticated.");

		var pending = new TaskCompletionSource<AccountAuthResult>(TaskCreationOptions.RunContinuationsAsynchronously);
		if (!_pendingAccountAuthRequests.TryAdd(accountId, pending))
			throw new InvalidOperationException($"Account auth request for account {accountId} is already pending.");

		try
		{
			await SendPacketAsync(new SmAccountAuth(accountId, loginOk, playOk1, playOk2), cancellationToken);
			using var registration = cancellationToken.Register(() => pending.TrySetCanceled(cancellationToken));
			return await pending.Task;
		}
		finally
		{
			_pendingAccountAuthRequests.TryRemove(accountId, out _);
		}
	}

	public Task NotifyAccountConnectedAsync(
		int accountId,
		long time,
		string ip,
		string mac = "",
		string hddSerial = "",
		CancellationToken cancellationToken = default)
	{
		// Java parity: loginserver bridge SM_ACCOUNT_CONNECTION_INFO.
		return IsAuthed
			? SendPacketAsync(new SmAccountConnectionInfo(accountId, time, ip, mac, hddSerial), cancellationToken)
			: Task.CompletedTask;
	}

	public Task NotifyAccountDisconnectedAsync(int accountId, CancellationToken cancellationToken = default)
	{
		// Java parity: loginserver bridge SM_ACCOUNT_DISCONNECTED.
		return IsAuthed
			? SendPacketAsync(new SmAccountDisconnected(accountId), cancellationToken)
			: Task.CompletedTask;
	}

	// Java parity: network/loginserver/LoginServer.onDisconnect(AionConnection) — drops any pending login
	// request tied to the closing connection and, when an account was bound, notifies the login server.
	public void OnDisconnect(global::Aion.GameServer.Network.Aion.AionConnection connection)
	{
		var account = connection.GetAccount();
		if (account != null)
		{
			var accountId = account.GetId();
			_pendingAccountAuthRequests.TryRemove(accountId, out _);
			SendPacket(new SmAccountDisconnected(accountId));
		}
	}

	public async Task StartAsync(CancellationToken cancellationToken = default)
	{
		// Java parity: gameserver/network/loginserver/LoginServer connects and sends SM_GS_AUTH.
		if (_readerTask != null)
			throw new InvalidOperationException("Login-server connector has already been started.");

		var endpoint = _options.Network.LoginEndPoint;
		_client = new TcpClient();
		await _client.ConnectAsync(endpoint.Address, endpoint.Port, cancellationToken);
		_stream = _client.GetStream();
		_state = LoginServerState.Connected;

		await SendPacketAsync(new SmGameServerAuth(_options), cancellationToken);
		_readerTask = Task.Run(() => ReadLoopAsync(_shutdownTokenSource.Token), CancellationToken.None);
		_logger.LogInformation("Connected to login server at {Endpoint}", endpoint);
	}

	public async Task SendPacketAsync(LoginServerPacket packet, CancellationToken cancellationToken = default)
	{
		var stream = _stream ?? throw new InvalidOperationException("Login-server connector is not connected.");
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

				await ProcessPacketAsync(packet, cancellationToken);
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
			_logger.LogError(ex, "Error on login-server bridge");
		}
		finally
		{
			CloseConnection();
		}
	}

	private async Task ProcessPacketAsync(PacketBuffer packet, CancellationToken cancellationToken)
	{
		// Java parity: gameserver/network/loginserver/LoginServer packet handler for auth, account auth, character count.
		var opcode = packet.ReadC();
		if (opcode == 0x01 && _state == LoginServerState.Authed)
		{
			ProcessAccountAuthResponse(packet);
			return;
		}

		if (opcode == 0x08 && _state == LoginServerState.Authed)
		{
			var accountId = packet.ReadD();
			var characterCount = await _characterSelectionRepository.GetCharacterCountAsync(accountId, cancellationToken);
			await SendPacketAsync(new SmGameServerCharacter(accountId, characterCount), cancellationToken);
			return;
		}

		if (opcode != 0x00 || _state != LoginServerState.Connected)
		{
			_logger.LogWarning("Unknown login-server packet 0x{Opcode:X2} in state {State}", opcode, _state);
			return;
		}

		var response = packet.ReadC();
		if (response == 0)
		{
			GameServerCount = packet.ReadC();
			_state = LoginServerState.Authed;
			_logger.LogInformation("Authenticated with login server; {Count} game servers registered", GameServerCount);
			return;
		}

		_logger.LogWarning("Login-server rejected game-server auth with response {Response}", response);
		CloseConnection();
	}

	private void ProcessAccountAuthResponse(PacketBuffer packet)
	{
		// Java parity: loginserver client packet CM_ACOUNT_AUTH_RESPONSE.
		var accountId = packet.ReadD();
		var ok = packet.ReadC() == 1;
		var result = ok
			? new AccountAuthResult(
				accountId,
				Ok: true,
				AccountName: packet.ReadS(),
				CreationDate: packet.ReadQ(),
				AccumulatedOnlineTime: packet.ReadQ(),
				AccumulatedRestTime: packet.ReadQ(),
				AccessLevel: packet.ReadC(),
				Membership: packet.ReadC(),
				Toll: packet.ReadQ(),
				AllowedHddSerial: packet.ReadS())
			: new AccountAuthResult(accountId, Ok: false);

		if (_pendingAccountAuthRequests.TryRemove(accountId, out var pending))
			pending.TrySetResult(result);
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
		_state = LoginServerState.Disconnected;
		_shutdownTokenSource.Cancel();

		try
		{
			_stream?.Close();
			_client?.Close();
		}
		catch
		{
		}

		foreach (var pending in _pendingAccountAuthRequests.Values)
			pending.TrySetException(new IOException("Login-server connector closed."));
		_pendingAccountAuthRequests.Clear();
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
