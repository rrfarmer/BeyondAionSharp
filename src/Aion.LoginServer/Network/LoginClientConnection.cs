using System.Buffers.Binary;
using System.Net.Sockets;
using System.Security.Cryptography;
using Aion.Commons.Network;
using Aion.Commons.Network.Server;
using Aion.LoginServer.Network.Aion;
using Aion.LoginServer.Network.Aion.ClientPackets;
using Aion.LoginServer.Network.Aion.ServerPackets;
using Microsoft.Extensions.Logging;

namespace Aion.LoginServer.Network;

public sealed class LoginClientConnection : BaseClientConnection
{
	private readonly byte[] _rsaModulus;
	private readonly byte[] _blowfishKey;
	private LoginClientState _state = LoginClientState.Connected;
	private readonly int _sessionId;

	public LoginClientConnection(ILogger logger, TcpClient client, string clientId)
		: base(logger, client, clientId)
	{
		_sessionId = GetHashCode();
		_rsaModulus = RandomNumberGenerator.GetBytes(128);
		_blowfishKey = RandomNumberGenerator.GetBytes(16);
	}

	public override async Task RunAsync()
	{
		await SendPacketAsync(new SmInit(_rsaModulus, _blowfishKey, _sessionId));
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
		return payload == null ? null : new PacketBuffer(payload);
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
			case CmLogin:
				await SendPacketAsync(new SmLoginFail(AionAuthResponse.STR_L2AUTH_S_SYSTEM_ERROR));
				break;
			case null:
				_logger.LogWarning("Unknown login packet from {ClientId} in state {State}", _clientId, _state);
				break;
			default:
				_logger.LogDebug("Parsed login packet 0x{Opcode:X2} in state {State}", parsed.OpCode, _state);
				break;
		}
	}

	private async Task SendPacketAsync(AionServerPacket packet)
	{
		var frame = packet.SerializeUnencryptedFrame();
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
