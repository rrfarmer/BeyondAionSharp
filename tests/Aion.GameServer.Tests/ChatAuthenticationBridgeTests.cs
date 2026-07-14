using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Aion.Commons.Network;
using Aion.Commons.Nio;
using Aion.Commons.Nio.Channels;
using Aion.GameServer.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Configs.Administration;
using Aion.GameServer.Data;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Ban;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;
using GameChatServer = Aion.GameServer.Network.ChatServer.ChatServer;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Tests;

[CollectionDefinition("Chat authentication bridge", DisableParallelization = true)]
public sealed class ChatAuthenticationBridgeCollectionDefinition
{
}

[Collection("Chat authentication bridge")]
public sealed class ChatAuthenticationBridgeTests
{
	[Fact]
	public async Task CmChatAuth_ResponseUsesTaggedNicknameAndDeliversSmChatInit()
	{
		var originalNameTags = AdminConfig.NAME_TAGS;
		AdminConfig.NAME_TAGS = ["%s", "»Dev«\uE04A%s"];
		try
		{
			await using var bridge = await MockChatBridgeServer.StartAsync();
			await using var connector = new GameChatServer(
				NullLogger<GameChatServer>.Instance,
				CreateOptions(bridge.EndPoint));
			await connector.StartAsync();
			await bridge.ReadInitialAuthFrameAsync();
			await WaitUntilAsync(() => connector.IsAuthed);

			var world = new GameWorld(NullLogger<GameWorld>.Instance);
			GameWorld.RegisterInstance(world);
			var (player, client) = CreateOnlinePlayer(7001, "account-one", "Kahrun", accessLevel: 2);
			using (client)
			{
				world.StoreObject(player);
				RunCmChatAuth(client.Connection, player.ObjectId);

				var playerAuthFrame = await bridge.ReadClientFrameAsync();
				AssertPlayerAuthFrame(
					playerAuthFrame,
					player.ObjectId,
					"account-one",
					"»Dev«\uE04AKahrun",
					raceId: 0,
					accessLevel: 2);

				var token = Convert.FromHexString("DEADBEEF");
				await bridge.SendPlayerAuthResponseAsync(player.ObjectId, token);
				var init = await WaitForPacketAsync<SM_CHAT_INIT>(client);

				Assert.Equal(Convert.FromHexString("04000000DEADBEEF"), CaptureWriteImplPayload(init));
			}
		}
		finally
		{
			AdminConfig.NAME_TAGS = originalNameTags;
			GameWorld.RegisterInstance(new GameWorld(NullLogger<GameWorld>.Instance));
		}
	}

	[Fact]
	public async Task CmChatAuth_ResponseReappliesRemainingGagToChatServer()
	{
		var originalNameTags = AdminConfig.NAME_TAGS;
		AdminConfig.NAME_TAGS = [];
		const int playerId = 7002;
		var chatBans = GetChatBans();
		try
		{
			await using var bridge = await MockChatBridgeServer.StartAsync();
			await using var connector = new GameChatServer(
				NullLogger<GameChatServer>.Instance,
				CreateOptions(bridge.EndPoint));
			await connector.StartAsync();
			await bridge.ReadInitialAuthFrameAsync();
			await WaitUntilAsync(() => connector.IsAuthed);

			var world = new GameWorld(NullLogger<GameWorld>.Instance);
			GameWorld.RegisterInstance(world);
			var (player, client) = CreateOnlinePlayer(playerId, "account-two", "Tiamat", accessLevel: 0);
			using (client)
			{
				world.StoreObject(player);
				chatBans[playerId] = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeMilliseconds();
				player.GetController().AddTask(TaskId.GAG, new ScheduledTask(new Task(() => { })));

				RunCmChatAuth(client.Connection, player.ObjectId);
				await bridge.ReadClientFrameAsync(); // SM_CS_PLAYER_AUTH
				var token = Convert.FromHexString("010203");
				await bridge.SendPlayerAuthResponseAsync(player.ObjectId, token);

				var init = await WaitForPacketAsync<SM_CHAT_INIT>(client);
				Assert.Equal(Convert.FromHexString("03000000010203"), CaptureWriteImplPayload(init));

				using var gag = PayloadReader(await bridge.ReadClientFrameAsync());
				Assert.Equal(0x03, (int)gag.ReadC());
				Assert.Equal(playerId, gag.ReadD());
				Assert.Equal(300_000L, gag.ReadQ());
				Assert.Equal(0, gag.Remaining);
			}
		}
		finally
		{
			chatBans.TryRemove(playerId, out _);
			AdminConfig.NAME_TAGS = originalNameTags;
			GameWorld.RegisterInstance(new GameWorld(NullLogger<GameWorld>.Instance));
		}
	}

