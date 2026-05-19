using System.Buffers.Binary;
using System.Net.Sockets;
using Aion.Commons.Network;
using Aion.Commons.Network.Server;
using Aion.LoginServer.Network.Aion;
using Aion.LoginServer.Network.Aion.ClientPackets;
using Aion.LoginServer.Network.Aion.ServerPackets;
using Aion.LoginServer.Network.Crypto;
using Aion.LoginServer.Services;
using Microsoft.Extensions.Logging;

namespace Aion.LoginServer.Network;

public sealed class LoginClientConnection : BaseClientConnection, ILoginClientSession
{
	private readonly LoginRsaKeyPair _rsaKeyPair;
	private readonly byte[] _blowfishKey;
	private readonly LoginCryptEngine _cryptEngine = new();
	private readonly ILoginAuthService _authService;
	private readonly ILoginSessionRegistry _sessionRegistry;
	private readonly IGameServerRegistry _gameServerRegistry;
	private readonly SemaphoreSlim _sendLock = new(1, 1);
	private readonly SemaphoreSlim _closeLock = new(1, 1);
	private LoginClientState _state = LoginClientState.Connected;
	private readonly int _sessionId;
	private Model.Account? _account;
	private SessionKey? _sessionKey;
	private bool _joinedGameServer;

	public LoginClientConnection(
		ILogger logger,
		TcpClient client,
		string clientId,
		ILoginKeyGenerator keyGenerator,
		ILoginAuthService authService,
		ILoginSessionRegistry sessionRegistry,
		IGameServerRegistry gameServerRegistry)
		: base(logger, client, clientId)
	{
		_authService = authService;
		_sessionRegistry = sessionRegistry;
		_gameServerRegistry = gameServerRegistry;
		_sessionId = GetHashCode();
		_rsaKeyPair = keyGenerator.GetEncryptedRsaKeyPair();
		_blowfishKey = keyGenerator.GenerateBlowfishKey();
		_cryptEngine.UpdateKey(_blowfishKey);
	}

	public Model.Account Account => _account ?? throw new InvalidOperationException("Login session does not have an account yet.");

	public SessionKey SessionKey => _sessionKey ?? throw new InvalidOperationException("Login session does not have a session key yet.");

	public bool JoinedGameServer => _joinedGameServer;

	public override async Task RunAsync()
	{
		await SendPacketAsync(new SmInit(_rsaKeyPair.EncryptedModulus, _blowfishKey, _sessionId));
		await base.RunAsync();
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
		if (payload == null)
			return null;

		if (!_cryptEngine.Decrypt(payload, 0, payload.Length))
		{
			_logger.LogWarning("Wrong checksum from login client {ClientId}", _clientId);
			await CloseAsync();
			return null;
		}

		return new PacketBuffer(payload, strictReads: false);
	}

