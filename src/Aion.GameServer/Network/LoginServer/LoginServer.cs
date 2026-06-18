using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net.Sockets;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Data;
using Aion.GameServer.Network.Aion.ServerPackets;
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
	// Java parity: network/loginserver/LoginServer.loginRequests (Map<Integer, LoginRequest>).
	private readonly ConcurrentDictionary<int, LoginRequest> _loginRequests = new();
	// Java parity: network/loginserver/LoginServer.loggedInAccounts (Map<Integer, AionConnection>).
	private readonly ConcurrentDictionary<int, global::Aion.GameServer.Network.Aion.AionConnection> _loggedInAccounts = new();
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

	// Java parity: LoginServer.sendLsControlPacket(int, int, Player, Player).
	public void SendLsControlPacket(int type, int param, global::Aion.GameServer.Model.GameObjects.Players.Player player,
		global::Aion.GameServer.Model.GameObjects.Players.Player admin)
	{
		SendPacket(new ServerPackets.SM_LS_CONTROL(type, param, player, admin));
	}

	public LoginServerState State => _state;

	public bool IsAuthed => _state == LoginServerState.Authed;

	public int GameServerCount { get; private set; }

	// Java parity: network/loginserver/LoginServer.registerLoginRequest(int, AionConnection, int, int, int).
	// putIfAbsent a LoginRequest holding the connection and the SM_ACCOUNT_AUTH to forward once the client triggers auth.
	public void RegisterLoginRequest(int accountId, global::Aion.GameServer.Network.Aion.AionConnection client, int loginOk, int playOk1, int playOk2)
	{
		_loginRequests.TryAdd(accountId, new LoginRequest(client, new SmAccountAuth(accountId, loginOk, playOk1, playOk2)));
	}

	// Java parity: network/loginserver/LoginServer.authenticateClient(AionConnection).
	// When the bridge is up, forward the stored SM_ACCOUNT_AUTH for this connection; otherwise disconnect the client.
	public void AuthenticateClient(global::Aion.GameServer.Network.Aion.AionConnection client)
	{
		if (IsAuthed)
		{
			foreach (var request in _loginRequests.Values)
			{
				if (ReferenceEquals(request.Connection, client))
				{
					SendPacket(request.AuthResponse);
					break;
				}
			}
		}
		else
		{
			client.Close(); // disconnect this client since authentication will not happen
		}
	}

	// Java parity: network/loginserver/LoginServer.accountAuthenticationResponse(...). Called by CM_ACOUNT_AUTH_RESPONSE
	// to notify the GameServer of the result of client authentication; completes the client (loginRequests) path.
	public void AccountAuthenticationResponse(int accountId, string accountName, bool result, long creationDate,
		global::Aion.GameServer.Model.Account.AccountTime accountTime, sbyte accessLevel, sbyte membership, long toll, string allowedHddSerial)
	{
		if (!_loginRequests.TryRemove(accountId, out var loginRequest))
			return;

		var client = loginRequest.Connection;
		if (!result || !ValidateMacAndHddSerial(client, allowedHddSerial))
		{
			client.Close(new SM_L2AUTH_LOGIN_CHECK(false, accountName)); // LS sends no accName when result is false
			SendPacket(new SmAccountDisconnected(accountId)); // disconnect manually from login server because account isn't attached to connection yet
			return;
		}

		var account = global::Aion.GameServer.Services.AccountService.GetAccount(accountId, accountName, creationDate, accountTime, accessLevel, membership, toll, allowedHddSerial);
		if (SecurityConfig.HDD_SERIAL_LOCK_UNLOCKED_ACCOUNTS && account.GetAllowedHddSerial().Length == 0 && client.GetHddSerial().Length != 0)
		{
			account.SetAllowedHddSerial(client.GetHddSerial());
			SendPacket(new SM_CHANGE_ALLOWED_HDD_SERIAL(account));
		}
		KickOnlineCharacters(account);
		client.SetAccount(account);
		client.SetState(global::Aion.GameServer.Network.Aion.AionConnection.State.AUTHED);
		_loggedInAccounts[accountId] = client;
		_logger.LogInformation("{Account} authed with MAC: {Mac} and HDD serial: {Hdd}", account, client.GetMacAddress(), client.GetHddSerial());
		client.SendPacket(new SM_L2AUTH_LOGIN_CHECK(true, accountName));
		SendPacket(new SmAccountConnectionInfo(account.GetId(), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), client.GetIP(), client.GetMacAddress(), client.GetHddSerial()));
	}

	// Java parity: network/loginserver/LoginServer.validateMacAndHddSerial(AionConnection, String).
	private bool ValidateMacAndHddSerial(global::Aion.GameServer.Network.Aion.AionConnection client, string allowedHddSerial)
	{
		if (!System.Text.RegularExpressions.Regex.IsMatch(client.GetMacAddress() ?? string.Empty, "^([0-9A-F]{2}-){5}[0-9A-F]{2}$"))
		{
			_logger.LogWarning("{Client} sent an invalid MAC address (modified client or hack): {Mac}", client, client.GetMacAddress());
			return false;
		}
		else if (global::Aion.GameServer.Network.BannedMacManager.GetInstance().IsBanned(client.GetMacAddress()))
		{
			_logger.LogInformation("{Client} was kicked due to mac ban", client);
			return false;
		}
		else if (global::Aion.GameServer.Services.Ban.HDDBanService.GetInstance().IsBanned(client.GetHddSerial()))
		{
			_logger.LogInformation("{Client} was kicked because hdd serial {Hdd} is banned", client, client.GetHddSerial());
			return false;
		}
		else if (SecurityConfig.HDD_SERIAL_LOCK_ENABLE && allowedHddSerial.Length != 0 && !allowedHddSerial.Equals(client.GetHddSerial()))
		{
			_logger.LogInformation("{Client} was kicked due to hdd serial mismatch. Expected {Expected} but client connected with {Actual}", client, allowedHddSerial, client.GetHddSerial());
			return false;
		}
		return true;
	}

	// Java parity: network/loginserver/LoginServer.kickOnlineCharacters(Account).
	private void KickOnlineCharacters(global::Aion.GameServer.Model.Account.Account account)
	{
		foreach (var accountData in account)
		{
			var pcd = accountData.GetPlayerCommonData();
			if (pcd.IsOnline())
			{
				var player = global::Aion.GameServer.World.World.GetInstance().GetPlayer(pcd.GetPlayerObjId());
				if (player != null && player.GetClientConnection() != null)
					player.GetClientConnection().Close(SM_SYSTEM_MESSAGE.STR_KICK_ANOTHER_USER_TRY_LOGIN()); // kick
			}
		}
	}

	// Java parity: network/loginserver/LoginServer.requestAuthReconnection(int, AionConnection).
	// When up and the requesting connection owns the account, ask the LoginServer for a reconnect key; otherwise close.
	public void RequestAuthReconnection(int accountId, global::Aion.GameServer.Network.Aion.AionConnection client)
	{
		if (IsAuthed && client.GetAccount() != null && client.GetAccount().GetId() == accountId)
			SendPacket(new SmAccountReconnectKey(client.GetAccount().GetId()));
		else
			client.Close();
	}

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
		// Java parity: loginRequests.values().removeIf(r -> r.connection == connection).
		foreach (var entry in _loginRequests)
		{
			if (ReferenceEquals(entry.Value.Connection, connection))
				_loginRequests.TryRemove(entry.Key, out _);
		}

		var account = connection.GetAccount();
		if (account != null)
		{
			var accountId = account.GetId();
			_pendingAccountAuthRequests.TryRemove(accountId, out _);
			// Java parity: loggedInAccounts.remove(connection.getAccount().getId()).
			_loggedInAccounts.TryRemove(accountId, out _);
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

		if (opcode == 0x0B && _state == LoginServerState.Authed)
		{
			// Java parity: loginserver clientpacket CM_LS_PING.runImpl -> sendPacket(new SM_LS_PONG()) keep-alive reply.
			await SendPacketAsync(new SmLsPong(), cancellationToken);
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

		// Java parity: CM_ACOUNT_AUTH_RESPONSE.runImpl -> LoginServer.getInstance().accountAuthenticationResponse(...).
		// This completes the client (loginRequests) path so the authenticating client receives SM_L2AUTH_LOGIN_CHECK(true)
		// and is set AUTHED. Build AccountTime from the parsed accumulated online/rest times (matching CM_ACOUNT_AUTH_RESPONSE.readImpl).
		var accountTime = new global::Aion.GameServer.Model.Account.AccountTime();
		if (result.Ok)
		{
			accountTime.SetAccumulatedOnlineTime(result.AccumulatedOnlineTime);
			accountTime.SetAccumulatedRestTime(result.AccumulatedRestTime);
		}
		AccountAuthenticationResponse(accountId, result.AccountName ?? string.Empty, result.Ok, result.CreationDate, accountTime,
			(sbyte)result.AccessLevel, (sbyte)result.Membership, result.Toll, result.AllowedHddSerial ?? string.Empty);

		// Dead path (RequestAccountAuthAsync has no callers) — left intact for safety.
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

	// Java parity: network/loginserver/LoginServer.LoginRequest (connection + pending SM_ACCOUNT_AUTH response).
	private sealed class LoginRequest
	{
		public LoginRequest(global::Aion.GameServer.Network.Aion.AionConnection connection, SmAccountAuth authResponse)
		{
			Connection = connection;
			AuthResponse = authResponse;
		}

		public global::Aion.GameServer.Network.Aion.AionConnection Connection { get; }

		public SmAccountAuth AuthResponse { get; }
	}
}
