using System.IO.Compression;
using System.Text;
using Aion.Commons.Network;
using Aion.GameServer.Data;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class CmHouseScriptTests
{
	[Fact]
	public void TryCreatePacket_RegistersJavaHouseScriptOpcodeAsInGameOnly()
	{
		Assert.IsType<CmHouseScript>(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(30, buffer =>
				{
					buffer.WriteD(12345);
					buffer.WriteC(7);
					buffer.WriteH(0);
				}),
				GameConnectionState.InGame));

		Assert.Null(GameClientPacketFactory.TryCreatePacket(
			CreateClientPayload(30, buffer =>
			{
				buffer.WriteD(12345);
				buffer.WriteC(7);
				buffer.WriteH(0);
			}),
			GameConnectionState.Authed));
	}

	[Fact]
	public void ReadFrom_ValidCompressedScriptReadsPayloadLikeJava()
	{
		var packet = CreatePacket();
		using var writeBuffer = new PacketBuffer();
		writeBuffer.WriteD(12345);
		writeBuffer.WriteC(255);
		writeBuffer.WriteH(11);
		writeBuffer.WriteD(3);
		writeBuffer.WriteD(9);
		writeBuffer.WriteB([0x01, 0x02, 0x03]);

		var readBuffer = new PacketBuffer(writeBuffer.ToArray());
		packet.ReadFrom(readBuffer);

		Assert.Equal(12345, packet.Address);
		Assert.Equal(255, packet.ScriptId);
		Assert.Equal(11, packet.TotalSize);
		Assert.Equal(3, packet.CompressedSize);
		Assert.Equal(9, packet.UncompressedSize);
		Assert.Equal([0x01, 0x02, 0x03], packet.ScriptContent);
		Assert.Equal(0, readBuffer.Remaining);
	}

	[Fact]
	public void ReadFrom_OversizedCompressedScriptStopsBeforeUncompressedSizeLikeJava()
	{
		var packet = CreatePacket();
		using var writeBuffer = new PacketBuffer();
		writeBuffer.WriteD(12345);
		writeBuffer.WriteC(7);
		writeBuffer.WriteH(11);
		writeBuffer.WriteD(CmHouseScript.MaxCompressedScriptSize + 1);
		writeBuffer.WriteD(9);
		writeBuffer.WriteB([0x01, 0x02, 0x03]);

		var readBuffer = new PacketBuffer(writeBuffer.ToArray());
		packet.ReadFrom(readBuffer);

		Assert.Equal(CmHouseScript.MaxCompressedScriptSize + 1, packet.CompressedSize);
		Assert.Equal(0, packet.UncompressedSize);
		Assert.Empty(packet.ScriptContent);
		Assert.Equal(7, readBuffer.Remaining);
	}

	[Fact]
	public async Task ProcessPacketAsync_OversizedScriptSendsJavaOverflowSystemMessage()
	{
		await using var fixture = await GameServerConnectionBuyItemTests.BuyItemFixture.CreateAsync();
		GameServerConnectionBuyItemTests.SetActivePlayerForPacketDispatchForAdapterTests(
			fixture.Connection,
			new Player
			{
				ObjectId = 1001,
				Name = "ScriptTester",
				Race = "ELYOS",
				PlayerClass = "RANGER",
				Position = new WorldPosition(210010000, 0, 0, 0, 0),
			});

		await GameServerConnectionBuyItemTests.InvokeProcessPacketAsyncForAdapterTests(
			fixture.Connection,
			CreateClientPayload(30, buffer =>
			{
				buffer.WriteD(700001);
				buffer.WriteC(7);
				buffer.WriteH(11);
				buffer.WriteD(CmHouseScript.MaxCompressedScriptSize + 1);
				buffer.WriteD(9);
				buffer.WriteB([0x01, 0x02, 0x03]);
			}));

		Assert.Equal(1401399, Assert.IsType<SmSystemMessage>(Assert.Single(fixture.SentPackets)).MessageId);
	}

	[Fact]
	public async Task ProcessPacketAsync_ValidOwnerScriptPersistsStateAndBroadcastsHouseScript()
	{
		var repository = new CapturingHousingRepository();
		await using var fixture = await GameServerConnectionBuyItemTests.BuyItemFixture.CreateAsync(housingRepository: repository);
		var player = CreatePlayerWithActiveHouse();
		GameServerConnectionBuyItemTests.SetActivePlayerForPacketDispatchForAdapterTests(fixture.Connection, player);
		var scriptXml = "<?xml version=\"1.0\" encoding=\"UTF-16\" ?><lboxes><lbox><id>7</id></lbox></lboxes>";
		var scriptBytes = Encoding.Unicode.GetBytes(scriptXml);
		var compressedBytes = CompressLikeJava(scriptBytes);

		await GameServerConnectionBuyItemTests.InvokeProcessPacketAsyncForAdapterTests(
			fixture.Connection,
			CreateClientPayload(30, buffer =>
			{
				buffer.WriteD(700001);
				buffer.WriteC(2);
				buffer.WriteH(11);
				buffer.WriteD(compressedBytes.Length);
				buffer.WriteD(scriptBytes.Length);
				buffer.WriteB(compressedBytes);
			}));

		var stored = Assert.Single(repository.StoredScripts);
		Assert.Equal((5001, 2, scriptXml), stored);
		var playerScript = player.Houses[0].Scripts.Get(2);
		Assert.NotNull(playerScript);
		Assert.Equal(compressedBytes, playerScript.CompressedBytes);
		Assert.Equal(scriptBytes.Length, playerScript.UncompressedSize);
		var broadcast = Assert.Single(fixture.Registry.VisibleBroadcasts);
		Assert.False(broadcast.IncludeSourcePlayer);
		var packet = Assert.IsType<SmHouseScripts>(broadcast.Packet);
		AssertHouseScriptsPacket(packet, 700001, 2, compressedBytes, scriptBytes.Length);
	}

	[Fact]
	public async Task ProcessPacketAsync_RemoveScriptDeletesStateAndBroadcastsRemoval()
	{
		var repository = new CapturingHousingRepository();
		await using var fixture = await GameServerConnectionBuyItemTests.BuyItemFixture.CreateAsync(housingRepository: repository);
		var player = CreatePlayerWithActiveHouse();
		var scriptBytes = Encoding.Unicode.GetBytes("<lboxes />");
		var compressedBytes = CompressLikeJava(scriptBytes);
		Assert.True(player.Houses[0].Scripts.Set(2, compressedBytes, scriptBytes.Length));
		GameServerConnectionBuyItemTests.SetActivePlayerForPacketDispatchForAdapterTests(fixture.Connection, player);

		await GameServerConnectionBuyItemTests.InvokeProcessPacketAsyncForAdapterTests(
			fixture.Connection,
			CreateClientPayload(30, buffer =>
			{
				buffer.WriteD(700001);
				buffer.WriteC(2);
				buffer.WriteH(0);
			}));

		Assert.Equal((5001, 2), Assert.Single(repository.DeletedScripts));
		var removedScript = player.Houses[0].Scripts.Get(2);
		Assert.NotNull(removedScript);
		Assert.False(removedScript.HasData);
		var packet = Assert.IsType<SmHouseScripts>(Assert.Single(fixture.Registry.VisibleBroadcasts).Packet);
		AssertHouseScriptRemovalPacket(packet, 700001, 2);
	}

	[Fact]
	public async Task ProcessPacketAsync_NonOwnerScriptMutationDoesNotPersistOrBroadcast()
	{
		var repository = new CapturingHousingRepository();
		await using var fixture = await GameServerConnectionBuyItemTests.BuyItemFixture.CreateAsync(housingRepository: repository);
		var player = CreatePlayerWithActiveHouse();
		GameServerConnectionBuyItemTests.SetActivePlayerForPacketDispatchForAdapterTests(fixture.Connection, player);
		var scriptBytes = Encoding.Unicode.GetBytes("<lboxes />");
		var compressedBytes = CompressLikeJava(scriptBytes);

		await GameServerConnectionBuyItemTests.InvokeProcessPacketAsyncForAdapterTests(
			fixture.Connection,
			CreateClientPayload(30, buffer =>
			{
				buffer.WriteD(123456);
				buffer.WriteC(2);
				buffer.WriteH(11);
				buffer.WriteD(compressedBytes.Length);
				buffer.WriteD(scriptBytes.Length);
				buffer.WriteB(compressedBytes);
			}));

		Assert.Empty(repository.StoredScripts);
		Assert.Empty(repository.DeletedScripts);
		Assert.Empty(fixture.Registry.VisibleBroadcasts);
	}

	private static CmHouseScript CreatePacket()
	{
		return new CmHouseScript(30, new HashSet<GameConnectionState> { GameConnectionState.InGame });
	}

	private static Player CreatePlayerWithActiveHouse()
	{
		return new Player
		{
			ObjectId = 1001,
			Name = "ScriptTester",
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
			Houses =
			[
				new PlayerHouse(
					ObjectId: 5001,
					AddressId: 700001,
					BuildingId: 353000,
					AcquiredTime: DateTime.UtcNow,
					NextPay: null,
					IsInactive: false),
			],
		};
	}

	private static byte[] CompressLikeJava(byte[] bytes)
	{
		using var target = new MemoryStream();
		using (var deflater = new ZLibStream(target, CompressionLevel.Optimal, leaveOpen: true))
			deflater.Write(bytes);
		return target.ToArray();
	}

	private static void AssertHouseScriptsPacket(
		SmHouseScripts packet,
		int expectedAddress,
		int expectedScriptId,
		byte[] expectedCompressedBytes,
		int expectedUncompressedSize)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(expectedAddress, reader.ReadD());
		Assert.Equal(1, reader.ReadH());
		Assert.Equal(expectedScriptId, reader.ReadC());
		Assert.Equal(8 + expectedCompressedBytes.Length + 8, reader.ReadH());
		Assert.Equal(expectedCompressedBytes.Length + 8, reader.ReadD());
		Assert.Equal(expectedUncompressedSize, reader.ReadD());
		Assert.Equal(expectedCompressedBytes, reader.ReadB(expectedCompressedBytes.Length));
		Assert.Equal(Enumerable.Repeat((byte)0xCD, 8).ToArray(), reader.ReadB(8));
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertHouseScriptRemovalPacket(SmHouseScripts packet, int expectedAddress, int expectedScriptId)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(expectedAddress, reader.ReadD());
		Assert.Equal(1, reader.ReadH());
		Assert.Equal(expectedScriptId, reader.ReadC());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.Remaining);
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}

	private static byte[] CreateClientPayload(int opcode, Action<PacketBuffer> writeBody)
	{
		using var body = new PacketBuffer();
		writeBody(body);
		var bodyBytes = body.ToArray();

		var encodedOpcode = EncodeClientPacketOpcode(opcode);
		using var payload = new PacketBuffer(5 + bodyBytes.Length);
		payload.WriteH(encodedOpcode);
		payload.WriteC(0x65);
		payload.WriteH((ushort)~encodedOpcode);
		payload.WriteB(bodyBytes);
		return payload.ToArray();
	}

	private static int EncodeClientPacketOpcode(int opcode)
	{
		return ((((opcode + 207) ^ 0xEF) + 0x0C) ^ 0xEF) & 0xffff;
	}

	private sealed class CapturingHousingRepository : IHousingRepository
	{
		public List<(int HouseObjectId, int ScriptId, string ScriptXml)> StoredScripts { get; } = [];

		public List<(int HouseObjectId, int ScriptId)> DeletedScripts { get; } = [];

		public Task<IReadOnlyList<WorldHouse>> LoadWorldHousesAsync(
			HousingTemplateTable housingTemplates,
			CancellationToken cancellationToken = default) =>
			Task.FromResult<IReadOnlyList<WorldHouse>>(Array.Empty<WorldHouse>());

		public Task<IReadOnlyList<WorldHouse>> LoadWorldStudiosAsync(
			HousingTemplateTable housingTemplates,
			CancellationToken cancellationToken = default) =>
			Task.FromResult<IReadOnlyList<WorldHouse>>(Array.Empty<WorldHouse>());

		public Task<HouseRegistrySummary> LoadHouseRegistryAsync(
			int playerObjectId,
			int buildingId,
			HousingTemplateTable housingTemplates,
			HousingObjectTemplateTable housingObjectTemplates,
			CancellationToken cancellationToken = default) =>
			Task.FromResult(HouseRegistrySummary.Empty);

		public Task<bool> SaveHouseObjectPlacementAsync(
			int playerObjectId,
			RegisteredHouseObjectSummary houseObject,
			CancellationToken cancellationToken = default) =>
			Task.FromResult(true);

		public Task<bool> RegisterHouseObjectFromInventoryAsync(
			int playerObjectId,
			int sourceItemObjectId,
			RegisteredHouseObjectSummary houseObject,
			int? expireTimeSeconds,
			CancellationToken cancellationToken = default) =>
			Task.FromResult(true);

		public Task<bool> RegisterHouseDecorationFromInventoryAsync(
			int playerObjectId,
			int sourceItemObjectId,
			RegisteredHouseDecorationSummary decoration,
			CancellationToken cancellationToken = default) =>
			Task.FromResult(true);

		public Task<bool> SaveHouseDecorationMutationAsync(
			int playerObjectId,
			IReadOnlyList<RegisteredHouseDecorationSummary> updatedDecorations,
			IReadOnlyList<int> deletedDecorationObjectIds,
			CancellationToken cancellationToken = default) =>
			Task.FromResult(true);

		public Task<bool> SaveHouseRenovationAsync(
			int playerObjectId,
			int houseObjectId,
			int buildingId,
			IReadOnlyList<InventoryItem> updatedCouponItems,
			IReadOnlyList<int> deletedCouponItemObjectIds,
			CancellationToken cancellationToken = default) =>
			Task.FromResult(true);

		public Task<bool> SaveHouseObjectUseAsync(
			int houseOwnerObjectId,
			int usingPlayerObjectId,
			RegisteredHouseObjectSummary? updatedHouseObject,
			int? deletedHouseObjectId,
			IReadOnlyList<InventoryItem> updatedConsumedItems,
			IReadOnlyList<int> deletedConsumedObjectIds,
			IReadOnlyList<InventoryItem> updatedRewardItems,
			IReadOnlyList<InventoryItem> addedRewardItems,
			CancellationToken cancellationToken = default) =>
			Task.FromResult(true);

		public Task<bool> DeleteHouseRegisteredObjectAsync(
			int playerObjectId,
			int itemObjectId,
			CancellationToken cancellationToken = default) =>
			Task.FromResult(true);

		public Task<bool> StoreHouseScriptAsync(
			int houseObjectId,
			int scriptId,
			string scriptXml,
			CancellationToken cancellationToken = default)
		{
			StoredScripts.Add((houseObjectId, scriptId, scriptXml));
			return Task.FromResult(true);
		}

		public Task<bool> DeleteHouseScriptAsync(
			int houseObjectId,
			int scriptId,
			CancellationToken cancellationToken = default)
		{
			DeletedScripts.Add((houseObjectId, scriptId));
			return Task.FromResult(true);
		}
	}
}
