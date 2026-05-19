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

public sealed class LoginClientConnection : BaseClientConnection
{
	private readonly LoginRsaKeyPair _rsaKeyPair;
	private readonly byte[] _blowfishKey;
	private readonly LoginCryptEngine _cryptEngine = new();
	private readonly ILoginAuthService _authService;
	private LoginClientState _state = LoginClientState.Connected;
	private readonly int _sessionId;
	private SessionKey? _sessionKey;

	public LoginClientConnection(ILogger logger, TcpClient client, string clientId, ILoginKeyGenerator keyGenerator, ILoginAuthService authService)
		: base(logger, client, clientId)
	{
		_authService = authService;
		_sessionId = GetHashCode();
		_rsaKeyPair = keyGenerator.GetEncryptedRsaKeyPair();
		_blowfishKey = keyGenerator.GenerateBlowfishKey();
		_cryptEngine.UpdateKey(_blowfishKey);
	}

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

		return new PacketBuffer(payload);
	}

	protected override async Task ProcessPacketAsync(PacketBuffer packet)
	{
		var parsed = AionClientPacketFactory.Create(packet, _state);
		switch (parsed)
		{
			case CmAuthGameGuard auth when auth.SessionId == _sessionId:
				_state = LoginClientState.AuthedGameGuard;
				await SendPacketAsync(new SmAuthGameGuard(_sessionId));
				break;
			case CmAuthGameGuard:
				await SendPacketAsync(new SmLoginFail(AionAuthResponse.STR_L2AUTH_S_SYSTEM_ERROR));
				await CloseAsync();
				break;
			case CmLogin login:
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
					_state = LoginClientState.AuthedLogin;
					_sessionKey = new SessionKey(authResult.Account);
					await SendPacketAsync(new SmLoginOk(_sessionKey));
				}
				else
				{
					await SendPacketAsync(new SmLoginFail(authResult.Response ?? AionAuthResponse.STR_L2AUTH_S_SYSTEM_ERROR));
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

	private async Task SendPacketAsync(AionServerPacket packet)
	{
		var frame = packet.SerializeEncryptedFrame(_cryptEngine);
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
