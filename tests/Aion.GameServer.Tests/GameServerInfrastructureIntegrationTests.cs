using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Aion.GameServer.Configuration;
using Aion.GameServer.Data;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Dataholders.LoadingUtils;
using Aion.GameServer.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.IdFactory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using GameChatServer = Aion.GameServer.Network.ChatServer.ChatServer;
using GameLoginServer = Aion.GameServer.Network.LoginServer.LoginServer;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Tests;

public sealed class GameServerInfrastructureIntegrationTests
{
	[Fact]
	public async Task HostedInfrastructure_LoadsDataStartsListenerAndAuthenticatesBridges()
	{
		await using var loginServer = await MockBridgeServer.StartAsync(CreateLoginAuthResponseFrame(1));
		await using var chatServer = await MockBridgeServer.StartAsync(CreateChatAuthResponseFrame(IPAddress.Loopback, 10241));
		using var staticData = StaticDataFixture.Create();
		using var host = CreateHost(staticData, loginServer.EndPoint, chatServer.EndPoint);

		await host.StartAsync();
		try
		{
			var gameSocketServer = host.Services.GetRequiredService<GameClientSocketServer>();
			var loginConnector = host.Services.GetRequiredService<GameLoginServer>();
			var chatConnector = host.Services.GetRequiredService<GameChatServer>();

			var loginFrame = await loginServer.ReadClientFrameAsync();
			var chatFrame = await chatServer.ReadClientFrameAsync();
			await WaitUntilAsync(() => loginConnector.IsAuthed && chatConnector.IsAuthed);

			var clientEndpoint = await WaitForEndpointAsync(() => gameSocketServer.LocalEndPoint);
			using var client = new TcpClient();
			await client.ConnectAsync(clientEndpoint.Address, clientEndpoint.Port);
			await using var stream = client.GetStream();
			var smKeyFrame = await ReadFrameAsync(stream);

			Assert.True(staticData.Loaded);
			Assert.Equal(1, staticData.LoadedData!.StaticData.GetElementCount("item"));
			Assert.Equal(Convert.FromHexString("1A00000131003200330034000000047F000001611E0064000000"), loginFrame);
			Assert.Equal(Convert.FromHexString("120000017300650063007200650074000000"), chatFrame);
			Assert.Equal(Convert.FromHexString("C8014437FE"), smKeyFrame[2..7]);
			Assert.Equal(new IPEndPoint(IPAddress.Loopback, 10241), chatConnector.PublicEndPoint);
		}
		finally
		{
			await host.StopAsync();
		}
	}

	private static IHost CreateHost(StaticDataFixture staticData, IPEndPoint loginEndPoint, IPEndPoint chatEndPoint)
	{
		var options = new GameServerOptions
		{
			Core = new GameServerCoreOptions
			{
				EnableChatServer = true,
			},
			Network = new GameServerNetworkOptions
			{
				ClientEndPoint = new IPEndPoint(IPAddress.Loopback, 0),
				ClientConnectEndPoint = new IPEndPoint(IPAddress.Loopback, 7777),
				LoginEndPoint = loginEndPoint,
				ChatEndPoint = chatEndPoint,
				GameServerId = 1,
				LoginPassword = "1234",
				ChatPassword = "secret",
				MaxOnlinePlayers = 100,
			},
		};

		return Host.CreateDefaultBuilder()
			.ConfigureLogging(logging => logging.ClearProviders())
			.ConfigureServices(
				services =>
				{
					services.AddSingleton(options);
					services.AddSingleton<ThreadPoolManager>();
					services.AddSingleton<IDFactory>();
					services.AddSingleton<IUsedIdRepository, EmptyUsedIdRepository>();
					services.AddSingleton<GameServerRuntimeContext>();
					services.AddSingleton<GameWorld>();
					services.AddSingleton(
						serviceProvider => new GameTimeService(
							serviceProvider.GetRequiredService<ILogger<GameTimeService>>(),
							serviceProvider.GetRequiredService<ThreadPoolManager>(),
							TimeSpan.FromMilliseconds(100),
							TimeSpan.FromMilliseconds(100)));
					services.AddSingleton<IStaticDataLoader>(staticData);
					services.AddSingleton<GamePacketProcessor<string>>(_ => new GamePacketProcessor<string>((packet, cancellationToken) => Task.CompletedTask));
					services.AddSingleton<GameLoginServer>();
					services.AddSingleton<GameChatServer>();
					services.AddHostedService<GameServerBootstrapService>();
					services.AddSingleton<GameClientSocketServer>();
					services.AddHostedService<GameServerHostedService>();
					services.AddHostedService<GameBridgeHostedService>();
				})
			.Build();
	}

	private static byte[] CreateLoginAuthResponseFrame(byte gameServerCount)
	{
		return BridgePacketFrameCodec.CreateFrame(new byte[] { 0x00, 0x00, gameServerCount });
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
		return BridgePacketFrameCodec.CreateFrame(payload);
	}

	private static async Task<IPEndPoint> WaitForEndpointAsync(Func<IPEndPoint?> getEndpoint)
	{
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		while (!timeout.IsCancellationRequested)
		{
			var endpoint = getEndpoint();
			if (endpoint is { Port: > 0 })
				return endpoint;
			await Task.Delay(20, timeout.Token);
		}

		throw new TimeoutException("Game client listener did not bind before timeout.");
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

	private sealed class StaticDataFixture : IStaticDataLoader, IDisposable
	{
		private StaticDataFixture(string path)
		{
			Path = path;
		}

		public string Path { get; }

		public bool Loaded { get; private set; }

		public DataManager? LoadedData { get; private set; }

		public static StaticDataFixture Create()
		{
			var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aion-hosted-infra-" + Guid.NewGuid().ToString("N"));
			var dataDirectory = Directory.CreateDirectory(System.IO.Path.Combine(path, "data", "static_data"));
			var itemsDirectory = Directory.CreateDirectory(System.IO.Path.Combine(dataDirectory.FullName, "items"));
			File.WriteAllText(
				System.IO.Path.Combine(dataDirectory.FullName, "static_data.xml"),
				"""
				<?xml version="1.0" encoding="UTF-8"?>
				<static_data>
					<import file="items/items.xml" />
				</static_data>
				""");
			File.WriteAllText(System.IO.Path.Combine(itemsDirectory.FullName, "items.xml"), """<items><item id="1" /></items>""");
			File.WriteAllText(System.IO.Path.Combine(dataDirectory.FullName, "static_data.xsd"), """<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema" />""");
			return new StaticDataFixture(path);
		}

		public async Task<DataManager> LoadAsync(CancellationToken cancellationToken = default)
		{
			Loaded = true;
			LoadedData = await DataManager.LoadAsync(
				new XmlDataLoaderOptions
				{
					MainXmlFilePath = System.IO.Path.Combine(Path, "data", "static_data", "static_data.xml"),
					CacheXmlFilePath = System.IO.Path.Combine(Path, "cache", "static_data.xml"),
					SchemaFilePath = System.IO.Path.Combine(Path, "data", "static_data", "static_data.xsd"),
					ValidateWhenCacheChanges = false,
				},
				cancellationToken: cancellationToken);
			return LoadedData;
		}

		public void Dispose()
		{
			try
			{
				Directory.Delete(Path, recursive: true);
			}
			catch
			{
			}
		}
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
}
