using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Net.Sockets;
using Aion.Commons.Network;
using Aion.Commons.Network.Server;
using Aion.LoginServer.Configuration;
using Aion.LoginServer.Data;
using Aion.LoginServer.Model;
using Aion.LoginServer.Network.Aion;
using Aion.LoginServer.Network.Aion.ServerPackets;
using Aion.LoginServer.Network.GameServer;
using Aion.LoginServer.Network.GameServer.ClientPackets;
using Aion.LoginServer.Network.GameServer.ServerPackets;
using Aion.LoginServer.Services;
using Aion.LoginServer.Utils;
using Microsoft.Extensions.Logging;

namespace Aion.LoginServer.Network;

public sealed class GameServerConnection : BaseClientConnection, IGameServerSession
{
	private static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(5);
	private readonly IGameServerRegistry _registry;
	private readonly ILoginSessionRegistry _sessionRegistry;
	private readonly IAccountRepository _accountRepository;
	private readonly IAccountTimeRepository _accountTimeRepository;
	private readonly IBannedIpService _bannedIpService;
	private readonly IPremiumRepository _premiumRepository;
	private readonly IAccountsLogRepository _accountsLogRepository;
	private readonly ILoginAuthService _authService;
	private readonly IBannedMacService _bannedMacService;
	private readonly IBannedHddService _bannedHddService;
	private readonly IPlayerTransferService _playerTransferService;
	private readonly LoginServerOptions _options;
	private readonly GameServerPingTracker _pingTracker = new();
	private readonly CancellationTokenSource _pingCancellationTokenSource = new();
	private readonly SemaphoreSlim _sendLock = new(1, 1);
	private readonly SemaphoreSlim _closeLock = new(1, 1);
	private GameServerConnectionState _state = GameServerConnectionState.Connected;
	private GameServerInfo? _gameServerInfo;
	private Task? _pingTask;

	public GameServerConnection(
		ILogger logger,
		TcpClient client,
		string clientId,
		IGameServerRegistry registry,
		ILoginSessionRegistry sessionRegistry,
		IAccountRepository accountRepository,
		IAccountTimeRepository accountTimeRepository,
		IBannedIpService bannedIpService,
		IPremiumRepository premiumRepository,
		IAccountsLogRepository accountsLogRepository,
		ILoginAuthService authService,
		IBannedMacService bannedMacService,
		IBannedHddService bannedHddService,
		IPlayerTransferService playerTransferService,
		LoginServerOptions options)
		: base(logger, client, clientId)
	{
		_registry = registry;
		_sessionRegistry = sessionRegistry;
		_accountRepository = accountRepository;
		_accountTimeRepository = accountTimeRepository;
		_bannedIpService = bannedIpService;
		_premiumRepository = premiumRepository;
		_accountsLogRepository = accountsLogRepository;
		_authService = authService;
		_bannedMacService = bannedMacService;
		_bannedHddService = bannedHddService;
		_playerTransferService = playerTransferService;
		_options = options;
	}

