using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Network;
using Aion.GameServer.Network.LoginServer;
using Aion.GameServer.Network.LoginServer.ServerPackets;
using Microsoft.Extensions.Logging.Abstractions;
using GameLoginServer = Aion.GameServer.Network.LoginServer.LoginServer;

namespace Aion.GameServer.Tests;

public sealed class LoginServerInboundProtocolTests
{
	[Fact]
	public void AccountListPacket_MatchesJavaWireFrame()
	{
		var packet = new SmAccountList(new[] { 0x11223344, 9 });

		Assert.Equal(
			Convert.FromHexString("0F0004020000004433221109000000"),
			packet.SerializeFrame());
	}

	[Fact]
	public void Factory_MapsTheCompleteJavaOpcodeAndStateTable()
	{
		var packets = new Dictionary<byte, Type>
		{
			[0x00] = typeof(GameServerAuthResponsePacket),
			[0x01] = typeof(AccountAuthResponsePacket),
			[0x02] = typeof(KickAccountPacket),
			[0x03] = typeof(AccountReconnectKeyPacket),
			[0x04] = typeof(LoginServerControlResponsePacket),
			[0x05] = typeof(BanResponsePacket),
			[0x08] = typeof(CharacterCountRequestPacket),
			[0x09] = typeof(MacBanListPacket),
			[0x0A] = typeof(HddBanListPacket),
			[0x0B] = typeof(LoginServerPingPacket),
			[0x0C] = typeof(PlayerTransferOkPacket),
		};

		foreach (var (opcode, expectedType) in packets)
		{
			var validState = opcode == 0 ? LoginServerState.Connected : LoginServerState.Authed;
			using var validBuffer = new PacketBuffer(CreateMinimalPacket(opcode));
			Assert.True(LoginServerInboundPacketFactory.TryCreate(
				validBuffer, validState, out var packet, out var parsedOpcode));
			Assert.Equal(opcode, parsedOpcode);
			Assert.IsType(expectedType, packet);

			foreach (var invalidState in Enum.GetValues<LoginServerState>().Where(s => s != validState))
			{
				using var invalidBuffer = new PacketBuffer(CreateMinimalPacket(opcode));
				Assert.False(LoginServerInboundPacketFactory.TryCreate(
					invalidBuffer, invalidState, out var invalidPacket, out var invalidOpcode));
				Assert.Equal(opcode, invalidOpcode);
				Assert.Null(invalidPacket);
			}
		}

		using var unknownBuffer = new PacketBuffer(new byte[] { 0x7F });
		Assert.False(LoginServerInboundPacketFactory.TryCreate(
			unknownBuffer, LoginServerState.Authed, out var unknown, out var unknownOpcode));
		Assert.Equal(0x7F, unknownOpcode);
		Assert.Null(unknown);
	}

	[Fact]
	public void Factory_ParsesKickReconnectControlAndBanResponsesInJavaOrder()
	{
		var kick = Parse<KickAccountPacket>(Payload(buffer =>
		{
			buffer.WriteC(0x02);
			buffer.WriteD(101);
			buffer.WriteC(1);
		}));
		Assert.Equal(101, kick.AccountId);
		Assert.True(kick.NotifyDoubleLogin);

		var reconnect = Parse<AccountReconnectKeyPacket>(Payload(buffer =>
		{
			buffer.WriteC(0x03);
			buffer.WriteD(102);
			buffer.WriteD(unchecked((int)0x88776655));
		}));
		Assert.Equal(102, reconnect.AccountId);
		Assert.Equal(unchecked((int)0x88776655), reconnect.ReconnectKey);

		var control = Parse<LoginServerControlResponsePacket>(Payload(buffer =>
		{
			buffer.WriteC(0x04);
			buffer.WriteC(2);
			buffer.WriteC(10);
			buffer.WriteD(103);
			buffer.WriteD(7001);
			buffer.WriteC(1);
		}));
		Assert.Equal((byte)2, control.Type);
		Assert.Equal((byte)10, control.Param);
		Assert.Equal(103, control.AccountId);
		Assert.Equal(7001, control.AdminObjectId);
		Assert.True(control.Result);

		var ban = Parse<BanResponsePacket>(Payload(buffer =>
		{
			buffer.WriteC(0x05);
			buffer.WriteC(3);
			buffer.WriteD(104);
			buffer.WriteS("127.0.*.*");
			buffer.WriteD(60);
			buffer.WriteD(7002);
			buffer.WriteC(0);
		}));
		Assert.Equal((byte)3, ban.Type);
		Assert.Equal(104, ban.AccountId);
		Assert.Equal("127.0.*.*", ban.Ip);
		Assert.Equal(60, ban.Time);
		Assert.Equal(7002, ban.AdminObjectId);
		Assert.False(ban.Result);
	}

