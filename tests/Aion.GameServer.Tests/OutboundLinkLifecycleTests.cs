using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.LoginServer;
using Aion.GameServer.Network.LoginServer.ServerPackets;
using Aion.GameServer.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using GameChatServer = Aion.GameServer.Network.ChatServer.ChatServer;
using GameLoginServer = Aion.GameServer.Network.LoginServer.LoginServer;

namespace Aion.GameServer.Tests;

public sealed class OutboundLinkLifecycleTests
{
	private static readonly OutboundLinkRetryDelays FastRetries = new(
		TimeSpan.FromMilliseconds(15),
		TimeSpan.FromMilliseconds(15),
		TimeSpan.FromMilliseconds(15),
		TimeSpan.FromMilliseconds(15));

	[Fact]
	public async Task LoginConnector_Reconnects_CleansPendingRequests_AndResendsRetainedAccounts()
	{
		var dispatcher = new HardwareListRecordingDispatcher(expectedPackets: 4);
		await using var server = await TwoSessionLoginServer.StartAsync();
		await using var connector = new GameLoginServer(
			NullLogger<GameLoginServer>.Instance,
			CreateOptions(loginEndPoint: server.EndPoint),
			characterSelectionRepository: null,
			dispatcher,
			FastRetries);

		var retainedConnection = NewRecordingConnection();
		GetLoggedInAccounts(connector)[42] = retainedConnection;
		await connector.StartAsync();

		var firstSnapshot = await server.ReadFirstAccountListAsync();
		var pendingConnection = NewRecordingConnection();
		connector.RegisterLoginRequest(77, pendingConnection, 1, 2, 3);
		var pendingAuth = connector.RequestAccountAuthAsync(88, 4, 5, 6);
		await server.ReadFirstAccountAuthAsync();
		server.DropFirstSession();

		await Assert.ThrowsAsync<IOException>(async () =>
			await pendingAuth.WaitAsync(TimeSpan.FromSeconds(5)));
		var secondSnapshot = await server.ReadSecondAccountListAsync();
		await dispatcher.WaitForExpectedPacketsAsync();
		await WaitUntilAsync(() => connector.IsAuthed);

		Assert.Equal(new[] { 42 }, ReadAccountIds(firstSnapshot));
		Assert.Equal(new[] { 42 }, ReadAccountIds(secondSnapshot));
		Assert.Equal(1, pendingConnection.CloseCount);
		Assert.Equal(0, GetPrivateCollectionCount(connector, "_loginRequests"));
		Assert.Single(GetLoggedInAccounts(connector));
		Assert.Equal(2, dispatcher.MacListCount);
		Assert.Equal(2, dispatcher.HddListCount);

		await connector.StopAsync();
		await Task.Delay(75);
		Assert.Equal(2, server.AcceptCount);
	}

	[Fact]
	public async Task ChatConnector_Reconnects_AndClearsPublicEndpointBetweenSessions()
	{
		await using var server = await TwoSessionChatServer.StartAsync();
		await using var connector = new GameChatServer(
			NullLogger<GameChatServer>.Instance,
			CreateOptions(chatEndPoint: server.EndPoint, chatPassword: "secret"),
			FastRetries);

		await connector.StartAsync();
		await WaitUntilAsync(() => connector.PublicEndPoint?.Port == 10241);
		using (var canceledSend = new CancellationTokenSource())
		{
			canceledSend.Cancel();
			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => connector.SendPlayerLoginRequestAsync(
				7001,
				"account-one",
				"Kahrun",
				raceId: 0,
				accessLevel: 0,
				sendChatInit: _ => Task.CompletedTask,
				cancellationToken: canceledSend.Token));
		}
		Assert.Equal(0, GetPrivateCollectionCount(connector, "_playerAuthCallbacks"));

		server.DropFirstSession();
		await server.WaitForSecondAuthAsync();

		Assert.Equal(Aion.GameServer.Network.ChatServer.ChatServerState.Connected, connector.State);
		Assert.Null(connector.PublicEndPoint);
		Assert.Empty(connector.GetPublicIP());
		Assert.Equal(0, connector.GetPublicPort());

		server.AuthenticateSecondSession();
		await WaitUntilAsync(() => connector.PublicEndPoint?.Port == 10242);
		Assert.True(connector.IsAuthed);

