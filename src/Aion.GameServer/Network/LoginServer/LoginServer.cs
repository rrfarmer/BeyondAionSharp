using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net.Sockets;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Data;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Network.LoginServer.ServerPackets;
using Aion.GameServer.Services.Transfers;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Network.LoginServer;

public sealed class LoginServer : IAsyncDisposable
{
	private readonly ILogger<LoginServer> _logger;
	private readonly GameServerOptions _options;
	private readonly ICharacterSelectionRepository _characterSelectionRepository;
	private readonly ILoginServerInboundPacketDispatcher _inboundPacketDispatcher;
	private readonly OutboundLinkRetryDelays _retryDelays;
	private readonly SemaphoreSlim _sendLock = new(1, 1);
	private readonly object _lifecycleLock = new();
	private readonly ConcurrentDictionary<int, TaskCompletionSource<AccountAuthResult>> _pendingAccountAuthRequests = new();
	// Java parity: network/loginserver/LoginServer.loginRequests (Map<Integer, LoginRequest>).
	private readonly ConcurrentDictionary<int, LoginRequest> _loginRequests = new();
	// Java parity: network/loginserver/LoginServer.loggedInAccounts (Map<Integer, AionConnection>).
	private readonly ConcurrentDictionary<int, global::Aion.GameServer.Network.Aion.AionConnection> _loggedInAccounts = new();
	private CancellationTokenSource? _lifetimeTokenSource;
	private Task? _supervisorTask;
	private ConnectionSession? _session;
	private volatile LoginServerState _state = LoginServerState.Disconnected;
	private int _sessionGeneration;
	private bool _stopRequested;

	// Java parity: LoginServer is a singleton (SingletonHolder.instance). The C# transport is DI-constructed,
	// so the most-recently-constructed instance is exposed as the singleton bridge for faithful callers.
	private static LoginServer? _instance;

	public LoginServer(
		ILogger<LoginServer> logger,
		GameServerOptions options,
		ICharacterSelectionRepository? characterSelectionRepository = null)
		: this(logger, options, characterSelectionRepository, null, null)
	{
	}