	[Fact]
	public void Factory_ParsesMacAndHddBanListsWithoutLosingRows()
	{
		var macList = Parse<MacBanListPacket>(Payload(buffer =>
		{
			buffer.WriteC(0x09);
			buffer.WriteD(2);
			buffer.WriteS("AA-BB-CC-DD-EE-01");
			buffer.WriteQ(1_700_000_000_001L);
			buffer.WriteS("first");
			buffer.WriteS("AA-BB-CC-DD-EE-02");
			buffer.WriteQ(1_700_000_000_002L);
			buffer.WriteS("second");
		}));
		Assert.Equal(
			new[]
			{
				new MacBanListEntry("AA-BB-CC-DD-EE-01", 1_700_000_000_001L, "first"),
				new MacBanListEntry("AA-BB-CC-DD-EE-02", 1_700_000_000_002L, "second"),
			},
			macList.Entries);

		var hddList = Parse<HddBanListPacket>(Payload(buffer =>
		{
			buffer.WriteC(0x0A);
			buffer.WriteD(2);
			buffer.WriteS("disk-1");
			buffer.WriteQ(1_800_000_000_001L);
			buffer.WriteS("disk-2");
			buffer.WriteQ(1_800_000_000_002L);
		}));
		Assert.Equal(
			new[]
			{
				new HddBanListEntry("disk-1", 1_800_000_000_001L),
				new HddBanListEntry("disk-2", 1_800_000_000_002L),
			},
			hddList.Entries);
	}

	[Theory]
	[InlineData(0x09)]
	[InlineData(0x0A)]
	public void Factory_HugeTruncatedBanListCountFailsWithoutPeerSizedAllocation(byte opcode)
	{
		using var buffer = new PacketBuffer(Payload(payload =>
		{
			payload.WriteC(opcode);
			payload.WriteD(int.MaxValue);
		}), strictReads: false);

		Assert.Throws<EndOfStreamException>(() => LoginServerInboundPacketFactory.TryCreate(
			buffer, LoginServerState.Authed, out _, out _));
	}

	[Fact]
	public void Factory_NegativeBanListCountsMatchJavaAsEmptyLists()
	{
		var macList = Parse<MacBanListPacket>(Payload(buffer =>
		{
			buffer.WriteC(0x09);
			buffer.WriteD(-1);
		}));
		var hddList = Parse<HddBanListPacket>(Payload(buffer =>
		{
			buffer.WriteC(0x0A);
			buffer.WriteD(-1);
		}));

		Assert.Empty(macList.Entries);
		Assert.Empty(hddList.Entries);
	}

	[Fact]
	public void Factory_ParsesEveryJavaPlayerTransferResponseAction()
	{
		var info = Parse<PlayerTransferInfoPacket>(TransferPayload(20, buffer =>
		{
			buffer.WriteD(501);
			buffer.WriteD(601);
			buffer.WriteS("Kahrun");
			buffer.WriteS("target-account");
			buffer.WriteD(3);
			buffer.WriteB(new byte[] { 1, 2, 3 });
		}));
		Assert.Equal(501, info.TargetAccountId);
		Assert.Equal(601, info.TaskId);
		Assert.Equal("Kahrun", info.Name);
		Assert.Equal("target-account", info.AccountName);
		Assert.Equal(new byte[] { 1, 2, 3 }, info.CommonData);

		var ok = Parse<PlayerTransferOkPacket>(TransferPayload(21, b => b.WriteD(602)));
		Assert.Equal(602, ok.TaskId);

		var error = Parse<PlayerTransferErrorPacket>(TransferPayload(22, buffer =>
		{
			buffer.WriteD(603);
			buffer.WriteS("failed");
		}));
		Assert.Equal(603, error.TaskId);
		Assert.Equal("failed", error.Reason);

		var perform = Parse<PlayerTransferPerformActionPacket>(TransferPayload(23, buffer =>
		{
			buffer.WriteC(1);
			buffer.WriteC(2);
			buffer.WriteD(701);
			buffer.WriteD(702);
			buffer.WriteD(703);
			buffer.WriteD(604);
		}));
		Assert.Equal((byte)1, perform.SourceServerId);
		Assert.Equal((byte)2, perform.TargetServerId);
		Assert.Equal(701, perform.SourceAccountId);
		Assert.Equal(702, perform.TargetAccountId);
		Assert.Equal(703, perform.PlayerId);
		Assert.Equal(604, perform.TaskId);

		foreach (var actionId in Enumerable.Range(24, 5))
		{
			var data = Parse<PlayerTransferDataPacket>(TransferPayload(actionId, buffer =>
			{
				buffer.WriteD(600 + actionId);
				buffer.WriteD(2);
				buffer.WriteB(new[] { (byte)actionId, (byte)(actionId + 1) });
			}));
			Assert.Equal(actionId, data.ActionId);
			Assert.Equal(600 + actionId, data.TaskId);
			Assert.Equal(new[] { (byte)actionId, (byte)(actionId + 1) }, data.Data);
		}
	}

