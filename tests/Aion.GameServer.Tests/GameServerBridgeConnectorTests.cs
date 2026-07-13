using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Data;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Network;
using Aion.GameServer.Network.ChatServer.ServerPackets;
using Aion.GameServer.Network.LoginServer.ServerPackets;
using Microsoft.Extensions.Logging.Abstractions;
using GameChatServer = Aion.GameServer.Network.ChatServer.ChatServer;
using GameLoginServer = Aion.GameServer.Network.LoginServer.LoginServer;

namespace Aion.GameServer.Tests;

public sealed class GameServerBridgeConnectorTests
{
	[Fact]
	public void LoginServerAuthPacket_MatchesJavaWireFrame()
	{
		var packet = new SmGameServerAuth(CreateOptions());

		Assert.Equal(
			Convert.FromHexString("1A00000131003200330034000000047F000001611E0064000000"),
			packet.SerializeFrame());
	}

	[Fact]
	public void LoginServerAccountLifecyclePackets_MatchJavaWireFrames()
	{
		Assert.Equal(
			Convert.FromHexString(
				"3D00076300000015CD5B07000000003100320037002E0030002E0030002E0031000000610061002D006200620000006400690073006B002D0031000000"),
			new SmAccountConnectionInfo(99, 123456789L, "127.0.0.1", "aa-bb", "disk-1").SerializeFrame());
		Assert.Equal(
			Convert.FromHexString("07000363000000"),
			new SmAccountDisconnected(99).SerializeFrame());
	}

	[Fact]
	public async Task LoginServerConnector_SendsAuthFrameAndReadsAuthResponse()
	{
		await using var mockServer = await MockBridgeServer.StartAsync(CreateLoginAuthResponseFrame(2));
		await using var connector = new GameLoginServer(
			NullLogger<GameLoginServer>.Instance,
			CreateOptions(loginEndPoint: mockServer.EndPoint));

		await connector.StartAsync();
		var frame = await mockServer.ReadClientFrameAsync();
		await WaitUntilAsync(() => connector.IsAuthed);

		Assert.Equal(
			Convert.FromHexString("1A00000131003200330034000000047F000001611E0064000000"),
			frame);
		Assert.True(connector.IsAuthed);
		Assert.Equal(2, connector.GameServerCount);
	}

	[Fact]
	public async Task LoginServerConnector_SendsAccountAuthAndReadsAccountAuthResponse()
	{
		await using var mockServer = await MockLoginBridgeServer.StartAsync();
		await using var connector = new GameLoginServer(
			NullLogger<GameLoginServer>.Instance,
			CreateOptions(loginEndPoint: mockServer.EndPoint));

		await connector.StartAsync();
		await WaitUntilAsync(() => connector.IsAuthed);
		var resultTask = connector.RequestAccountAuthAsync(accountId: 99, loginOk: 44, playOk1: 22, playOk2: 11);
		var accountAuthFrame = await mockServer.ReadAccountAuthFrameAsync();
		var result = await resultTask;

		Assert.Equal(Convert.FromHexString("130001630000002C000000160000000B000000"), accountAuthFrame);
		Assert.True(result.Ok);
		Assert.Equal(99, result.AccountId);
		Assert.Equal("account-name", result.AccountName);
		Assert.Equal(123456789L, result.CreationDate);
		Assert.Equal(1000L, result.AccumulatedOnlineTime);
		Assert.Equal(2000L, result.AccumulatedRestTime);
		Assert.Equal(3, result.AccessLevel);
		Assert.Equal(2, result.Membership);
		Assert.Equal("disk-1", result.AllowedHddSerial);
	}

	[Fact]
	public async Task LoginServerConnector_RespondsToCharacterCountRequest()
	{
		var repository = new FixedCharacterSelectionRepository(3);
		await using var mockServer = await MockLoginCharacterCountServer.StartAsync(accountId: 77);
		await using var connector = new GameLoginServer(
			NullLogger<GameLoginServer>.Instance,
			CreateOptions(loginEndPoint: mockServer.EndPoint),
			repository);

		await connector.StartAsync();
		await WaitUntilAsync(() => connector.IsAuthed);
		var characterCountFrame = await mockServer.ReadCharacterCountFrameAsync();

		Assert.Equal(77, repository.LastCharacterCountAccountId);
		Assert.Equal(Convert.FromHexString("0800084D00000003"), characterCountFrame);
	}