	internal LoginServer(
		ILogger<LoginServer> logger,
		GameServerOptions options,
		ICharacterSelectionRepository? characterSelectionRepository,
		ILoginServerInboundPacketDispatcher? inboundPacketDispatcher,
		OutboundLinkRetryDelays? retryDelays = null)
	{
		_logger = logger;
		_options = options;
		_characterSelectionRepository = characterSelectionRepository ?? new EmptyCharacterSelectionRepository();
		_inboundPacketDispatcher = inboundPacketDispatcher ?? new RuntimeInboundPacketDispatcher(this);
		_retryDelays = retryDelays ?? OutboundLinkRetryDelays.JavaDefaults;
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
		ConnectionSession session;
		lock (_lifecycleLock)
		{
			if (_session == null || _session.State != LoginServerState.Authed)
				return false;
			session = _session;
		}
		OutboundLinkSendObserver.Observe(
			() => SendPacketAsync(session, packet, CancellationToken.None),
			_logger,
			"login server",
			packet.GetType().Name);
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
			client.Close(new SM_L2AUTH_LOGIN_CHECK(false, null!)); // disconnect this client since authentication will not happen
		}
	}

	// Java parity: network/loginserver/LoginServer.accountAuthenticationResponse(...). Called by CM_ACCOUNT_AUTH_RESPONSE
	// to notify the GameServer of the result of client authentication; completes the client (loginRequests) path.
	public void AccountAuthenticationResponse(int accountId, string accountName, bool result, long creationDate,
		global::Aion.GameServer.Model.Account.AccountTime accountTime, sbyte accessLevel, sbyte membership, string allowedHddSerial)
	{
		if (!_loginRequests.TryRemove(accountId, out var loginRequest))
		{
			return;
		}

		var client = loginRequest.Connection;
		if (!result)
		{
			client.Close(new SM_L2AUTH_LOGIN_CHECK(false, accountName));
			SendPacket(new SmAccountDisconnected(accountId));
			return;
		}
		if (!ValidateMacAndHddSerial(client, allowedHddSerial))
		{
			client.Close(new SM_L2AUTH_LOGIN_CHECK(false, accountName));
			SendPacket(new SmAccountDisconnected(accountId));
			return;
		}

		var account = global::Aion.GameServer.Services.AccountService.GetAccount(accountId, accountName, creationDate, accountTime, accessLevel, membership, allowedHddSerial);
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
		if (IsAuthed && _loggedInAccounts.TryGetValue(accountId, out var registeredClient)
			&& ReferenceEquals(client, registeredClient))
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

	public Task StartAsync(CancellationToken cancellationToken = default)
	{
		// Java parity: LoginServer.connect() owns retry scheduling for the lifetime of the GameServer.
		// A fresh ConnectionSession (including a fresh linked CTS) is created for every TCP connection.
		cancellationToken.ThrowIfCancellationRequested();
		lock (_lifecycleLock)
		{
			if (_supervisorTask != null)
				throw new InvalidOperationException("Login-server connector has already been started.");
			if (_stopRequested)
				throw new InvalidOperationException("Login-server connector has been stopped.");

			_lifetimeTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			_supervisorTask = Task.Run(
				() => SuperviseConnectionsAsync(_lifetimeTokenSource.Token),
				CancellationToken.None);
		}
		return Task.CompletedTask;
	}

	public Task SendPacketAsync(LoginServerPacket packet, CancellationToken cancellationToken = default)
	{
		ConnectionSession session;
		lock (_lifecycleLock)
		{
			session = _session ?? throw new InvalidOperationException("Login-server connector is not connected.");
			if (session.State != LoginServerState.Authed)
				throw new InvalidOperationException("Login-server connector is not authenticated.");
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
			CleanupDisconnectedSession();
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
				await SendPacketAsync(session, new SmGameServerAuth(_options), cancellationToken);
				_logger.LogInformation("Connected to login server at {Endpoint}", _options.Network.LoginEndPoint);
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
					"Could not connect to login server at {Endpoint}; trying again in {Delay}",
					_options.Network.LoginEndPoint, retryDelay);
			}
			catch (IOException ex)
			{
				retryDelay = session == null
					? _retryDelays.IoFailure
					: session.WasAuthed ? _retryDelays.AuthedReconnect : _retryDelays.PreAuthReconnect;
				_logger.LogWarning(ex, "Login-server bridge I/O failed; trying again in {Delay}", retryDelay);
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
				_logger.LogError(ex, "Error on login-server bridge; trying again in {Delay}", retryDelay);
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
		var endpoint = _options.Network.LoginEndPoint;
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
				_state = LoginServerState.Connected;
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
				await ProcessPacketAsync(session, packet, session.Token);
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
				// Java LsClientPacket.run catches handler failures per packet; the TCP session remains usable.
				_logger.LogError(ex, "Error handling a login-server packet on session {Generation}", session.Generation);
			}
		}
	}

	private async Task ProcessPacketAsync(ConnectionSession session, PacketBuffer packet, CancellationToken cancellationToken)
	{
		// Java parity: LsClientPacketFactory owns the full opcode/state table and each CM_* read order.
		if (!LoginServerInboundPacketFactory.TryCreate(packet, session.State, out var inboundPacket, out var opcode))
		{
			_logger.LogWarning("Unknown login-server packet 0x{Opcode:X2} in state {State}", opcode, session.State);
			return;
		}

		switch (inboundPacket)
		{
			case GameServerAuthResponsePacket authResponse:
				if (authResponse.Response == 0)
				{
					if (!TryAuthenticateSession(session, authResponse.GameServerCount))
						return;
					_logger.LogInformation("Authenticated with login server; {Count} game servers registered", GameServerCount);
					// Java CM_GS_AUTH_RESPONSE.runImpl immediately rebuilds the LS online-account view.
					await SendPacketAsync(session, new SmAccountList(_loggedInAccounts.Keys), cancellationToken);
					return;
				}

				_logger.LogWarning("Login-server rejected game-server auth with response {Response}", authResponse.Response);
				session.Close();
				return;

			case AccountAuthResponsePacket accountAuthResponse:
				ProcessAccountAuthResponse(accountAuthResponse.Result);
				return;

			case CharacterCountRequestPacket characterCountRequest:
				var characterCount = await _characterSelectionRepository.GetCharacterCountAsync(
					characterCountRequest.AccountId, cancellationToken);
				await SendPacketAsync(session,
					new SmGameServerCharacter(characterCountRequest.AccountId, characterCount), cancellationToken);
				return;

			case LoginServerPingPacket:
				await SendPacketAsync(session, new SmLsPong(), cancellationToken);
				return;

			default:
				_inboundPacketDispatcher.Dispatch(inboundPacket!);
				return;
		}
	}

	private void ProcessAccountAuthResponse(AccountAuthResult result)
	{
		// Java parity: CM_ACCOUNT_AUTH_RESPONSE.runImpl -> LoginServer.getInstance().accountAuthenticationResponse(...).
		// This completes the client (loginRequests) path so the authenticating client receives SM_L2AUTH_LOGIN_CHECK(true)
		// and is set AUTHED. Build AccountTime from the parsed accumulated online/rest times (matching CM_ACCOUNT_AUTH_RESPONSE.readImpl).
		var accountTime = new global::Aion.GameServer.Model.Account.AccountTime();
		if (result.Ok)
		{
			accountTime.SetAccumulatedOnlineTime(result.AccumulatedOnlineTime);
			accountTime.SetAccumulatedRestTime(result.AccumulatedRestTime);
		}
		AccountAuthenticationResponse(result.AccountId, result.AccountName ?? string.Empty, result.Ok, result.CreationDate, accountTime,
			(sbyte)result.AccessLevel, (sbyte)result.Membership, result.AllowedHddSerial ?? string.Empty);

		// Dead path (RequestAccountAuthAsync has no callers) — left intact for safety.
		if (_pendingAccountAuthRequests.TryRemove(result.AccountId, out var pending))
			pending.TrySetResult(result);
	}

	private void DispatchRuntimePacket(LoginServerInboundPacket packet)
	{
		switch (packet)
		{
			case KickAccountPacket kick:
				KickAccount(kick.AccountId, kick.NotifyDoubleLogin);
				break;
			case AccountReconnectKeyPacket reconnect:
				AuthReconnectionResponse(reconnect.AccountId, reconnect.ReconnectKey);
				break;
			case LoginServerControlResponsePacket control:
				ProcessLoginServerControlResponse(control);
				break;
			case BanResponsePacket ban:
				ProcessBanResponse(ban);
				break;
			case MacBanListPacket macBanList:
				ProcessMacBanList(macBanList);
				break;
			case HddBanListPacket hddBanList:
				ProcessHddBanList(hddBanList);
				break;
			case PlayerTransferResponsePacket transfer:
				ProcessPlayerTransferResponse(transfer);
				break;
		}
	}

	private void KickAccount(int accountId, bool notifyDoubleLogin)
	{
		if (!_loggedInAccounts.TryGetValue(accountId, out var client))
			return;

		_logger.LogInformation("Kicking account ID {AccountId} by LS request.", accountId);
		client.Close(notifyDoubleLogin ? SM_SYSTEM_MESSAGE.STR_KICK_ANOTHER_USER_TRY_LOGIN() : null);
	}

	private void AuthReconnectionResponse(int accountId, int reconnectKey)
	{
		if (_loggedInAccounts.TryGetValue(accountId, out var client))
			client.Close(new SM_RECONNECT_KEY(reconnectKey));
	}

	private AionConnection? AccountUpdate(int accountId, byte type, byte param)
	{
		if (!_loggedInAccounts.TryGetValue(accountId, out var client))
			return null;

		var account = client.GetAccount();
		if (type == 1)
			account.SetAccessLevel(unchecked((sbyte)param));
		else if (type == 2)
			account.SetMembership(unchecked((sbyte)param));
		return client;
	}

	private void ProcessLoginServerControlResponse(LoginServerControlResponsePacket response)
	{
		var admin = global::Aion.GameServer.World.World.GetInstance().GetPlayer(response.AdminObjectId);
		if (!response.Result)
		{
			SendMessage(admin, "The operation failed.");
			return;
		}

		var playerConnection = AccountUpdate(response.AccountId, response.Type, response.Param);
		var player = playerConnection?.GetActivePlayer();
		var targetAccount = player == null ? $"Account {response.AccountId}" : $"Account of {player.GetName(false)}";
		switch (response.Type)
		{
			case 1:
				NotifyAboutNewPermissions(admin, player, targetAccount, "access level", response.Param);
				break;
			case 2:
				NotifyAboutNewPermissions(admin, player, targetAccount, "membership level", response.Param);
				break;
			default:
				SendMessage(admin, targetAccount + " has been successfully updated.");
				break;
		}
	}

	private static void NotifyAboutNewPermissions(
		Player? admin,
		Player? player,
		string targetAccount,
		string permissionType,
		byte param)
	{
		SendMessage(admin, $"{targetAccount} has been granted {permissionType} {param}.");
		if (admin == null)
			SendMessage(player, $"You have been granted {permissionType} {param}.");
		else
			SendMessage(player, $"You have been granted {permissionType} {param} by {admin.GetName(true)}.");
	}

	private static void SendMessage(Player? player, string message)
	{
		if (player != null)
			PacketSendUtility.SendMessage(player, message);
	}

	private static void ProcessBanResponse(BanResponsePacket response)
	{
		var admin = global::Aion.GameServer.World.World.GetInstance().GetPlayer(response.AdminObjectId);
		if (admin == null)
			return;

		if (response.Type is 1 or 3)
		{
			var message = response.Result
				? response.Time < 0
					? $"Account ID {response.AccountId} was successfully unbanned"
					: response.Time == 0
						? $"Account ID {response.AccountId} was successfully banned"
						: $"Account ID {response.AccountId} was successfully banned for {response.Time} minutes"
				: "Error occurred while banning player's account";
			PacketSendUtility.SendMessage(admin, message);
		}

		if (response.Type is 2 or 3)
		{
			var message = response.Result
				? response.Time < 0
					? $"IP mask {response.Ip} was successfully removed from block list"
					: response.Time == 0
						? $"IP mask {response.Ip} was successfully added to block list"
						: $"IP mask {response.Ip} was successfully added to block list for {response.Time} minutes"
				: $"Error occurred while adding IP mask {response.Ip}";
			PacketSendUtility.SendMessage(admin, message);
		}
	}

	private static void ProcessMacBanList(MacBanListPacket packet)
	{
		var manager = BannedMacManager.GetInstance();
		foreach (var entry in packet.Entries)
			manager.DbLoad(entry.Address, entry.Time, entry.Details);
		manager.OnEnd();
	}

	private void ProcessHddBanList(HddBanListPacket packet)
	{
		var service = Services.Ban.HDDBanService.GetInstance();
		foreach (var entry in packet.Entries)
			service.LoadBan(entry.Serial, entry.Time);
		_logger.LogInformation("Loaded {Count} HDD ban entries.", packet.Entries.Count);
	}

	private void ProcessPlayerTransferResponse(PlayerTransferResponsePacket packet)
	{
		var service = PlayerTransferService.GetInstance();
		switch (packet)
		{
			case PlayerTransferInfoPacket info:
				var transfer = new PlayerTransfer(
					info.TaskId, info.TargetAccountId, info.AccountName, info.Name);
				transfer.SetCommonData(info.CommonData);
				service.PutTransfer(info.TaskId, transfer);
				break;
			case PlayerTransferOkPacket ok:
				service.OnOk(ok.TaskId);
				break;
			case PlayerTransferErrorPacket error:
				service.OnError(error.TaskId, error.Reason);
				break;
			case PlayerTransferPerformActionPacket action:
				if (_options.Network.GameServerId != action.SourceServerId)
				{
					_logger.LogError(
						"Player transfer task {TaskId} targets source server {SourceServerId}, but this server is {GameServerId}.",
						action.TaskId, action.SourceServerId, _options.Network.GameServerId);
					break;
				}
				service.StartTransfer(
					action.SourceAccountId,
					action.TargetAccountId,
					action.PlayerId,
					unchecked((sbyte)action.TargetServerId),
					action.TaskId);
				break;
			case PlayerTransferDataPacket data:
				ProcessPlayerTransferData(service, data);
				break;
			case UnknownPlayerTransferResponsePacket unknown:
				_logger.LogWarning("Unknown player-transfer response action {ActionId}", unknown.ActionId);
				break;
		}
	}

	private static void ProcessPlayerTransferData(PlayerTransferService service, PlayerTransferDataPacket packet)
	{
		var transfer = service.GetTransfer(packet.TaskId);
		switch (packet.ActionId)
		{
			case 24:
				transfer.SetItemsData(packet.Data);
				break;
			case 25:
				transfer.SetData(packet.Data);
				break;
			case 26:
				transfer.SetSkillData(packet.Data);
				break;
			case 27:
				transfer.SetRecipeData(packet.Data);
				break;
			case 28:
				transfer.SetQuestData(packet.Data);
				service.CloneCharacter(packet.TaskId, transfer);
				break;
		}
	}

	private sealed class RuntimeInboundPacketDispatcher : ILoginServerInboundPacketDispatcher
	{
		private readonly LoginServer _owner;

		public RuntimeInboundPacketDispatcher(LoginServer owner)
		{
			_owner = owner;
		}

		public void Dispatch(LoginServerInboundPacket packet)
		{
			_owner.DispatchRuntimePacket(packet);
		}
	}

	private bool TryAuthenticateSession(ConnectionSession session, int gameServerCount)
	{
		lock (_lifecycleLock)
		{
			if (!ReferenceEquals(_session, session) || session.IsClosed)
				return false;

			session.State = LoginServerState.Authed;
			session.WasAuthed = true;
			GameServerCount = gameServerCount;
			_state = LoginServerState.Authed;
			return true;
		}
	}

	private async Task SendPacketAsync(
		ConnectionSession session,
		LoginServerPacket packet,
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
						"Login-server connection changed before the packet could be sent.");
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
			throw new OutboundLinkTransportException("Login-server packet send failed.", ex);
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
		if (frameLength < 3)
			return null;

		var payload = await ReadExactOrNullAsync(session, frameLength - 2, cancellationToken);
		return payload == null ? null : new PacketBuffer(payload, strictReads: false);
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
				_state = LoginServerState.Disconnected;
				wasCurrent = true;
			}
		}

		if (wasCurrent)
			CleanupDisconnectedSession();
		session.Dispose();
	}

	private void CleanupDisconnectedSession()
	{
		// Java LoginServer.disconnect(): pending client logins cannot complete while LS is down.
		foreach (var request in _loginRequests.Values)
		{
			try
			{
				request.Connection.Close();
			}
			catch (Exception ex)
			{
				_logger.LogDebug(ex, "Failed to close a pending client login after LS disconnect");
			}
		}
		_loginRequests.Clear();

		foreach (var pending in _pendingAccountAuthRequests.Values)
			pending.TrySetException(new IOException("Login-server connector closed."));
		_pendingAccountAuthRequests.Clear();
		// Java intentionally retains loggedInAccounts. The next successful authentication sends a fresh snapshot.
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
		public LoginServerState State { get; set; } = LoginServerState.Connected;
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