	public override async Task RunAsync()
	{
		try
		{
			await base.RunAsync();
		}
		finally
		{
			await StopPingLoopAsync(waitForTask: true);
		}
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
				var response = _registry.RegisterGameServer(request, _client.Client.RemoteEndPoint?.ToString() ?? string.Empty, this);
				if (response == GsAuthResponse.AUTHED)
				{
					_state = GameServerConnectionState.Authed;
					_gameServerInfo = _registry.GetGameServer(auth.GameServerId);
					StartPingLoop();
				}
				await SendPacketAsync(new SmGameServerAuthResponse(response, _registry.GetGameServers().Count));
				if (response != GsAuthResponse.AUTHED)
					await CloseAsync();
				break;
			case CmAccountAuth accountAuth:
				await HandleAccountAuthAsync(accountAuth);
				break;
			case CmAccountReconnectKey reconnectKey:
				await HandleAccountReconnectKeyAsync(reconnectKey);
				break;
			case CmAccountDisconnected disconnected:
				await HandleAccountDisconnectedAsync(disconnected);
				break;
			case CmAccountList accountList:
				await HandleAccountListAsync(accountList);
				break;
			case CmLoginServerControl loginServerControl:
				await HandleLoginServerControlAsync(loginServerControl);
				break;
			case CmBan ban:
				await HandleBanAsync(ban);
				break;
			case CmAccountConnectionInfo connectionInfo:
				await HandleAccountConnectionInfoAsync(connectionInfo);
				break;
			case CmGameServerCharacter character:
				await HandleGameServerCharacterAsync(character);
				break;
			case CmAccountTollInfo tollInfo:
				await _premiumRepository.UpdatePointsAsync(tollInfo.AccountId, tollInfo.Toll, required: 0);
				break;
			case CmMacBanControl macBanControl:
				await HandleMacBanControlAsync(macBanControl);
				break;
			case CmPremiumControl premiumControl:
				await HandlePremiumControlAsync(premiumControl);
				break;
			case CmGameServerPong:
				_pingTracker.OnReceivePong();
				_logger.LogDebug("Received gameserver pong from {ClientId}", _clientId);
				break;
			case CmPlayerTransferControl playerTransfer:
				await HandlePlayerTransferControlAsync(playerTransfer);
				break;
			case CmHddBanControl hddBanControl:
				await HandleHddBanControlAsync(hddBanControl);
				break;
			case CmChangeAllowedHddSerial allowedHddSerial:
				await _accountRepository.UpdateAllowedHddSerialAsync(allowedHddSerial.AccountId, allowedHddSerial.HddSerial);
				break;
			case null:
				_logger.LogWarning("Unknown gameserver packet from {ClientId} in state {State}", _clientId, _state);
				break;
			default:
				_logger.LogDebug("Parsed gameserver packet 0x{Opcode:X2} in state {State}", parsed.OpCode, _state);
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
			await WriteAsync(frame, 0, frame.Length);
		}
		finally
		{
			_sendLock.Release();
		}
	}

	private async Task HandleAccountAuthAsync(CmAccountAuth packet)
	{
		if (_gameServerInfo == null)
		{
			await SendPacketAsync(new SmAccountAuthResponse(packet.SessionKey.AccountId, ok: false));
			return;
		}

		var session = _sessionRegistry.ConsumeLoginSession(packet.SessionKey);
		if (session == null)
		{
			await SendPacketAsync(new SmAccountAuthResponse(packet.SessionKey.AccountId, ok: false));
			return;
		}

		var account = session.Account;
		_gameServerInfo.AddAccount(account);
		account.LastServer = (sbyte)_gameServerInfo.Id;
		await _accountRepository.UpdateLastServerAsync(account.Id, account.LastServer);

		var toll = await _premiumRepository.GetPointsAsync(account.Id);
		await SendPacketAsync(
			new SmAccountAuthResponse(
				account.Id,
				ok: true,
				account.Name,
				new DateTimeOffset(account.CreationDate).ToUnixTimeMilliseconds(),
				account.AccountTime.AccumulatedOnlineTime,
				account.AccountTime.AccumulatedRestTime,
				account.AccessLevel,
				account.Membership,
				toll,
				account.AllowedHddSerial ?? string.Empty));
	}

	private async Task HandleAccountReconnectKeyAsync(CmAccountReconnectKey packet)
	{
		var reconnectKey = RandomNumberGenerator.GetInt32(int.MinValue, int.MaxValue);
		var account = _gameServerInfo?.RemoveAccount(packet.AccountId);
		if (account == null)
		{
			_logger.LogWarning("{ClientId} requested reconnection for account {AccountId}, but account is not registered on game server", _clientId, packet.AccountId);
		}
		else
		{
			_sessionRegistry.AddReconnectingAccount(new ReconnectingAccount(account, reconnectKey));
		}

		await SendPacketAsync(new SmAccountReconnectKey(packet.AccountId, reconnectKey));
	}

	private async Task HandleAccountDisconnectedAsync(CmAccountDisconnected packet)
	{
		var account = _gameServerInfo?.RemoveAccount(packet.AccountId);
		if (account != null)
			await _authService.UpdateOnLogoutAsync(account);
	}

	private async Task HandleAccountListAsync(CmAccountList packet)
	{
		if (_gameServerInfo == null)
			return;

		foreach (var accountId in packet.AccountIds)
		{
			var existingServer = _registry.FindLoggedInAccountGameServer(accountId);
			if (existingServer == null)
			{
				var account = await _accountRepository.GetAccountByIdAsync(accountId, useExternalAuth: false);
				if (account != null)
					_gameServerInfo.AddAccount(account);
			}
			else if (existingServer.Id != _gameServerInfo.Id)
			{
				await SendPacketAsync(new SmRequestKickAccount(accountId, notifyDoubleLogin: false));
			}
		}

		await SendPacketAsync(new SmMacBanList(await _bannedMacService.GetEntriesAsync()));
		await SendPacketAsync(new SmHddBanList(await _bannedHddService.GetEntriesAsync()));
		await _sessionRegistry.UpdateServerListForAllLoggedInPlayersAsync(_registry.GetGameServers());
	}

	private async Task HandleLoginServerControlAsync(CmLoginServerControl packet)
	{
		var account = await _accountRepository.GetAccountByIdAsync(packet.AccountId, useExternalAuth: false);
		var result = false;
		if (account != null)
		{
			switch (packet.Type)
			{
				case 1:
					account.AccessLevel = packet.Param;
					break;
				case 2:
					account.Membership = packet.Param;
					break;
			}
			result = await _accountRepository.UpdateAccountAsync(account, useExternalAuth: false);
		}

		await SendPacketAsync(new SmLoginServerControlResponse(packet.Type, packet.Param, packet.AccountId, packet.AdminId, result));
	}

	private async Task HandleBanAsync(CmBan packet)
	{
		var result = false;
		var ip = packet.Ip;

		if ((packet.Type == 1 || packet.Type == 3) && packet.AccountId != 0 && packet.Time >= 0)
		{
			var account = _registry.FindLoggedInAccountGameServer(packet.AccountId)?.GetAccount(packet.AccountId);
			var accountTime = account?.AccountTime ?? await _accountTimeRepository.GetAccountTimeAsync(packet.AccountId);
			if (accountTime != null)
			{
				accountTime.PenaltyEnd = packet.Time == 0
					? DateTime.UnixEpoch.AddMilliseconds(1000)
					: DateTime.UtcNow.AddMinutes(packet.Time);
				await _accountTimeRepository.UpdateAccountTimeAsync(packet.AccountId, accountTime);
				if (account != null)
					account.AccountTime = accountTime;
				result = true;
			}
		}

		if (packet.Type == 2 || packet.Type == 3)
		{
			if (packet.AccountId != 0)
			{
				var lastIp = await _accountRepository.GetLastIpAsync(packet.AccountId);
				if (!string.IsNullOrEmpty(lastIp))
					ip = lastIp;
			}

			if (!string.IsNullOrEmpty(ip))
			{
				if (_bannedIpService.IsBanned(ip))
					result = await _bannedIpService.UnbanAsync(ip);

				if (packet.Time >= 0)
				{
					DateTime? expireTime = packet.Time == 0 ? null : DateTime.UtcNow.AddMinutes(packet.Time);
					result = await _bannedIpService.BanAsync(ip, expireTime);
				}
			}
		}

		if (packet.AccountId != 0)
			await KickAccountAsync(packet.AccountId);

		await SendPacketAsync(new SmBanResponse(packet.Type, packet.AccountId, ip, packet.Time, packet.AdminObjectId, result));
	}

	private async Task HandleAccountConnectionInfoAsync(CmAccountConnectionInfo packet)
	{
		if (!await _accountRepository.UpdateLastMacAsync(packet.AccountId, packet.Mac))
			_logger.LogWarning("Couldn't update account_data.last_mac for accountId {AccountId}", packet.AccountId);
		if (!await _accountRepository.UpdateLastHddSerialAsync(packet.AccountId, packet.HddSerial))
			_logger.LogWarning("Couldn't update account_data.last_hdd_serial for accountId {AccountId}", packet.AccountId);

		if (_options.LogGameServerLogins && _gameServerInfo != null)
		{
			await _accountsLogRepository.AddRecordAsync(
				packet.AccountId,
				_gameServerInfo.Id,
				DateTimeOffset.FromUnixTimeMilliseconds(packet.Time).UtcDateTime,
				packet.Ip,
				packet.Mac,
				packet.HddSerial);
		}
	}

	private async Task HandleMacBanControlAsync(CmMacBanControl packet)
	{
		switch (packet.Type)
		{
			case 0:
				await _bannedMacService.UnbanAsync(packet.Address);
				break;
			case 1:
				await _bannedMacService.BanAsync(packet.Address, DateTimeOffset.FromUnixTimeMilliseconds(packet.Time).UtcDateTime, packet.Details);
				break;
		}
	}

	private async Task HandleHddBanControlAsync(CmHddBanControl packet)
	{
		switch (packet.Type)
		{
			case 0:
				await _bannedHddService.UnbanAsync(packet.Address);
				break;
			case 1:
				await _bannedHddService.BanAsync(packet.Address, DateTimeOffset.FromUnixTimeMilliseconds(packet.Time).UtcDateTime);
				break;
		}
	}

	private async Task HandlePremiumControlAsync(CmPremiumControl packet)
	{
		var points = await _premiumRepository.GetPointsAsync(packet.AccountId);
		var server = _registry.GetGameServer(packet.ServerId);
		if (server == null || !server.IsOnline || !server.IsAccountOnGameServer(packet.AccountId))
		{
			_logger.LogError("Account {AccountId} requested {RequestId} from gs #{ServerId} and server is down.", packet.AccountId, packet.RequestId, packet.ServerId);
			return;
		}

		if (packet.RequiredCost < 0)
		{
			var newCount = points + (packet.RequiredCost * -1);
			await _premiumRepository.UpdatePointsAsync(packet.AccountId, newCount, required: 0);
			await _registry.SendPacketToGameServerAsync(packet.ServerId, new SmPremiumResponse(packet.RequestId, result: 4, newCount));
			return;
		}

		if (points < packet.RequiredCost)
		{
			await _registry.SendPacketToGameServerAsync(packet.ServerId, new SmPremiumResponse(packet.RequestId, result: 2, points));
			return;
		}

		if (await _premiumRepository.UpdatePointsAsync(packet.AccountId, points, packet.RequiredCost))
		{
			points -= packet.RequiredCost;
			await _registry.SendPacketToGameServerAsync(packet.ServerId, new SmPremiumResponse(packet.RequestId, result: 3, points));
		}
		else
		{
			await _registry.SendPacketToGameServerAsync(packet.ServerId, new SmPremiumResponse(packet.RequestId, result: 1, points));
		}
	}

	private async Task HandlePlayerTransferControlAsync(CmPlayerTransferControl packet)
	{
		switch (packet.ActionId)
		{
			case 1:
				await _playerTransferService.RequestTransferAsync(packet.TaskId, packet.Name, packet.Db);
				break;
			case 2:
				await _playerTransferService.OnErrorAsync(packet.TaskId, packet.Reason);
				break;
			case 3:
				await _playerTransferService.OnOkAsync(packet.TaskId);
				break;
			case 4:
				await _playerTransferService.OnTaskStopAsync(packet.TaskId, packet.Reason);
				break;
		}
	}

	private async Task KickAccountAsync(int accountId)
	{
		await _registry.KickAccountFromGameServerAsync(accountId, notifyDoubleLogin: false);
		await _sessionRegistry.KickLoginSessionAsync(accountId, AionAuthResponse.STR_L2AUTH_S_BLOCKED_IP);
	}

	private async Task HandleGameServerCharacterAsync(CmGameServerCharacter packet)
	{
		if (_gameServerInfo == null)
			return;

		_sessionRegistry.AddGameServerCharacterCount(packet.AccountId, _gameServerInfo.Id, packet.CharacterCount);
		if (!_sessionRegistry.HasAllGameServerCharacterCounts(packet.AccountId, _registry.GetGameServers().Count))
			return;

		var session = _sessionRegistry.GetLoginSession(packet.AccountId);
		if (session != null && !session.JoinedGameServer)
		{
			await session.SendPacketAsync(
				new SmServerList(
					_registry.GetGameServers(),
					_sessionRegistry.GetGameServerCharacterCounts(packet.AccountId),
					(byte)session.Account.LastServer));
		}
	}

	public override async Task CloseAsync()
	{
		await _closeLock.WaitAsync();
		try
		{
			if (!_isConnected)
				return;

			await StopPingLoopAsync(waitForTask: false);

			if (_gameServerInfo != null)
			{
				_registry.UnregisterGameServer(_gameServerInfo.Id, this);
				_gameServerInfo = null;
				await _sessionRegistry.UpdateServerListForAllLoggedInPlayersAsync(_registry.GetGameServers());
			}

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

	private void StartPingLoop()
	{
		if (_pingTask != null)
			return;

		_pingTask = Task.Run(PingLoopAsync);
	}

	private async Task StopPingLoopAsync(bool waitForTask)
	{
		if (!_pingCancellationTokenSource.IsCancellationRequested)
			_pingCancellationTokenSource.Cancel();

		if (waitForTask && _pingTask != null)
		{
			try
			{
				await _pingTask;
			}
			catch (OperationCanceledException)
			{
			}
		}
	}

	private async Task PingLoopAsync()
	{
		var cancellationToken = _pingCancellationTokenSource.Token;
		while (!cancellationToken.IsCancellationRequested)
		{
			await Task.Delay(PingInterval, cancellationToken);
			if (_pingTracker.ShouldCloseOnPingTick())
			{
				_logger.LogWarning("Gameserver #{GameServerId} connection died, closing it.", _gameServerInfo?.Id);
				await CloseAsync();
				return;
			}

			await SendPacketAsync(new SmPing());
		}
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