	protected override async Task ProcessPacketAsync(PacketBuffer packet)
	{
		var parsed = AionClientPacketFactory.Create(packet, _state);
		switch (parsed)
		{
			case CmUpdateSession updateSession:
				if (_sessionRegistry.TryConsumeReconnectingAccount(updateSession.AccountId, updateSession.ReconnectKey, out var reconnectingAccount) && reconnectingAccount != null)
				{
					_account = reconnectingAccount.Account;
					_state = LoginClientState.AuthedLogin;
					_sessionKey = new SessionKey(_account);
					_sessionRegistry.RegisterReconnectedSession(this);
					await SendPacketAsync(new SmUpdateSession(_sessionKey));
				}
				else
				{
					await CloseAsync();
				}
				break;
			case CmAuthGameGuard auth when auth.SessionId == _sessionId:
				_state = LoginClientState.AuthedGameGuard;
				await SendPacketAsync(new SmAuthGameGuard(_sessionId));
				break;
			case CmAuthGameGuard:
				await SendPacketAsync(new SmLoginFail(AionAuthResponse.STR_L2AUTH_S_SYSTEM_ERROR));
				await CloseAsync();
				break;
			case CmLogin login:
				if (login.SessionId != _sessionId)
				{
					await SendPacketAsync(new SmLoginFail(AionAuthResponse.STR_L2AUTH_S_SYSTEM_ERROR));
					break;
				}

				var credentials = LoginCredentialDecryptor.Decrypt(login.EncryptedLoginData, _rsaKeyPair);
				if (credentials == null)
				{
					await SendPacketAsync(new SmLoginFail(AionAuthResponse.STR_L2AUTH_S_SYSTEM_ERROR));
					break;
				}
				var authResult = await _authService.LoginAsync(credentials.Username, credentials.Password, GetRemoteIp());
				if (authResult.SendAccountBannedPacket)
				{
					await SendPacketAsync(new SmAccountBanned2());
					await CloseAsync();
				}
				else if (authResult.Response == AionAuthResponse.STR_L2AUTH_S_ALL_OK && authResult.Account != null)
				{
					if (await _gameServerRegistry.KickAccountFromGameServerAsync(authResult.Account.Id, notifyDoubleLogin: true))
					{
						await SendPacketAsync(new SmLoginFail(AionAuthResponse.STR_L2AUTH_S_ALREADY_LOGIN));
						break;
					}

					_account = authResult.Account;
					_state = LoginClientState.AuthedLogin;
					_sessionKey = new SessionKey(authResult.Account);
					var registerResult = await _sessionRegistry.RegisterLoginSessionAsync(this);
					if (registerResult == LoginSessionRegisterResult.AlreadyLoggedIn)
					{
						await SendPacketAsync(new SmLoginFail(AionAuthResponse.STR_L2AUTH_S_ALREADY_LOGIN));
						_account = null;
						_sessionKey = null;
						_state = LoginClientState.AuthedGameGuard;
						break;
					}
					await _authService.CompleteSuccessfulLoginAsync(authResult.Account, GetRemoteIp());
					await SendPacketAsync(new SmLoginOk(_sessionKey));
				}
				else
				{
					await SendPacketAsync(new SmLoginFail(authResult.Response ?? AionAuthResponse.STR_L2AUTH_S_SYSTEM_ERROR));
					if (authResult.CloseAfterResponse)
						await CloseAsync();
				}
				break;
			case CmServerList serverList:
				if (_sessionKey == null || _account == null || !_sessionKey.CheckLogin(serverList.AccountId, serverList.LoginOk))
				{
					await SendPacketAsync(new SmLoginFail(AionAuthResponse.STR_L2AUTH_S_SYSTEM_ERROR));
					await CloseAsync();
					break;
				}

				var servers = _gameServerRegistry.GetGameServers();
				if (servers.Count == 0)
				{
					await SendPacketAsync(new SmLoginFail(AionAuthResponse.STR_L2AUTH_S_NO_SERVER_LIST));
					await CloseAsync();
					break;
				}

				_sessionRegistry.BeginGameServerCharacterCountLoad(serverList.AccountId, _gameServerRegistry.GetOfflineGameServerCharacterCounts());
				await _gameServerRegistry.RequestOnlineGameServerCharacterCountsAsync(serverList.AccountId);
				if (_sessionRegistry.HasAllGameServerCharacterCounts(serverList.AccountId, servers.Count))
				{
					await SendPacketAsync(
						new SmServerList(
							servers,
							_sessionRegistry.GetGameServerCharacterCounts(serverList.AccountId),
							(byte)_account.LastServer));
				}
				break;
			case CmPlay play:
				if (_sessionKey == null || _account == null || !_sessionKey.CheckLogin(play.AccountId, play.LoginOk))
				{
					await SendPacketAsync(new SmLoginFail(AionAuthResponse.STR_L2AUTH_S_SYSTEM_ERROR));
					await CloseAsync();
					break;
				}

				var gameServer = _gameServerRegistry.GetGameServer(play.ServerId);
				if (gameServer == null || !gameServer.IsOnline)
				{
					await SendPacketAsync(new SmPlayFail(AionAuthResponse.STR_L2AUTH_S_SERVER_DOWN));
				}
				else if (gameServer.MinAccessLevel > _account.AccessLevel)
				{
					await SendPacketAsync(new SmPlayFail(AionAuthResponse.STR_L2AUTH_S_SEVER_CHECK));
				}
				else if (gameServer.IsFull)
				{
					await SendPacketAsync(new SmPlayFail(AionAuthResponse.STR_L2AUTH_S_LIMIT_EXCEED));
				}
				else
				{
					_joinedGameServer = true;
					await SendPacketAsync(new SmPlayOk(_sessionKey, play.ServerId));
				}
				break;
			case null:
				_logger.LogWarning("Unknown login packet from {ClientId} in state {State}", _clientId, _state);
				break;
			default:
				_logger.LogDebug("Parsed login packet 0x{Opcode:X2} in state {State}", parsed.OpCode, _state);
				break;
		}
	}

	private string GetRemoteIp()
	{
		return _client.Client.RemoteEndPoint is System.Net.IPEndPoint endPoint
			? endPoint.Address.ToString()
			: string.Empty;
	}

	public async Task SendPacketAsync(AionServerPacket packet)
	{
		await _sendLock.WaitAsync();
		try
		{
			if (!_isConnected)
				return;

			var frame = packet.SerializeEncryptedFrame(_cryptEngine);
			await WriteAsync(frame, 0, frame.Length);
		}
		finally
		{
			_sendLock.Release();
		}
	}

	public async Task CloseWithPacketAsync(AionServerPacket packet)
	{
		await SendPacketAsync(packet);
		await CloseAsync();
	}

	public override async Task CloseAsync()
	{
		await _closeLock.WaitAsync();
		try
		{
			if (!_isConnected)
				return;

			if (_account != null && !_joinedGameServer)
			{
				_sessionRegistry.RemoveLoginSession(_account, this);
				await _authService.UpdateOnLogoutAsync(_account);
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