		await connector.StopAsync();
		await Task.Delay(75);
		Assert.Equal(2, server.AcceptCount);
	}

	[Fact]
	public async Task HostedService_KeepsRetryingUntilLateLoginServerStarts_AndStopsCleanly()
	{
		var endpoint = ReserveEndpoint();
		var options = CreateOptions(loginEndPoint: endpoint);
		await using var login = new GameLoginServer(
			NullLogger<GameLoginServer>.Instance,
			options,
			characterSelectionRepository: null,
			inboundPacketDispatcher: null,
			FastRetries);
		await using var chat = new GameChatServer(NullLogger<GameChatServer>.Instance, options, FastRetries);
		var hosted = new OutboundLinkHostedService(
			login,
			chat,
			options,
			NullLogger<OutboundLinkHostedService>.Instance);

		await hosted.StartAsync(CancellationToken.None);
		await Task.Delay(60); // Let at least one connection-refused retry occur before the service appears.
		await using var server = await HoldingLoginServer.StartAsync(endpoint);
		await server.ReadAccountListAsync();
		await WaitUntilAsync(() => login.IsAuthed);

		using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		await hosted.StopAsync(stopTimeout.Token);
		Assert.Equal(Aion.GameServer.Network.LoginServer.LoginServerState.Disconnected, login.State);
		await Assert.ThrowsAsync<InvalidOperationException>(() => login.StartAsync());
	}

	[Fact]
	public async Task LoginConnector_HandlerFailure_DoesNotKillTheCurrentSession()
	{
		var dispatcher = new ThrowingDispatcher();
		await using var server = await PacketIsolationLoginServer.StartAsync();
		await using var connector = new GameLoginServer(
			NullLogger<GameLoginServer>.Instance,
			CreateOptions(loginEndPoint: server.EndPoint),
			characterSelectionRepository: null,
			dispatcher,
			FastRetries);

		await connector.StartAsync();
		var pong = await server.ReadPongAsync();

		Assert.Equal(Convert.FromHexString("03000C"), pong);
		Assert.Equal(1, dispatcher.DispatchCount);
		Assert.True(connector.IsAuthed);
	}

	[Fact]
	public async Task LoginConnector_RejectsApplicationPacketsUntilAuthenticated()
	{
		await using var server = await GatedLoginServer.StartAsync();
		await using var connector = new GameLoginServer(
			NullLogger<GameLoginServer>.Instance,
			CreateOptions(loginEndPoint: server.EndPoint),
			characterSelectionRepository: null,
			inboundPacketDispatcher: null,
			FastRetries);

		await connector.StartAsync();
		await server.WaitForAuthRequestAsync();
		Assert.Equal(LoginServerState.Connected, connector.State);
		Assert.False(connector.SendPacket(new SmLsPong()));
		await Assert.ThrowsAsync<InvalidOperationException>(() => connector.SendPacketAsync(new SmLsPong()));

		server.AllowAuthentication();
		await server.ReadAccountListAsync();
		await WaitUntilAsync(() => connector.IsAuthed);
	}

	[Fact]
	public async Task LoginConnector_WhenDown_ClosesClientWithJavaProtocolFailure()
	{
		await using var connector = new GameLoginServer(
			NullLogger<GameLoginServer>.Instance,
			CreateOptions(),
			characterSelectionRepository: null);
		var client = NewRecordingConnection();

		connector.AuthenticateClient(client);

		var failure = Assert.IsType<Aion.GameServer.Network.Aion.ServerPackets.SM_L2AUTH_LOGIN_CHECK>(client.ClosePacket);
		Assert.NotNull(failure);
		Assert.Equal(0, client.CloseCount);
	}

	[Fact]
	public async Task BackgroundBridgeSendFailuresAreObservedAndLogged()
	{
		var logger = new RecordingLogger();

		await OutboundLinkSendObserver.ObserveAsync(
			Task.FromException(new IOException("async send failed")),
			logger,
			"login server",
			"TestPacket");
		OutboundLinkSendObserver.Observe(
			() => throw new IOException("sync send failed"),
			logger,
			"chat server",
			"TestPacket");

		Assert.Equal(2, logger.WarningCount);
	}

	[Theory]
	[InlineData(2, false)]
	[InlineData(3, true)]
	[InlineData(ChatFrameLimits.MaxPacketLength, true)]
	[InlineData(ChatFrameLimits.MaxPacketLength + 1, false)]
	public void ChatBridgeReaderUsesJavaFrameBoundary(int frameLength, bool expected)
	{
		Assert.Equal(expected, GameChatServer.IsSupportedFrameLength(frameLength));
	}

	private static GameServerOptions CreateOptions(
		IPEndPoint? loginEndPoint = null,
		IPEndPoint? chatEndPoint = null,
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
				LoginPassword = "1234",
				ChatPassword = chatPassword,
				MaxOnlinePlayers = 100,
			},
		};
	}

	private static IPEndPoint ReserveEndpoint()
	{
		var listener = new TcpListener(IPAddress.Loopback, 0);
		listener.Start();
		var endpoint = (IPEndPoint)listener.LocalEndpoint;
		listener.Stop();
		return endpoint;
	}

	private static RecordingAionConnection NewRecordingConnection()
	{
		return (RecordingAionConnection)RuntimeHelpers.GetUninitializedObject(typeof(RecordingAionConnection));
	}

	private static ConcurrentDictionary<int, AionConnection> GetLoggedInAccounts(GameLoginServer connector)
	{
		return (ConcurrentDictionary<int, AionConnection>)(typeof(GameLoginServer).GetField(
			"_loggedInAccounts",
			BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(connector)
			?? throw new MissingFieldException(typeof(GameLoginServer).FullName, "_loggedInAccounts"));
	}

	private static int GetPrivateCollectionCount(object owner, string fieldName)
	{
		var value = owner.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(owner)
			?? throw new MissingFieldException(owner.GetType().FullName, fieldName);
		return (int)(value.GetType().GetProperty("Count")?.GetValue(value)
			?? throw new MissingMemberException(value.GetType().FullName, "Count"));
	}

	private static int[] ReadAccountIds(byte[] frame)
	{
		using var payload = new PacketBuffer(frame.AsSpan(2).ToArray());
		Assert.Equal(0x04, payload.ReadC());
		var count = payload.ReadD();
		var accountIds = Enumerable.Range(0, count).Select(_ => payload.ReadD()).ToArray();
		Assert.Equal(0, payload.Remaining);
		return accountIds;
	}

	private static byte[] LoginAuthResponse(byte gameServerCount = 1)
	{
		return ServerPacketFrameCodec.CreateFrame(new byte[] { 0x00, 0x00, gameServerCount });
	}

	private static byte[] ChatAuthResponse(int port)
	{
		using var payload = new PacketBuffer();
		payload.WriteC(0x00);
		payload.WriteC(0x00);
		var address = IPAddress.Loopback.GetAddressBytes();
		payload.WriteC(address.Length);
		payload.WriteB(address);
		payload.WriteH(port);
		return ServerPacketFrameCodec.CreateFrame(payload.ToArray());
	}

	private static async Task WaitUntilAsync(Func<bool> condition)
	{
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		while (!condition())
			await Task.Delay(10, timeout.Token);
	}

	private static async Task<byte[]> ReadFrameAsync(NetworkStream stream)
	{
		var header = await ReadExactAsync(stream, 2);
		var frameLength = BinaryPrimitives.ReadUInt16LittleEndian(header);
		var frame = new byte[frameLength];
		header.CopyTo(frame, 0);
		(await ReadExactAsync(stream, frameLength - 2)).CopyTo(frame.AsSpan(2));
		return frame;
	}

	private static async Task<byte[]> ReadExactAsync(NetworkStream stream, int length)
	{
		var result = new byte[length];
		var offset = 0;
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		while (offset < length)
		{
			var read = await stream.ReadAsync(result.AsMemory(offset, length - offset), timeout.Token);
			if (read == 0)
				throw new EndOfStreamException("Bridge closed before the expected frame was read.");
			offset += read;
		}
		return result;
	}

	private sealed class RecordingAionConnection : AionConnection
	{
		private int _closeCount;
		private AionServerPacket? _closePacket;

		private RecordingAionConnection()
			: base(null!, null!)
		{
		}

		public int CloseCount => Volatile.Read(ref _closeCount);
		public AionServerPacket? ClosePacket => Volatile.Read(ref _closePacket);

		public override void Close()
		{
			Interlocked.Increment(ref _closeCount);
		}

		public override void Close(AionServerPacket? closePacket)
		{
			Volatile.Write(ref _closePacket, closePacket);
		}
	}

	private sealed class RecordingLogger : ILogger
	{
		private int _warningCount;

		public int WarningCount => Volatile.Read(ref _warningCount);

		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(
			LogLevel logLevel,
			EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter)
		{
			if (logLevel == LogLevel.Warning)
				Interlocked.Increment(ref _warningCount);
		}
	}

	private sealed class ThrowingDispatcher : ILoginServerInboundPacketDispatcher
	{
		private int _dispatchCount;

		public int DispatchCount => Volatile.Read(ref _dispatchCount);

		public void Dispatch(LoginServerInboundPacket packet)
		{
			Interlocked.Increment(ref _dispatchCount);
			throw new IOException("simulated packet-handler failure");
		}
	}

	private sealed class HardwareListRecordingDispatcher : ILoginServerInboundPacketDispatcher
	{
		private readonly int _expectedPackets;
		private readonly TaskCompletionSource _completed = NewSource();
		private int _packetCount;
		private int _macListCount;
		private int _hddListCount;

		public HardwareListRecordingDispatcher(int expectedPackets)
		{
			_expectedPackets = expectedPackets;
		}

		public int MacListCount => Volatile.Read(ref _macListCount);
		public int HddListCount => Volatile.Read(ref _hddListCount);

		public void Dispatch(LoginServerInboundPacket packet)
		{
			if (packet is MacBanListPacket)
				Interlocked.Increment(ref _macListCount);
			else if (packet is HddBanListPacket)
				Interlocked.Increment(ref _hddListCount);
			if (Interlocked.Increment(ref _packetCount) == _expectedPackets)
				_completed.TrySetResult();
		}

		public Task WaitForExpectedPacketsAsync() => _completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
	}

	private sealed class TwoSessionLoginServer : IAsyncDisposable
	{
		private readonly TcpListener _listener;
		private readonly TaskCompletionSource<byte[]> _firstAccountList = NewSource<byte[]>();
		private readonly TaskCompletionSource<byte[]> _firstAccountAuth = NewSource<byte[]>();
		private readonly TaskCompletionSource<byte[]> _secondAccountList = NewSource<byte[]>();
		private readonly TaskCompletionSource _dropFirst = NewSource();
		private readonly TaskCompletionSource _closeSecond = NewSource();
		private readonly Task _serverTask;
		private TcpClient? _activeClient;
		private int _acceptCount;

		private TwoSessionLoginServer(TcpListener listener)
		{
			_listener = listener;
			EndPoint = (IPEndPoint)listener.LocalEndpoint;
			_serverTask = Task.Run(RunAsync);
		}

		public IPEndPoint EndPoint { get; }
		public int AcceptCount => Volatile.Read(ref _acceptCount);

		public static Task<TwoSessionLoginServer> StartAsync()
		{
			var listener = new TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			return Task.FromResult(new TwoSessionLoginServer(listener));
		}

		public Task<byte[]> ReadFirstAccountListAsync() => _firstAccountList.Task.WaitAsync(TimeSpan.FromSeconds(5));
		public Task<byte[]> ReadFirstAccountAuthAsync() => _firstAccountAuth.Task.WaitAsync(TimeSpan.FromSeconds(5));
		public Task<byte[]> ReadSecondAccountListAsync() => _secondAccountList.Task.WaitAsync(TimeSpan.FromSeconds(5));
		public void DropFirstSession() => _dropFirst.TrySetResult();

		private async Task RunAsync()
		{
			for (var sessionNumber = 1; sessionNumber <= 2; sessionNumber++)
			{
				_activeClient = await _listener.AcceptTcpClientAsync();
				Interlocked.Increment(ref _acceptCount);
				await using var stream = _activeClient.GetStream();
				await ReadFrameAsync(stream); // SM_GS_AUTH
				await stream.WriteAsync(LoginAuthResponse());
				await stream.FlushAsync();
				var accountList = await ReadFrameAsync(stream);
				await stream.WriteAsync(ServerPacketFrameCodec.CreateFrame(new byte[] { 0x09, 0, 0, 0, 0 }));
				await stream.WriteAsync(ServerPacketFrameCodec.CreateFrame(new byte[] { 0x0A, 0, 0, 0, 0 }));
				await stream.FlushAsync();

				if (sessionNumber == 1)
				{
					_firstAccountList.TrySetResult(accountList);
					_firstAccountAuth.TrySetResult(await ReadFrameAsync(stream));
					await _dropFirst.Task;
					_activeClient.Close();
				}
				else
				{
					_secondAccountList.TrySetResult(accountList);
					await _closeSecond.Task;
				}
			}
		}

		public async ValueTask DisposeAsync()
		{
			_dropFirst.TrySetResult();
			_closeSecond.TrySetResult();
			_listener.Stop();
			_activeClient?.Dispose();
			try { await _serverTask.WaitAsync(TimeSpan.FromSeconds(2)); } catch { }
		}
	}

	private sealed class TwoSessionChatServer : IAsyncDisposable
	{
		private readonly TcpListener _listener;
		private readonly TaskCompletionSource _dropFirst = NewSource();
		private readonly TaskCompletionSource _secondAuth = NewSource();
		private readonly TaskCompletionSource _authenticateSecond = NewSource();
		private readonly TaskCompletionSource _closeSecond = NewSource();
		private readonly Task _serverTask;
		private TcpClient? _activeClient;
		private int _acceptCount;

		private TwoSessionChatServer(TcpListener listener)
		{
			_listener = listener;
			EndPoint = (IPEndPoint)listener.LocalEndpoint;
			_serverTask = Task.Run(RunAsync);
		}

		public IPEndPoint EndPoint { get; }
		public int AcceptCount => Volatile.Read(ref _acceptCount);

		public static Task<TwoSessionChatServer> StartAsync()
		{
			var listener = new TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			return Task.FromResult(new TwoSessionChatServer(listener));
		}

		public void DropFirstSession() => _dropFirst.TrySetResult();
		public Task WaitForSecondAuthAsync() => _secondAuth.Task.WaitAsync(TimeSpan.FromSeconds(5));
		public void AuthenticateSecondSession() => _authenticateSecond.TrySetResult();

		private async Task RunAsync()
		{
			for (var sessionNumber = 1; sessionNumber <= 2; sessionNumber++)
			{
				_activeClient = await _listener.AcceptTcpClientAsync();
				Interlocked.Increment(ref _acceptCount);
				await using var stream = _activeClient.GetStream();
				await ReadFrameAsync(stream); // SM_CS_AUTH
				if (sessionNumber == 1)
				{
					await stream.WriteAsync(ChatAuthResponse(10241));
					await stream.FlushAsync();
					await _dropFirst.Task;
					_activeClient.Close();
				}
				else
				{
					_secondAuth.TrySetResult();
					await _authenticateSecond.Task;
					await stream.WriteAsync(ChatAuthResponse(10242));
					await stream.FlushAsync();
					await _closeSecond.Task;
				}
			}
		}

		public async ValueTask DisposeAsync()
		{
			_dropFirst.TrySetResult();
			_authenticateSecond.TrySetResult();
			_closeSecond.TrySetResult();
			_listener.Stop();
			_activeClient?.Dispose();
			try { await _serverTask.WaitAsync(TimeSpan.FromSeconds(2)); } catch { }
		}
	}

	private sealed class HoldingLoginServer : IAsyncDisposable
	{
		private readonly TcpListener _listener;
		private readonly TaskCompletionSource<byte[]> _accountList = NewSource<byte[]>();
		private readonly TaskCompletionSource _close = NewSource();
		private readonly Task _serverTask;
		private TcpClient? _client;

		private HoldingLoginServer(TcpListener listener)
		{
			_listener = listener;
			_serverTask = Task.Run(RunAsync);
		}

		public static Task<HoldingLoginServer> StartAsync(IPEndPoint endpoint)
		{
			var listener = new TcpListener(endpoint);
			listener.Start();
			return Task.FromResult(new HoldingLoginServer(listener));
		}

		public Task<byte[]> ReadAccountListAsync() => _accountList.Task.WaitAsync(TimeSpan.FromSeconds(5));

		private async Task RunAsync()
		{
			_client = await _listener.AcceptTcpClientAsync();
			await using var stream = _client.GetStream();
			await ReadFrameAsync(stream);
			await stream.WriteAsync(LoginAuthResponse());
			await stream.FlushAsync();
			_accountList.TrySetResult(await ReadFrameAsync(stream));
			await _close.Task;
		}

		public async ValueTask DisposeAsync()
		{
			_close.TrySetResult();
			_listener.Stop();
			_client?.Dispose();
			try { await _serverTask.WaitAsync(TimeSpan.FromSeconds(2)); } catch { }
		}
	}

	private sealed class PacketIsolationLoginServer : IAsyncDisposable
	{
		private readonly TcpListener _listener;
		private readonly TaskCompletionSource<byte[]> _pong = NewSource<byte[]>();
		private readonly TaskCompletionSource _close = NewSource();
		private readonly Task _serverTask;
		private TcpClient? _client;

		private PacketIsolationLoginServer(TcpListener listener)
		{
			_listener = listener;
			EndPoint = (IPEndPoint)listener.LocalEndpoint;
			_serverTask = Task.Run(RunAsync);
		}

		public IPEndPoint EndPoint { get; }

		public static Task<PacketIsolationLoginServer> StartAsync()
		{
			var listener = new TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			return Task.FromResult(new PacketIsolationLoginServer(listener));
		}

		public Task<byte[]> ReadPongAsync() => _pong.Task.WaitAsync(TimeSpan.FromSeconds(5));

		private async Task RunAsync()
		{
			_client = await _listener.AcceptTcpClientAsync();
			await using var stream = _client.GetStream();
			await ReadFrameAsync(stream);
			await stream.WriteAsync(LoginAuthResponse());
			await stream.FlushAsync();
			await ReadFrameAsync(stream); // SM_ACCOUNT_LIST

			using var kick = new PacketBuffer();
			kick.WriteC(0x02);
			kick.WriteD(123);
			kick.WriteC(0);
			await stream.WriteAsync(ServerPacketFrameCodec.CreateFrame(kick.ToArray()));
			await stream.WriteAsync(ServerPacketFrameCodec.CreateFrame(new byte[] { 0x0B }));
			await stream.FlushAsync();
			_pong.TrySetResult(await ReadFrameAsync(stream));
			await _close.Task;
		}

		public async ValueTask DisposeAsync()
		{
			_close.TrySetResult();
			_listener.Stop();
			_client?.Dispose();
			try { await _serverTask.WaitAsync(TimeSpan.FromSeconds(2)); } catch { }
		}
	}

	private sealed class GatedLoginServer : IAsyncDisposable
	{
		private readonly TcpListener _listener;
		private readonly TaskCompletionSource _authRequest = NewSource();
		private readonly TaskCompletionSource _allowAuthentication = NewSource();
		private readonly TaskCompletionSource<byte[]> _accountList = NewSource<byte[]>();
		private readonly TaskCompletionSource _close = NewSource();
		private readonly Task _serverTask;
		private TcpClient? _client;

		private GatedLoginServer(TcpListener listener)
		{
			_listener = listener;
			EndPoint = (IPEndPoint)listener.LocalEndpoint;
			_serverTask = Task.Run(RunAsync);
		}

		public IPEndPoint EndPoint { get; }

		public static Task<GatedLoginServer> StartAsync()
		{
			var listener = new TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			return Task.FromResult(new GatedLoginServer(listener));
		}

		public Task WaitForAuthRequestAsync() => _authRequest.Task.WaitAsync(TimeSpan.FromSeconds(5));
		public void AllowAuthentication() => _allowAuthentication.TrySetResult();
		public Task<byte[]> ReadAccountListAsync() => _accountList.Task.WaitAsync(TimeSpan.FromSeconds(5));

		private async Task RunAsync()
		{
			_client = await _listener.AcceptTcpClientAsync();
			await using var stream = _client.GetStream();
			await ReadFrameAsync(stream);
			_authRequest.TrySetResult();
			await _allowAuthentication.Task;
			await stream.WriteAsync(LoginAuthResponse());
			await stream.FlushAsync();
			_accountList.TrySetResult(await ReadFrameAsync(stream));
			await _close.Task;
		}

		public async ValueTask DisposeAsync()
		{
			_allowAuthentication.TrySetResult();
			_close.TrySetResult();
			_listener.Stop();
			_client?.Dispose();
			try { await _serverTask.WaitAsync(TimeSpan.FromSeconds(2)); } catch { }
		}
	}

	private static TaskCompletionSource NewSource() =>
		new(TaskCreationOptions.RunContinuationsAsynchronously);

	private static TaskCompletionSource<T> NewSource<T>() =>
		new(TaskCreationOptions.RunContinuationsAsynchronously);
}
