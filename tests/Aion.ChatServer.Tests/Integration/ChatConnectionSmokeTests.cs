using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Aion.ChatServer.Configuration;
using Aion.ChatServer.Data.Repositories;
using Aion.ChatServer.Handlers;
using Aion.ChatServer.Handlers.BuiltIn;
using Aion.ChatServer.Models;
using Aion.ChatServer.Models.Channels;
using Aion.ChatServer.Network;
using Aion.ChatServer.Network.Handlers;
using Aion.ChatServer.Network.Packets;
using Aion.ChatServer.Network.Packets.GameServer;
using Aion.ChatServer.Services;
using Aion.Commons.Network;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.ChatServer.Tests.Integration;

public class ChatConnectionSmokeTests
{
	[Fact]
	public async Task GameServerConnection_AuthenticatesAndRegistersPlayerOverSocket()
	{
		var options = new ChatServerOptions
		{
			GameServerPassword = "secret",
			ClientConnectEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 10241)
		};
		var channels = new ChatChannels(NullLogger<ChatChannels>.Instance);
		var broadcast = new BroadcastService(NullLogger<BroadcastService>.Instance);
		var chatService = new ChatService(channels, broadcast, NullLogger<ChatService>.Instance);
		var gameServerService = new GameServerService(options, NullLogger<GameServerService>.Instance);

		await using var harness = await SocketHarness.ConnectAsync(
			serverClient => new GsConnection(
				NullLogger.Instance,
				serverClient,
				"gs-smoke",
				gameServerService,
				chatService,
				options));

		await harness.ClientStream.WriteAsync(ChatPacketFrameCodec.CreateFrame(Packet(w => w.C(GsPacketFactory.CmChatServerAuth).C(1).S("secret"))));
		var authResponse = await ReadPayloadAsync(harness.ClientStream);
		Assert.Equal([0x00, 0x00, 0x04, 0x7F, 0x00, 0x00, 0x01, 0x01, 0x28], authResponse);

		await harness.ClientStream.WriteAsync(
			ChatPacketFrameCodec.CreateFrame(
				Packet(w => w.C(GsPacketFactory.CmPlayerAuth).D(123).S("account").S("Daeva").D((int)Race.Elyos).C(0))));
		var playerAuthResponse = await ReadPayloadAsync(harness.ClientStream);