	[Fact]
	public async Task Connector_SendsPostAuthAccountSnapshotBeforeDispatchingAllActiveResponses()
	{
		var dispatcher = new RecordingDispatcher(expectedCount: 7);
		await using var mockServer = await DispatchingLoginBridgeServer.StartAsync(CreateActiveResponseFrames());
		await using var connector = new GameLoginServer(
			NullLogger<GameLoginServer>.Instance,
			CreateOptions(mockServer.EndPoint),
			characterSelectionRepository: null,
			dispatcher);

		await connector.StartAsync();
		var accountListFrame = await mockServer.ReadAccountListFrameAsync();
		await dispatcher.WaitForExpectedPacketsAsync();

		Assert.Equal(Convert.FromHexString("07000400000000"), accountListFrame);
		Assert.Collection(
			dispatcher.Packets,
			packet => Assert.IsType<KickAccountPacket>(packet),
			packet => Assert.IsType<AccountReconnectKeyPacket>(packet),
			packet => Assert.IsType<LoginServerControlResponsePacket>(packet),
			packet => Assert.IsType<BanResponsePacket>(packet),
			packet => Assert.IsType<MacBanListPacket>(packet),
			packet => Assert.IsType<HddBanListPacket>(packet),
			packet => Assert.IsType<PlayerTransferErrorPacket>(packet));
	}

	private static T Parse<T>(byte[] payload, LoginServerState state = LoginServerState.Authed)
		where T : LoginServerInboundPacket
	{
		using var buffer = new PacketBuffer(payload);
		Assert.True(LoginServerInboundPacketFactory.TryCreate(buffer, state, out var packet, out _));
		Assert.Equal(0, buffer.Remaining);
		return Assert.IsType<T>(packet);
	}

	private static byte[] TransferPayload(int actionId, Action<PacketBuffer> writeAction)
	{
		return Payload(buffer =>
		{
			buffer.WriteC(0x0C);
			buffer.WriteD(actionId);
			writeAction(buffer);
		});
	}

	private static byte[] CreateMinimalPacket(byte opcode)
	{
		return opcode switch
		{
			0x00 => Payload(b => { b.WriteC(0x00); b.WriteC(0); b.WriteC(1); }),
			0x01 => Payload(b => { b.WriteC(0x01); b.WriteD(1); b.WriteC(0); }),
			0x02 => Payload(b => { b.WriteC(0x02); b.WriteD(1); b.WriteC(0); }),
			0x03 => Payload(b => { b.WriteC(0x03); b.WriteD(1); b.WriteD(2); }),
			0x04 => Payload(b => { b.WriteC(0x04); b.WriteC(1); b.WriteC(2); b.WriteD(3); b.WriteD(4); b.WriteC(1); }),
			0x05 => Payload(b => { b.WriteC(0x05); b.WriteC(1); b.WriteD(2); b.WriteS(""); b.WriteD(3); b.WriteD(4); b.WriteC(1); }),
			0x08 => Payload(b => { b.WriteC(0x08); b.WriteD(1); }),
			0x09 => Payload(b => { b.WriteC(0x09); b.WriteD(0); }),
			0x0A => Payload(b => { b.WriteC(0x0A); b.WriteD(0); }),
			0x0B => new byte[] { 0x0B },
			0x0C => TransferPayload(21, b => b.WriteD(1)),
			_ => throw new ArgumentOutOfRangeException(nameof(opcode)),
		};
	}

