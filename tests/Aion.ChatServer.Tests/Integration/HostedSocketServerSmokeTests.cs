using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Aion.ChatServer.Configuration;
using Aion.ChatServer.Data.Repositories;
using Aion.ChatServer.Handlers;
using Aion.ChatServer.Handlers.BuiltIn;
using Aion.ChatServer.Models.Channels;
using Aion.ChatServer.Network;
using Aion.ChatServer.Network.Packets;
using Aion.ChatServer.Network.Packets.GameServer;
using Aion.ChatServer.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.ChatServer.Tests.Integration;

public class HostedSocketServerSmokeTests
{
	[Fact]
	public async Task ClientSocketServer_AcceptsFramedChatInit()
	{
		var options = new ChatServerOptions { ClientEndPoint = new IPEndPoint(IPAddress.Loopback, 0) };
		var channels = new ChatChannels(NullLogger<ChatChannels>.Instance);
		var broadcast = new BroadcastService(NullLogger<BroadcastService>.Instance);
		var chatService = new ChatService(channels, broadcast, NullLogger<ChatService>.Instance);
		var chatLogRepository = new NullChatLogRepository();
		var server = new ClientSocketServer(
			NullLogger<ClientSocketServer>.Instance,
			options,
			chatService,
			channels,
			broadcast,
			CreateHandlerRegistry(options, chatLogRepository));
		var serverTask = Task.Run(() => server.StartAsync());

		try
		{
			var endpoint = await WaitForEndpointAsync(() => server.LocalEndPoint);
			using var client = new TcpClient();
			await client.ConnectAsync(endpoint.Address, endpoint.Port);
			await using var stream = client.GetStream();

			await stream.WriteAsync(ChatPacketFrameCodec.CreateFrame(Packet(w => w.C(ClientPacketFactory.CmChatIni).C(0x40).H(0).D(0).D(0).D(0))));

			Assert.Equal([0x31, 0x40, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00], await ReadPayloadAsync(stream));
		}
		finally
		{
			await server.StopAsync();
			await Task.WhenAny(serverTask, Task.Delay(TimeSpan.FromSeconds(2)));
		}
	}

	[Fact]
	public async Task GameServerSocketServer_AcceptsAuth()
	{
		var options = new ChatServerOptions
		{
			GameServerPassword = "secret",
			GameServerEndPoint = new IPEndPoint(IPAddress.Loopback, 0),
			ClientConnectEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 10241),
		};
		var channels = new ChatChannels(NullLogger<ChatChannels>.Instance);
		var broadcast = new BroadcastService(NullLogger<BroadcastService>.Instance);
		var chatService = new ChatService(channels, broadcast, NullLogger<ChatService>.Instance);
		var gameServerService = new GameServerService(options, NullLogger<GameServerService>.Instance);
		var server = new GameServerSocketServer(NullLogger<GameServerSocketServer>.Instance, options, gameServerService, chatService);
		var serverTask = Task.Run(() => server.StartAsync());

		try
		{
			var endpoint = await WaitForEndpointAsync(() => server.LocalEndPoint);
			using var client = new TcpClient();
			await client.ConnectAsync(endpoint.Address, endpoint.Port);
			await using var stream = client.GetStream();

			await stream.WriteAsync(ChatPacketFrameCodec.CreateFrame(Packet(w => w.C(GsPacketFactory.CmChatServerAuth).C(1).S("secret"))));

			Assert.Equal([0x00, 0x00, 0x04, 0x7F, 0x00, 0x00, 0x01, 0x01, 0x28], await ReadPayloadAsync(stream));
			Assert.True(gameServerService.IsOnline);
		}
		finally
		{
			await server.StopAsync();
			await Task.WhenAny(serverTask, Task.Delay(TimeSpan.FromSeconds(2)));
		}
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

	private static async Task<IPEndPoint> WaitForEndpointAsync(Func<IPEndPoint?> getEndpoint)
	{
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		while (!cts.IsCancellationRequested)
		{
			var endpoint = getEndpoint();
			if (endpoint is { Port: > 0 })
				return endpoint;
			await Task.Delay(20, cts.Token);
		}

		throw new TimeoutException("Socket server did not bind before timeout.");
	}

	private static byte[] Packet(Action<ByteWriter> write)
	{
		var writer = new ByteWriter();
		write(writer);
		return writer.ToArray();
	}

	private static async Task<byte[]> ReadPayloadAsync(NetworkStream stream)
	{
		var header = await ReadExactAsync(stream, 2);
		var length = BinaryPrimitives.ReadUInt16LittleEndian(header);
		return await ReadExactAsync(stream, length - 2);
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

		public ByteWriter Utf16Bytes(string value)
		{
			_bytes.AddRange(Encoding.Unicode.GetBytes(value));
			return this;
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