	private static GameServerOptions CreateOptions(IPEndPoint chatEndPoint)
	{
		return new GameServerOptions
		{
			Network = new GameServerNetworkOptions
			{
				ChatEndPoint = chatEndPoint,
				ChatPassword = "secret",
				LoginEndPoint = new IPEndPoint(IPAddress.Loopback, 9014),
				ClientConnectEndPoint = new IPEndPoint(IPAddress.Loopback, 7777),
				GameServerId = 1,
				LoginPassword = "1234",
				MaxOnlinePlayers = 100,
			},
		};
	}

	private static (Player Player, QueuedClientConnection Client) CreateOnlinePlayer(
		int objectId,
		string accountName,
		string playerName,
		sbyte accessLevel)
	{
		EnsureDataManagerBridge();
		var common = new PlayerCommonData(objectId);
		common.SetPlayerClass(PlayerClass.WARRIOR);
		common.SetRace(Race.ELYOS);
		common.SetGender(Gender.MALE);
		common.SetName(playerName);
		common.SetNote(string.Empty);
		var account = new Account(objectId + 1000);
		account.SetName(accountName);
		account.SetAccessLevel(accessLevel);
		var player = new Player(new PlayerAccountData(common, new PlayerAppearance()), account);
		player.SetPosition(new WorldPosition(210010000));
		return (player, new QueuedClientConnection(player, account));
	}