	[Fact]
	public void ChatServerAuthPacket_MatchesJavaWireFrame()
	{
		var packet = new SmChatServerAuth(CreateOptions(chatPassword: "secret"));

		Assert.Equal(
			Convert.FromHexString("120000017300650063007200650074000000"),
			packet.SerializeFrame());
	}

	[Fact]
	public void ChatServerPlayerAuthPacket_MatchesJavaPayload()
	{
		var payload = new SmPlayerAuth(7001, "account-one", "Kahrun", raceId: 0, accessLevel: 3).SerializePayload();
		using var reader = new PacketBuffer(payload);

		Assert.Equal(0x01, (int)reader.ReadC());
		Assert.Equal(7001, reader.ReadD());
		Assert.Equal("account-one", reader.ReadS());
		Assert.Equal("Kahrun", reader.ReadS());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(3, (int)reader.ReadC());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void ChatServerPlayerLogoutPacket_MatchesJavaPayload()
	{
		var payload = new SmPlayerLogout(7001).SerializePayload();
		using var reader = new PacketBuffer(payload);

		Assert.Equal(0x02, (int)reader.ReadC());
		Assert.Equal(7001, reader.ReadD());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public async Task ChatServerConnector_SendsAuthFrameAndReadsPublicEndpoint()
	{
		await using var mockServer = await MockBridgeServer.StartAsync(CreateChatAuthResponseFrame(IPAddress.Loopback, 10241));
		await using var connector = new GameChatServer(
			NullLogger<GameChatServer>.Instance,
			CreateOptions(chatEndPoint: mockServer.EndPoint, chatPassword: "secret"));

		await connector.StartAsync();
		var frame = await mockServer.ReadClientFrameAsync();
		await WaitUntilAsync(() => connector.IsAuthed);

		Assert.Equal(
			Convert.FromHexString("120000017300650063007200650074000000"),
			frame);
		Assert.True(connector.IsAuthed);
		Assert.Equal(new IPEndPoint(IPAddress.Loopback, 10241), connector.PublicEndPoint);
	}

	private static GameServerOptions CreateOptions(
		IPEndPoint? loginEndPoint = null,
		IPEndPoint? chatEndPoint = null,
		string loginPassword = "1234",
		string chatPassword = "")
	{
		return new GameServerOptions
		{
			Network = new GameServerNetworkOptions
			{
				LoginEndPoint = loginEndPoint ?? new IPEndPoint(IPAddress.Loopback, 9014),
				ChatEndPoint = chatEndPoint ?? new IPEndPoint(IPAddress.Loopback, 9021),
				ClientConnectEndPoint = new IPEndPoint(IPAddress.Loopback, 7777),
				GameServerId = 1,
				LoginPassword = loginPassword,
				ChatPassword = chatPassword,
				MinimumAccessLevel = 0,
				MaxOnlinePlayers = 100,
			},
		};
	}

	private static byte[] CreateLoginAuthResponseFrame(byte gameServerCount)
	{
		return ServerPacketFrameCodec.CreateFrame(new byte[] { 0x00, 0x00, gameServerCount });
	}

	private static byte[] CreateChatAuthResponseFrame(IPAddress publicAddress, int port)
	{
		var addressBytes = publicAddress.GetAddressBytes();
		var payload = new byte[2 + 1 + addressBytes.Length + 2];
		payload[0] = 0x00;
		payload[1] = 0x00;
		payload[2] = (byte)addressBytes.Length;
		addressBytes.CopyTo(payload.AsSpan(3));
		BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(3 + addressBytes.Length, 2), (ushort)port);
		return ServerPacketFrameCodec.CreateFrame(payload);
	}

	private static byte[] CreateAccountAuthResponseFrame()
	{
		using var payload = new PacketBuffer();
		payload.WriteC(0x01);
		payload.WriteD(99);
		payload.WriteC(1);
		payload.WriteS("account-name");
		payload.WriteQ(123456789L);
		payload.WriteQ(1000L);
		payload.WriteQ(2000L);
		payload.WriteC(3);
		payload.WriteC(2);
		payload.WriteS("disk-1");
		return ServerPacketFrameCodec.CreateFrame(payload.ToArray());
	}

	private static byte[] CreateCharacterCountRequestFrame(int accountId)
	{
		using var payload = new PacketBuffer();
		payload.WriteC(0x08);
		payload.WriteD(accountId);
		return ServerPacketFrameCodec.CreateFrame(payload.ToArray());
	}

	private static async Task WaitUntilAsync(Func<bool> condition)
	{
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		while (!condition())
		{
			await Task.Delay(20, timeout.Token);
		}
	}

	private static async Task<byte[]> ReadFrameAsync(NetworkStream stream)
	{
		var header = await ReadExactAsync(stream, 2);
		var frameLength = BinaryPrimitives.ReadUInt16LittleEndian(header);
		var frame = new byte[frameLength];
		header.CopyTo(frame, 0);
		var payload = await ReadExactAsync(stream, frameLength - 2);
		payload.CopyTo(frame.AsSpan(2));
		return frame;
	}

	private static async Task<byte[]> ReadExactAsync(NetworkStream stream, int length)
	{
		var buffer = new byte[length];
		var offset = 0;
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		while (offset < length)
		{
			var read = await stream.ReadAsync(buffer.AsMemory(offset, length - offset), timeout.Token);
			if (read == 0)
				throw new EndOfStreamException("Socket closed before the expected frame was read.");
			offset += read;
		}

		return buffer;
	}

	private sealed class MockBridgeServer : IAsyncDisposable
	{
		private readonly TcpListener _listener;
		private readonly byte[] _responseFrame;
		private readonly TaskCompletionSource _closeAcceptedClient = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private readonly TaskCompletionSource<byte[]> _clientFrame = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private readonly Task _acceptedClientTask;
		private TcpClient? _acceptedClient;

		private MockBridgeServer(TcpListener listener, byte[] responseFrame)
		{
			_listener = listener;
			_responseFrame = responseFrame;
			EndPoint = (IPEndPoint)listener.LocalEndpoint;
			_acceptedClientTask = Task.Run(AcceptAndRespondAsync);
		}

		public IPEndPoint EndPoint { get; }

		public static Task<MockBridgeServer> StartAsync(byte[] responseFrame)
		{
			var listener = new TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			return Task.FromResult(new MockBridgeServer(listener, responseFrame));
		}

		public Task<byte[]> ReadClientFrameAsync()
		{
			return _clientFrame.Task.WaitAsync(TimeSpan.FromSeconds(5));
		}

		private async Task AcceptAndRespondAsync()
		{
			_acceptedClient = await _listener.AcceptTcpClientAsync();
			await using var stream = _acceptedClient.GetStream();
			var frame = await ReadFrameAsync(stream);
			_clientFrame.TrySetResult(frame);
			await stream.WriteAsync(_responseFrame);
			await stream.FlushAsync();
			await _closeAcceptedClient.Task;
		}

		public async ValueTask DisposeAsync()
		{
			_closeAcceptedClient.TrySetResult();
			_listener.Stop();
			_acceptedClient?.Dispose();

			try
			{
				await _acceptedClientTask.WaitAsync(TimeSpan.FromSeconds(2));
			}
			catch
			{
			}
		}
	}

	private sealed class MockLoginBridgeServer : IAsyncDisposable
	{
		private readonly TcpListener _listener;
		private readonly TaskCompletionSource _closeAcceptedClient = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private readonly TaskCompletionSource<byte[]> _accountAuthFrame = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private readonly Task _acceptedClientTask;
		private TcpClient? _acceptedClient;

		private MockLoginBridgeServer(TcpListener listener)
		{
			_listener = listener;
			EndPoint = (IPEndPoint)listener.LocalEndpoint;
			_acceptedClientTask = Task.Run(AcceptAndRespondAsync);
		}

		public IPEndPoint EndPoint { get; }

		public static Task<MockLoginBridgeServer> StartAsync()
		{
			var listener = new TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			return Task.FromResult(new MockLoginBridgeServer(listener));
		}

		public Task<byte[]> ReadAccountAuthFrameAsync()
		{
			return _accountAuthFrame.Task.WaitAsync(TimeSpan.FromSeconds(5));
		}

		private async Task AcceptAndRespondAsync()
		{
			_acceptedClient = await _listener.AcceptTcpClientAsync();
			await using var stream = _acceptedClient.GetStream();
			await ReadFrameAsync(stream);
			await stream.WriteAsync(CreateLoginAuthResponseFrame(1));
			await stream.FlushAsync();
			var accountAuthFrame = await ReadFrameAsync(stream);
			_accountAuthFrame.TrySetResult(accountAuthFrame);
			await stream.WriteAsync(CreateAccountAuthResponseFrame());
			await stream.FlushAsync();
			await _closeAcceptedClient.Task;
		}

		public async ValueTask DisposeAsync()
		{
			_closeAcceptedClient.TrySetResult();
			_listener.Stop();
			_acceptedClient?.Dispose();

			try
			{
				await _acceptedClientTask.WaitAsync(TimeSpan.FromSeconds(2));
			}
			catch
			{
			}
		}
	}

	private sealed class MockLoginCharacterCountServer : IAsyncDisposable
	{
		private readonly TcpListener _listener;
		private readonly int _accountId;
		private readonly TaskCompletionSource _closeAcceptedClient = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private readonly TaskCompletionSource<byte[]> _characterCountFrame = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private readonly Task _acceptedClientTask;
		private TcpClient? _acceptedClient;

		private MockLoginCharacterCountServer(TcpListener listener, int accountId)
		{
			_listener = listener;
			_accountId = accountId;
			EndPoint = (IPEndPoint)listener.LocalEndpoint;
			_acceptedClientTask = Task.Run(AcceptAndRespondAsync);
		}

		public IPEndPoint EndPoint { get; }

		public static Task<MockLoginCharacterCountServer> StartAsync(int accountId)
		{
			var listener = new TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			return Task.FromResult(new MockLoginCharacterCountServer(listener, accountId));
		}

		public Task<byte[]> ReadCharacterCountFrameAsync()
		{
			return _characterCountFrame.Task.WaitAsync(TimeSpan.FromSeconds(5));
		}

		private async Task AcceptAndRespondAsync()
		{
			_acceptedClient = await _listener.AcceptTcpClientAsync();
			await using var stream = _acceptedClient.GetStream();
			await ReadFrameAsync(stream);
			await stream.WriteAsync(CreateLoginAuthResponseFrame(1));
			await stream.FlushAsync();
			await stream.WriteAsync(CreateCharacterCountRequestFrame(_accountId));
			await stream.FlushAsync();
			var characterCountFrame = await ReadFrameAsync(stream);
			_characterCountFrame.TrySetResult(characterCountFrame);
			await _closeAcceptedClient.Task;
		}

		public async ValueTask DisposeAsync()
		{
			_closeAcceptedClient.TrySetResult();
			_listener.Stop();
			_acceptedClient?.Dispose();

			try
			{
				await _acceptedClientTask.WaitAsync(TimeSpan.FromSeconds(2));
			}
			catch
			{
			}
		}
	}

	private sealed class FixedCharacterSelectionRepository : ICharacterSelectionRepository
	{
		private readonly int _characterCount;

		public FixedCharacterSelectionRepository(int characterCount)
		{
			_characterCount = characterCount;
		}

		public int LastCharacterCountAccountId { get; private set; }

		public Task<IReadOnlyList<CharacterSelectionEntry>> LoadCharactersAsync(int accountId, CancellationToken cancellationToken = default)
		{
			return Task.FromResult<IReadOnlyList<CharacterSelectionEntry>>(Array.Empty<CharacterSelectionEntry>());
		}

		public Task<int> GetCharacterCountAsync(int accountId, CancellationToken cancellationToken = default)
		{
			LastCharacterCountAccountId = accountId;
			return Task.FromResult(_characterCount);
		}

		public Task<int> MarkCharacterForDeletionAsync(
			int accountId,
			int characterObjectId,
			TimeSpan deletionDelay,
			CancellationToken cancellationToken = default)
		{
			return Task.FromResult(0);
		}

		public Task<bool> RestoreCharacterAsync(int accountId, int characterObjectId, CancellationToken cancellationToken = default)
		{
			return Task.FromResult(false);
		}
	}
}