		Assert.Equal(0x01, playerAuthResponse[0]);
		Assert.Equal(123, BinaryPrimitives.ReadInt32LittleEndian(playerAuthResponse.AsSpan(1, 4)));
		Assert.Equal(48, playerAuthResponse[5]);
		Assert.Equal(48, playerAuthResponse.Length - 6);
		Assert.NotNull(chatService.GetPlayer(123));
	}

	[Fact]
	public async Task GameServerConnection_HandlerFailureDoesNotCloseSocket()
	{
		var options = new ChatServerOptions
		{
			GameServerPassword = "secret",
			ClientConnectEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 10241)
		};
		var channels = new ChatChannels(NullLogger<ChatChannels>.Instance);
		var broadcast = new BroadcastService(NullLogger<BroadcastService>.Instance);
		var innerChatService = new ChatService(channels, broadcast, NullLogger<ChatService>.Instance);
		var chatService = new ThrowingOnceChatService(innerChatService);
		var gameServerService = new GameServerService(options, NullLogger<GameServerService>.Instance);

		await using var harness = await SocketHarness.ConnectAsync(
			serverClient => new GsConnection(
				NullLogger.Instance,
				serverClient,
				"gs-handler-failure",
				gameServerService,
				chatService,
				options));

		await harness.ClientStream.WriteAsync(ChatPacketFrameCodec.CreateFrame(Packet(w => w.C(GsPacketFactory.CmChatServerAuth).C(1).S("secret"))));
		Assert.Equal([0x00, 0x00, 0x04, 0x7F, 0x00, 0x00, 0x01, 0x01, 0x28], await ReadPayloadAsync(harness.ClientStream));

		await harness.ClientStream.WriteAsync(
			ChatPacketFrameCodec.CreateFrame(
				Packet(w => w.C(GsPacketFactory.CmPlayerAuth).D(123).S("account").S("First").D((int)Race.Elyos).C(0))));
		await harness.ClientStream.WriteAsync(
			ChatPacketFrameCodec.CreateFrame(
				Packet(w => w.C(GsPacketFactory.CmPlayerAuth).D(124).S("account").S("Second").D((int)Race.Elyos).C(0))));

		var playerAuthResponse = await ReadPayloadAsync(harness.ClientStream);
		Assert.Equal(GsPacketFactory.SmPlayerAuthResponse, playerAuthResponse[0]);
		Assert.Equal(124, BinaryPrimitives.ReadInt32LittleEndian(playerAuthResponse.AsSpan(1, 4)));
		Assert.Null(innerChatService.GetPlayer(123));
		Assert.NotNull(innerChatService.GetPlayer(124));
	}

	[Fact]
	public async Task ClientConnection_CompletesChatInitAuthAndChannelRequestOverSocket()
	{
		var options = new ChatServerOptions();
		var channels = new ChatChannels(NullLogger<ChatChannels>.Instance);
		var broadcast = new BroadcastService(NullLogger<BroadcastService>.Instance);
		var chatService = new ChatService(channels, broadcast, NullLogger<ChatService>.Instance);
		var client = chatService.RegisterPlayer(123, "account", "Daeva", Race.Elyos, 0);
		var handlerRegistry = CreateHandlerRegistry(options, new NullChatLogRepository());

		await using var harness = await SocketHarness.ConnectAsync(
			serverClient => new ClientChannelHandler(
				NullLogger.Instance,
				serverClient,
				"client-smoke",
				chatService,
				channels,
				broadcast,
				handlerRegistry,
				options));

		await harness.ClientStream.WriteAsync(ChatPacketFrameCodec.CreateFrame(Packet(w => w.C(ClientPacketFactory.CmChatIni).C(0x40).H(0).D(0).D(0).D(0))));
		Assert.Equal([0x31, 0x40, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00], await ReadPayloadAsync(harness.ClientStream));

		await harness.ClientStream.WriteAsync(ChatPacketFrameCodec.CreateFrame(BuildClientAuthPayload(client)));
		Assert.Equal([0x02, 0x40, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x22, 0x08], await ReadPayloadAsync(harness.ClientStream));

		const string identifier = "@\u0001public_ALL\u00011.0.AION.KOR";
		await harness.ClientStream.WriteAsync(
			ChatPacketFrameCodec.CreateFrame(
				Packet(w => w.C(ClientPacketFactory.CmChannelRequest).C(0x40).H(0).D(77).Bytes(new byte[16]).Utf16LengthBytes(identifier).D(0))));
		var channelResponse = await ReadPayloadAsync(harness.ClientStream);

		Assert.Equal(0x11, channelResponse[0]);
		Assert.Equal(0x40, channelResponse[1]);
		Assert.Equal(77, BinaryPrimitives.ReadInt32LittleEndian(channelResponse.AsSpan(2, 4)));
		Assert.NotEqual(0, BinaryPrimitives.ReadInt32LittleEndian(channelResponse.AsSpan(8, 4)));
	}

	[Fact]
	public async Task ClientConnection_BroadcastsChannelMessagesToChannelMembers()
	{
		var options = new ChatServerOptions();
		var channels = new ChatChannels(NullLogger<ChatChannels>.Instance);
		var broadcast = new BroadcastService(NullLogger<BroadcastService>.Instance);
		var chatService = new ChatService(channels, broadcast, NullLogger<ChatService>.Instance);
		var firstClient = chatService.RegisterPlayer(123, "account", "Daeva", Race.Elyos, 0);
		var secondClient = chatService.RegisterPlayer(124, "account2", "Other", Race.Elyos, 0);
		var chatLogRepository = new NullChatLogRepository();
		var handlerRegistry = CreateHandlerRegistry(options, chatLogRepository);

		await using var first = await SocketHarness.ConnectAsync(
			serverClient => new ClientChannelHandler(
				NullLogger.Instance,
				serverClient,
				"client-one",
				chatService,
				channels,
				broadcast,
				handlerRegistry,
				options));
		await using var second = await SocketHarness.ConnectAsync(
			serverClient => new ClientChannelHandler(
				NullLogger.Instance,
				serverClient,
				"client-two",
				chatService,
				channels,
				broadcast,
				handlerRegistry,
				options));

		var channelId = await AuthenticateAndJoinAsync(first.ClientStream, firstClient, requestId: 1);
		var secondChannelId = await AuthenticateAndJoinAsync(second.ClientStream, secondClient, requestId: 2);
		Assert.Equal(channelId, secondChannelId);

		await first.ClientStream.WriteAsync(
			ChatPacketFrameCodec.CreateFrame(
				Packet(w => w.C(ClientPacketFactory.CmChannelMessage).H(0).C(0).D(0).D(0).D(0).D(0).D(channelId).C(0).Utf16LengthBytes("Hello"))));

		var firstPayload = await ReadPayloadAsync(first.ClientStream);
		var secondPayload = await ReadPayloadAsync(second.ClientStream);
		Assert.Equal(0x1A, firstPayload[0]);
		Assert.Equal(0x1A, secondPayload[0]);
		Assert.Equal("Hello", ExtractChannelMessageText(firstPayload));
		Assert.Equal("Hello", ExtractChannelMessageText(secondPayload));
	}

	[Fact]
	public async Task ClientConnection_RejectsFrameAboveJavaSixteenKiBLimitFromHeader()
	{
		var options = new ChatServerOptions();
		var channels = new ChatChannels(NullLogger<ChatChannels>.Instance);
		var broadcast = new BroadcastService(NullLogger<BroadcastService>.Instance);
		var chatService = new ChatService(channels, broadcast, NullLogger<ChatService>.Instance);
		var handlerRegistry = CreateHandlerRegistry(options, new NullChatLogRepository());
		await using var harness = await SocketHarness.ConnectAsync(
			serverClient => new ClientChannelHandler(
				NullLogger.Instance,
				serverClient,
				"oversized-client-frame",
				chatService,
				channels,
				broadcast,
				handlerRegistry,
				options));
		var header = new byte[2];
		BinaryPrimitives.WriteUInt16LittleEndian(
			header,
			ChatFrameLimits.MaxPacketLength + 1);

		await harness.ClientStream.WriteAsync(header);

		await harness.WaitForConnectionCloseAsync();
	}

	private static async Task<int> AuthenticateAndJoinAsync(NetworkStream stream, ChatClient client, int requestId)
	{
		const string identifier = "@\u0001public_ALL\u00011.0.AION.KOR";
		await stream.WriteAsync(ChatPacketFrameCodec.CreateFrame(BuildClientAuthPayload(client)));
		Assert.Equal([0x02, 0x40, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x22, 0x08], await ReadPayloadAsync(stream));
		await stream.WriteAsync(
			ChatPacketFrameCodec.CreateFrame(
				Packet(w => w.C(ClientPacketFactory.CmChannelRequest).C(0x40).H(0).D(requestId).Bytes(new byte[16]).Utf16LengthBytes(identifier).D(0))));
		var channelResponse = await ReadPayloadAsync(stream);
		Assert.Equal(0x11, channelResponse[0]);
		return BinaryPrimitives.ReadInt32LittleEndian(channelResponse.AsSpan(8, 4));
	}

	private static byte[] BuildClientAuthPayload(ChatClient client)
	{
		var identifier = $"{client.Name}@\u0001public_ALL\u00011.0.AION.KOR";
		return Packet(
			w => w.C(ClientPacketFactory.CmPlayerAuth)
				.Utf16Bytes("@")
				.C(0)
				.D(1)
				.Utf16LengthBytes("AION")
				.D(27)
				.D(1)
				.D(0)
				.D(client.ClientId)
				.D(0)
				.D(0)
				.D(0)
				.Utf16LengthBytes(identifier)
				.Utf16LengthBytes(client.AccountName.ToLowerInvariant())
				.H(client.Token.Length)
				.Bytes(client.Token));
	}

	private static string ExtractChannelMessageText(byte[] payload)
	{
		var offset = 1 + 1 + 4 + 4 + 4 + 4 + 4 + 1;
		var identifierChars = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(offset, 2));
		offset += 2 + (identifierChars * 2);
		var textChars = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(offset, 2));
		offset += 2;
		return Encoding.Unicode.GetString(payload.AsSpan(offset, textChars * 2));
	}

	private static byte[] Packet(Action<ByteWriter> write)
	{
		var writer = new ByteWriter();
		write(writer);
		return writer.ToArray();
	}

	private static ChatHandlerRegistry CreateHandlerRegistry(ChatServerOptions options, IChatLogRepository chatLogRepository)
	{
		IChatMessageHandler[] handlers =
		[
			new FloodProtectionHandler(),
			new FilterHandler(options),
			new LoggingHandler(options, chatLogRepository, NullLogger<LoggingHandler>.Instance),
		];
		return new ChatHandlerRegistry(handlers, NullLogger<ChatHandlerRegistry>.Instance);
	}

	private static async Task<byte[]> ReadPayloadAsync(NetworkStream stream)
	{
		var header = await ReadExactAsync(stream, 2);
		var length = BinaryPrimitives.ReadUInt16LittleEndian(header);
		var payload = await ReadExactAsync(stream, length - 2);
		return payload;
	}

	private static async Task<byte[]> ReadExactAsync(NetworkStream stream, int length)
	{
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var buffer = new byte[length];
		var offset = 0;
		while (offset < length)
		{
			var read = await stream.ReadAsync(buffer.AsMemory(offset, length - offset), cts.Token);
			if (read == 0)
				throw new EndOfStreamException("Socket closed before the expected frame was read.");
			offset += read;
		}
		return buffer;
	}

	private sealed class NullChatLogRepository : IChatLogRepository
	{
		public Task InsertChatLogAsync(string sender, string message, string type, CancellationToken cancellationToken = default) => Task.CompletedTask;
	}

	private sealed class ThrowingOnceChatService : IChatService
	{
		private readonly IChatService _inner;
		private bool _throwOnRegister = true;

		public ThrowingOnceChatService(IChatService inner)
		{
			_inner = inner;
		}

		public ChatClient RegisterPlayer(int playerId, string accountName, string nick, Race race, byte accessLevel)
		{
			if (_throwOnRegister)
			{
				_throwOnRegister = false;
				throw new InvalidOperationException("Injected chat gameserver handler failure.");
			}

			return _inner.RegisterPlayer(playerId, accountName, nick, race, accessLevel);
		}

		public ChatClient? GetPlayer(int playerId) => _inner.GetPlayer(playerId);

		public bool RegisterPlayerConnection(int playerId, byte[] token, byte[] identifier, string name, string accountName, IChatClientConnection connection)
		{
			return _inner.RegisterPlayerConnection(playerId, token, identifier, name, accountName, connection);
		}

		public Channel? RegisterPlayerWithChannel(ChatClient client, int channelRequestId, string identifier)
		{
			return _inner.RegisterPlayerWithChannel(client, channelRequestId, identifier);
		}

		public ChatClient? PlayerLogout(int playerId) => _inner.PlayerLogout(playerId);

		public void GagPlayer(int playerId, long gagTimeMillis) => _inner.GagPlayer(playerId, gagTimeMillis);
	}

	private sealed class SocketHarness : IAsyncDisposable
	{
		private readonly TcpListener _listener;
		private readonly TcpClient _serverClient;
		private readonly Task _connectionTask;

		private SocketHarness(TcpListener listener, TcpClient client, TcpClient serverClient, Task connectionTask)
		{
			_listener = listener;
			Client = client;
			_serverClient = serverClient;
			_connectionTask = connectionTask;
			ClientStream = Client.GetStream();
		}

		public TcpClient Client { get; }

		public NetworkStream ClientStream { get; }

		public Task WaitForConnectionCloseAsync()
		{
			return _connectionTask.WaitAsync(TimeSpan.FromSeconds(5));
		}

		public static async Task<SocketHarness> ConnectAsync(Func<TcpClient, Aion.Commons.Network.Server.BaseClientConnection> connectionFactory)
		{
			var listener = new TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			var endpoint = (IPEndPoint)listener.LocalEndpoint;
			var client = new TcpClient();
			var acceptTask = listener.AcceptTcpClientAsync();
			await client.ConnectAsync(endpoint.Address, endpoint.Port);
			var serverClient = await acceptTask;
			var connection = connectionFactory(serverClient);
			var connectionTask = connection.RunAsync();
			return new SocketHarness(listener, client, serverClient, connectionTask);
		}

		public async ValueTask DisposeAsync()
		{
			Client.Close();
			_serverClient.Close();
			_listener.Stop();
			await Task.WhenAny(_connectionTask, Task.Delay(TimeSpan.FromSeconds(2)));
			Client.Dispose();
			_serverClient.Dispose();
		}
	}

	private sealed class ByteWriter
	{
		private readonly List<byte> _bytes = [];

		public ByteWriter C(int value)
		{
			_bytes.Add((byte)value);
			return this;
		}

		public ByteWriter H(int value)
		{
			_bytes.Add((byte)value);
			_bytes.Add((byte)(value >> 8));
			return this;
		}

		public ByteWriter D(int value)
		{
			_bytes.Add((byte)value);
			_bytes.Add((byte)(value >> 8));
			_bytes.Add((byte)(value >> 16));
			_bytes.Add((byte)(value >> 24));
			return this;
		}

		public ByteWriter Bytes(byte[] bytes)
		{
			_bytes.AddRange(bytes);
			return this;
		}

		public ByteWriter Utf16Bytes(string value)
		{
			_bytes.AddRange(Encoding.Unicode.GetBytes(value));
			return this;
		}

		public ByteWriter Utf16LengthBytes(string value)
		{
			H(value.Length);
			return Utf16Bytes(value);
		}

		public ByteWriter S(string value)
		{
			Utf16Bytes(value);
			H(0);
			return this;
		}

		public byte[] ToArray() => _bytes.ToArray();
	}
}