	private static void EnsureDataManagerBridge()
	{
		try
		{
			if (DataManager.PLAYER_EXPERIENCE_TABLE != null)
				return;
		}
		catch (InvalidOperationException)
		{
		}
		catch (NullReferenceException)
		{
		}

		var experience = new long[67];
		for (long level = 0; level < experience.Length; level++)
			experience[level] = 100L * level * level * level + 1000L * level;

		var staticData = (StaticData)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(StaticData));
		SetAutoProperty(staticData, nameof(StaticData.AbsoluteStatsDataDh), new AbsoluteStatsData());
		SetAutoProperty(staticData, nameof(StaticData.PlayerExperienceTable), new PlayerExperienceTable(experience));
		var constructor = typeof(DataManager).GetConstructor(
			BindingFlags.Instance | BindingFlags.NonPublic,
			binder: null,
			[typeof(StaticData)],
			modifiers: null)!;
		DataManager.RegisterInstance((DataManager)constructor.Invoke([staticData]));
	}

	private static void SetAutoProperty(object target, string propertyName, object value)
	{
		var field = target.GetType().GetField(
			$"<{propertyName}>k__BackingField",
			BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new MissingFieldException(target.GetType().FullName, propertyName);
		field.SetValue(target, value);
	}

	private static void RunCmChatAuth(AionConnection connection, int playerId)
	{
		var data = ByteBuffer.Allocate(10).Order(ByteOrder.LITTLE_ENDIAN);
		data.PutInt(playerId);
		data.Put(new byte[6]);
		data.Flip();
		var packet = new CM_CHAT_AUTH(0, new HashSet<AionConnection.State> { AionConnection.State.IN_GAME });
		packet.SetBuffer(data);
		packet.SetConnection(connection);
		Assert.True(packet.Read());
		packet.Run();
	}

	private static void AssertPlayerAuthFrame(
		byte[] frame,
		int playerId,
		string accountName,
		string nickname,
		int raceId,
		byte accessLevel)
	{
		using var reader = PayloadReader(frame);
		Assert.Equal(0x01, (int)reader.ReadC());
		Assert.Equal(playerId, reader.ReadD());
		Assert.Equal(accountName, reader.ReadS());
		Assert.Equal(nickname, reader.ReadS());
		Assert.Equal(raceId, reader.ReadD());
		Assert.Equal(accessLevel, reader.ReadC());
		Assert.Equal(0, reader.Remaining);
	}

	private static PacketBuffer PayloadReader(byte[] frame)
	{
		var length = BinaryPrimitives.ReadUInt16LittleEndian(frame);
		Assert.Equal(frame.Length, length);
		return new PacketBuffer(frame.AsSpan(2).ToArray());
	}

	private static async Task<T> WaitForPacketAsync<T>(QueuedClientConnection client) where T : AionServerPacket
	{
		T? result = null;
		await WaitUntilAsync(() => (result = client.FindPacket<T>()) != null);
		return result!;
	}

	private static async Task WaitUntilAsync(Func<bool> condition)
	{
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		while (!condition())
			await Task.Delay(20, timeout.Token);
	}

	private static byte[] CaptureWriteImplPayload(AionServerPacket packet)
	{
		var buffer = ByteBuffer.Allocate(64).Order(ByteOrder.LITTLE_ENDIAN);
		packet.SetBuf(buffer);
		var writeImpl = typeof(AionServerPacket).GetMethod(
			"WriteImpl",
			BindingFlags.Instance | BindingFlags.NonPublic,
			[typeof(AionConnection)])!;
		writeImpl.Invoke(packet, [null]);
		var payload = new byte[buffer.Position()];
		buffer.Flip();
		buffer.Get(payload);
		return payload;
	}

	private static ConcurrentDictionary<int, long> GetChatBans()
	{
		return (ConcurrentDictionary<int, long>)(typeof(ChatBanService).GetField(
			"chatBans",
			BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null)
			?? throw new MissingFieldException(nameof(ChatBanService), "chatBans"));
	}

	private sealed class QueuedClientConnection : IDisposable
	{
		private readonly Socket _socket;
		private readonly SocketChannel _channel;
		private readonly Selector _selector;
		private readonly Queue<AionServerPacket> _packets;
		private readonly object _guard;

		public QueuedClientConnection(Player player, Account account)
		{
			_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			_channel = SocketChannel.Open(_socket);
			_selector = new Selector();
			Connection = (AionConnection)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(AionConnection));
			_guard = new object();
			_packets = new Queue<AionServerPacket>();
			SetField(Connection, typeof(AConnection), "guard", _guard);
			SetField(Connection, typeof(AionConnection), "sendMsgQueue", _packets);
			SetField(Connection, typeof(AConnection), "key", _channel.Register(_selector, SelectionKey.OP_READ, Connection));
			Connection.SetAccount(account);
			Assert.True(Connection.SetActivePlayer(player));
			player.SetClientConnection(Connection);
		}

		public AionConnection Connection { get; }

		public T? FindPacket<T>() where T : AionServerPacket
		{
			lock (_guard)
				return _packets.OfType<T>().FirstOrDefault();
		}

		public void Dispose()
		{
			_channel.Close();
			_selector.Close();
			_socket.Dispose();
		}

		private static void SetField(object target, Type declaringType, string fieldName, object value)
		{
			var field = declaringType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
				?? throw new MissingFieldException(declaringType.FullName, fieldName);
			field.SetValue(target, value);
		}
	}

	private sealed class MockChatBridgeServer : IAsyncDisposable
	{
		private readonly TcpListener _listener;
		private readonly TaskCompletionSource<byte[]> _initialAuthFrame = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private readonly Task _acceptTask;
		private TcpClient? _client;
		private NetworkStream? _stream;

		private MockChatBridgeServer(TcpListener listener)
		{
			_listener = listener;
			EndPoint = (IPEndPoint)listener.LocalEndpoint;
			_acceptTask = Task.Run(AcceptAndAuthenticateAsync);
		}

		public IPEndPoint EndPoint { get; }

		public static Task<MockChatBridgeServer> StartAsync()
		{
			var listener = new TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			return Task.FromResult(new MockChatBridgeServer(listener));
		}

		public Task<byte[]> ReadInitialAuthFrameAsync() => _initialAuthFrame.Task.WaitAsync(TimeSpan.FromSeconds(5));

		public async Task<byte[]> ReadClientFrameAsync()
		{
			await _acceptTask.WaitAsync(TimeSpan.FromSeconds(5));
			return await ReadFrameAsync(_stream!);
		}

		public async Task SendPlayerAuthResponseAsync(int playerId, byte[] token)
		{
			await _acceptTask.WaitAsync(TimeSpan.FromSeconds(5));
			using var payload = new PacketBuffer();
			payload.WriteC(0x01);
			payload.WriteD(playerId);
			payload.WriteC(token.Length);
			payload.WriteB(token);
			await _stream!.WriteAsync(ServerPacketFrameCodec.CreateFrame(payload.ToArray()));
			await _stream.FlushAsync();
		}

		private async Task AcceptAndAuthenticateAsync()
		{
			_client = await _listener.AcceptTcpClientAsync();
			_stream = _client.GetStream();
			_initialAuthFrame.TrySetResult(await ReadFrameAsync(_stream));
			await _stream.WriteAsync(CreateAuthResponseFrame());
			await _stream.FlushAsync();
		}

		private static byte[] CreateAuthResponseFrame()
		{
			using var payload = new PacketBuffer();
			payload.WriteC(0x00);
			payload.WriteC(0x00);
			payload.WriteC(4);
			payload.WriteB(IPAddress.Loopback.GetAddressBytes());
			payload.WriteH(10241);
			return ServerPacketFrameCodec.CreateFrame(payload.ToArray());
		}

		public async ValueTask DisposeAsync()
		{
			_listener.Stop();
			_stream?.Dispose();
			_client?.Dispose();
			try
			{
				await _acceptTask.WaitAsync(TimeSpan.FromSeconds(2));
			}
			catch
			{
			}
		}
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
				throw new EndOfStreamException("Chat bridge closed before the expected frame was read.");
			offset += read;
		}
		return result;
	}
}