	private static IReadOnlyList<byte[]> CreateActiveResponseFrames()
	{
		return new[]
		{
			Frame(b => { b.WriteC(0x02); b.WriteD(101); b.WriteC(1); }),
			Frame(b => { b.WriteC(0x03); b.WriteD(102); b.WriteD(555); }),
			Frame(b => { b.WriteC(0x04); b.WriteC(1); b.WriteC(3); b.WriteD(103); b.WriteD(7001); b.WriteC(1); }),
			Frame(b => { b.WriteC(0x05); b.WriteC(2); b.WriteD(104); b.WriteS("127.*"); b.WriteD(60); b.WriteD(7002); b.WriteC(1); }),
			Frame(b => { b.WriteC(0x09); b.WriteD(1); b.WriteS("AA-BB-CC-DD-EE-FF"); b.WriteQ(1_900_000_000_000L); b.WriteS("qa"); }),
			Frame(b => { b.WriteC(0x0A); b.WriteD(1); b.WriteS("disk-qa"); b.WriteQ(1_900_000_000_001L); }),
			Frame(b => { b.WriteC(0x0C); b.WriteD(22); b.WriteD(605); b.WriteS("transfer failed"); }),
		};
	}

	private static byte[] Frame(Action<PacketBuffer> writePayload)
	{
		return ServerPacketFrameCodec.CreateFrame(Payload(writePayload));
	}

	private static byte[] Payload(Action<PacketBuffer> writePayload)
	{
		using var buffer = new PacketBuffer();
		writePayload(buffer);
		return buffer.ToArray();
	}

	private static GameServerOptions CreateOptions(IPEndPoint loginEndPoint)
	{
		return new GameServerOptions
		{
			Network = new GameServerNetworkOptions
			{
				LoginEndPoint = loginEndPoint,
				ChatEndPoint = new IPEndPoint(IPAddress.Loopback, 9021),
				ClientConnectEndPoint = new IPEndPoint(IPAddress.Loopback, 7777),
				GameServerId = 1,
				LoginPassword = "1234",
				MaxOnlinePlayers = 100,
			},
		};
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

	private sealed class RecordingDispatcher : ILoginServerInboundPacketDispatcher
	{
		private readonly int _expectedCount;
		private readonly ConcurrentQueue<LoginServerInboundPacket> _packets = new();
		private readonly TaskCompletionSource _completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private int _count;

		public RecordingDispatcher(int expectedCount)
		{
			_expectedCount = expectedCount;
		}

		public IReadOnlyList<LoginServerInboundPacket> Packets => _packets.ToArray();

		public void Dispatch(LoginServerInboundPacket packet)
		{
			_packets.Enqueue(packet);
			if (Interlocked.Increment(ref _count) == _expectedCount)
				_completed.TrySetResult();
		}

		public Task WaitForExpectedPacketsAsync()
		{
			return _completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
		}
	}

	private sealed class DispatchingLoginBridgeServer : IAsyncDisposable
	{
		private readonly TcpListener _listener;
		private readonly IReadOnlyList<byte[]> _responseFrames;
		private readonly TaskCompletionSource<byte[]> _accountListFrame = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private readonly TaskCompletionSource _closeClient = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private readonly Task _serverTask;
		private TcpClient? _client;

		private DispatchingLoginBridgeServer(TcpListener listener, IReadOnlyList<byte[]> responseFrames)
		{
			_listener = listener;
			_responseFrames = responseFrames;
			EndPoint = (IPEndPoint)listener.LocalEndpoint;
			_serverTask = Task.Run(RunAsync);
		}

		public IPEndPoint EndPoint { get; }

		public static Task<DispatchingLoginBridgeServer> StartAsync(IReadOnlyList<byte[]> responseFrames)
		{
			var listener = new TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			return Task.FromResult(new DispatchingLoginBridgeServer(listener, responseFrames));
		}

		public Task<byte[]> ReadAccountListFrameAsync()
		{
			return _accountListFrame.Task.WaitAsync(TimeSpan.FromSeconds(5));
		}

		private async Task RunAsync()
		{
			_client = await _listener.AcceptTcpClientAsync();
			await using var stream = _client.GetStream();
			await ReadFrameAsync(stream); // SM_GS_AUTH
			await stream.WriteAsync(Frame(b => { b.WriteC(0x00); b.WriteC(0); b.WriteC(1); }));
			await stream.FlushAsync();
			_accountListFrame.TrySetResult(await ReadFrameAsync(stream));

			foreach (var responseFrame in _responseFrames)
			{
				await stream.WriteAsync(responseFrame);
				await stream.FlushAsync();
			}

			await _closeClient.Task;
		}

		public async ValueTask DisposeAsync()
		{
			_closeClient.TrySetResult();
			_listener.Stop();
			_client?.Dispose();
			try
			{
				await _serverTask.WaitAsync(TimeSpan.FromSeconds(2));
			}
			catch
			{
			}
		}
	}
}
