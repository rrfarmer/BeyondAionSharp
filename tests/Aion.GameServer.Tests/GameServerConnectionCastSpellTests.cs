using System.Net;
using System.Net.Sockets;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public class GameServerConnectionCastSpellTests
{
	[Fact]
	public async Task HandleCastSpellAsync_DeadPlayerSendsCannotCastDeadPacket()
	{
		var sentPackets = new List<GameServerPacket>();
		await using var pair = await TestConnectionPair.CreateAsync(sentPackets);
		var player = CreatePlayer(currentHp: 0);

		var result = await pair.Connection.HandleCastSpellAsync(player, CreateCastSpell(100));

		Assert.Equal(PlayerCastSpellEarlyExitStatus.DeadPlayer, result.Status);
		var packet = Assert.Single(sentPackets);
		var message = Assert.IsType<SmSystemMessage>(packet);
		Assert.Equal(1300026, message.MessageId);
		AssertSystemMessageParameters(message, ChatUtil.L10n(1400059));
	}

	[Fact]
	public async Task HandleCastSpellAsync_PetOrderWithoutPetSendsPetRequiredPacket()
	{
		var sentPackets = new List<GameServerPacket>();
		var hooks = new GameServerCastSpellHandlerHooks
		{
			IsPetOrderSkill = (_, _) => true,
			HasPetSummon = _ => false,
		};
		await using var pair = await TestConnectionPair.CreateAsync(sentPackets, hooks);
		var player = CreatePlayer();

		var result = await pair.Connection.HandleCastSpellAsync(player, CreateCastSpell(200));

		Assert.Equal(PlayerCastSpellEarlyExitStatus.PetRequired, result.Status);
		var message = Assert.IsType<SmSystemMessage>(Assert.Single(sentPackets));
		Assert.Equal(1402918, message.MessageId);
	}

	[Fact]
	public async Task HandleCastSpellAsync_ZeroSpellIdClearsCastSkillAndSendsCancelPackets()
	{
		var events = new List<string>();
		var sentPackets = new List<GameServerPacket>();
		var hooks = new GameServerCastSpellHandlerHooks
		{
			IsPetOrderSkill = (_, _) => throw new InvalidOperationException("Pet order check should not run after zero spell id."),
			GetSkillTemplate = (_, _) => throw new InvalidOperationException("Template lookup should not run after zero spell id."),
			CancelCurrentSkill = (player, _) => events.Add($"cancel-current:{player.CastingSkillId}:{player.LastCastingSkillId}"),
		};
		await using var pair = await TestConnectionPair.CreateAsync(sentPackets, hooks);
		var player = CreatePlayer();
		player.SetCastingSkill(7001);

		var result = await pair.Connection.HandleCastSpellAsync(player, CreateCastSpell(0));

		Assert.Equal(PlayerCastSpellEarlyExitStatus.CancelCurrentSkill, result.Status);
		Assert.Equal(0, player.CastingSkillId);
		Assert.Equal(7001, player.LastCastingSkillId);
		Assert.Equal(["cancel-current:0:7001"], events);
		Assert.Collection(
			sentPackets,
			packet => AssertSkillCancelPayload(Assert.IsType<SmSkillCancel>(packet), player.ObjectId, 7001),
			packet =>
			{
				var message = Assert.IsType<SmSystemMessage>(packet);
				Assert.Equal(1300023, message.MessageId);
			});
	}

	[Fact]
	public async Task HandleCastSpellAsync_ZeroSpellIdWithItemSkillClearsCastingSkillWithoutCastCancelPackets()
	{
		var events = new List<string>();
		var sentPackets = new List<GameServerPacket>();
		var hooks = new GameServerCastSpellHandlerHooks
		{
			CancelCurrentSkill = (player, _) => events.Add($"cancel-current:{player.CastingSkillId}:{player.LastCastingSkillId}"),
		};
		await using var pair = await TestConnectionPair.CreateAsync(sentPackets, hooks);
		var player = CreatePlayer();
		player.SetCastingSkill(9001, PlayerCastingSkillMethod.Item);

		var result = await pair.Connection.HandleCastSpellAsync(player, CreateCastSpell(0));

		Assert.Equal(PlayerCastSpellEarlyExitStatus.CancelCurrentSkill, result.Status);
		Assert.Equal(0, player.CastingSkillId);
		Assert.Equal(9001, player.LastCastingSkillId);
		Assert.Equal(["cancel-current:0:9001"], events);
		Assert.Empty(sentPackets);
	}

	[Fact]
	public async Task HandleCastSpellAsync_ZeroSpellIdWithItemSkillMetadataSendsItemCancelPacketsAndRemovesCooldown()
	{
		var events = new List<string>();
		var sentPackets = new List<GameServerPacket>();
		var hooks = new GameServerCastSpellHandlerHooks
		{
			CancelCurrentSkill = (player, _) => events.Add($"cancel-current:{player.CastingSkillId}:{player.LastCastingSkillId}"),
		};
		await using var pair = await TestConnectionPair.CreateAsync(sentPackets, hooks);
		var player = CreatePlayer();
		player.AddItemCooldown(77, 5_000, new DateTimeOffset(2026, 5, 24, 12, 0, 0, TimeSpan.Zero));
		player.SetCastingSkill(
			9001,
			PlayerCastingSkillMethod.Item,
			itemObjectId: 6001,
			itemTemplateId: 166050001,
			firstTargetObjectId: 7001,
			itemCooldownDelayId: 77);

		var result = await pair.Connection.HandleCastSpellAsync(player, CreateCastSpell(0));

		Assert.Equal(PlayerCastSpellEarlyExitStatus.CancelCurrentSkill, result.Status);
		Assert.Equal(0, player.CastingSkillId);
		Assert.Equal(9001, player.LastCastingSkillId);
		Assert.False(player.ItemCooldowns.ContainsKey(77));
		Assert.Equal(["cancel-current:0:9001"], events);
		Assert.Collection(
			sentPackets,
			packet =>
			{
				var message = Assert.IsType<SmSystemMessage>(packet);
				Assert.Equal(1300427, message.MessageId);
			},
			packet => AssertItemUsageAnimationPayload(
				Assert.IsType<SmItemUsageAnimation>(packet),
				player.ObjectId,
				targetObjectId: 7001,
				itemObjectId: 6001,
				itemId: 166050001,
				time: 0,
				end: 3,
				unknown: 0,
				unknown1: 0,
				unknown2: 1,
				unknown3: 0));
	}

	[Fact]
	public async Task HandleCastSpellAsync_CooldownNotReadySendsNotReadyAfterCancelUseItemAndAudit()
	{
		var events = new List<string>();
		var sentPackets = new List<GameServerPacket>();
		var hooks = new GameServerCastSpellHandlerHooks
		{
			GetSkillTemplate = (_, skillId) => new PlayerCastSpellSkillTemplate(skillId),
			GetNextSkillUseMilliseconds = _ => 2_000,
			GetCurrentTimeMilliseconds = () => 1_500,
			GetLastSkillId = _ => 199,
			CancelUseItem = _ => events.Add("cancel-use-item"),
			AuditCooldown = (_, skillId, delta, lastSkillId) => events.Add($"audit:{skillId}:{delta}:{lastSkillId}"),
			UseSkill = (_, _, _) => events.Add("use-skill"),
		};
		await using var pair = await TestConnectionPair.CreateAsync(sentPackets, hooks);
		var player = CreatePlayer();
		player.UsingItemObjectId = 9001;

		var result = await pair.Connection.HandleCastSpellAsync(player, CreateCastSpell(200, receiveTimeMilliseconds: 1_000));

		Assert.Equal(PlayerCastSpellEarlyExitStatus.SkillNotReady, result.Status);
		Assert.Equal(["cancel-use-item", "audit:200:1000:199"], events);
		Assert.Equal(0, player.UsingItemObjectId);
		var message = Assert.IsType<SmSystemMessage>(Assert.Single(sentPackets));
		Assert.Equal(1300021, message.MessageId);
	}

	[Fact]
	public async Task HandleCastSpellAsync_MissingTemplateLeavesUsingItemUntouched()
	{
		var sentPackets = new List<GameServerPacket>();
		await using var pair = await TestConnectionPair.CreateAsync(sentPackets);
		var player = CreatePlayer();
		player.UsingItemObjectId = 9001;

		var result = await pair.Connection.HandleCastSpellAsync(player, CreateCastSpell(300));

		Assert.Equal(PlayerCastSpellEarlyExitStatus.MissingOrPassiveTemplate, result.Status);
		Assert.Equal(9001, player.UsingItemObjectId);
		Assert.Empty(sentPackets);
	}

	[Fact]
	public async Task HandleCastSpellAsync_ReadySkillStopsProtectionCancelsUseItemAndCallsUseSkillWithoutPackets()
	{
		var events = new List<string>();
		var sentPackets = new List<GameServerPacket>();
		var hooks = new GameServerCastSpellHandlerHooks
		{
			GetSkillTemplate = (_, skillId) => new PlayerCastSpellSkillTemplate(skillId),
			StopProtection = _ => events.Add("stop-protection"),
			CancelUseItem = _ => events.Add("cancel-use-item"),
			UseSkill = (_, template, packet) => events.Add($"use-skill:{template.SkillId}:{packet.HitTime}"),
		};
		await using var pair = await TestConnectionPair.CreateAsync(sentPackets, hooks);
		var player = CreatePlayer();
		player.SetVisualState(PlayerVisualStates.Blinking);
		player.UsingItemObjectId = 9001;

		var result = await pair.Connection.HandleCastSpellAsync(player, CreateCastSpell(300, hitTime: 777));

		Assert.Equal(PlayerCastSpellEarlyExitStatus.UseSkill, result.Status);
		Assert.Equal(["stop-protection", "cancel-use-item", "use-skill:300:777"], events);
		Assert.False(player.IsProtectionActive());
		Assert.Equal(0, player.UsingItemObjectId);
		Assert.Empty(sentPackets);
	}

	private static Player CreatePlayer(int currentHp = 100)
	{
		return new Player
		{
			ObjectId = 1,
			LifeStats = new PlayerLifeStats(currentHp, CurrentMp: 100, CurrentFp: 100),
		};
	}

	private static CmCastSpell CreateCastSpell(int spellId, int hitTime = 300, long receiveTimeMilliseconds = 0)
	{
		var packet = new CmCastSpell(33, new HashSet<GameConnectionState> { GameConnectionState.InGame }, receiveTimeMilliseconds);
		using var buffer = new PacketBuffer();
		buffer.WriteH(spellId);
		buffer.WriteC(1);
		buffer.WriteC(0);
		buffer.WriteD(7001);
		buffer.WriteH(hitTime);
		buffer.WriteD(0);
		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));
		return packet;
	}

	private static void AssertSystemMessageParameters(SmSystemMessage packet, params string[] expected)
	{
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);
		Assert.Equal(25, (int)reader.ReadC());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(packet.MessageId, reader.ReadD());
		Assert.Equal(expected.Length, (int)reader.ReadC());
		foreach (var parameter in expected)
			Assert.Equal(parameter, reader.ReadS());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertSkillCancelPayload(SmSkillCancel packet, int expectedCreatureObjectId, int expectedSkillId)
	{
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);
		Assert.Equal(expectedCreatureObjectId, reader.ReadD());
		Assert.Equal(expectedSkillId, reader.ReadH());
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertItemUsageAnimationPayload(
		SmItemUsageAnimation packet,
		int playerObjectId,
		int targetObjectId,
		int itemObjectId,
		int itemId,
		int time,
		int end,
		int unknown,
		int unknown1,
		int unknown2,
		int unknown3)
	{
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);
		Assert.Equal(playerObjectId, reader.ReadD());
		Assert.Equal(targetObjectId, reader.ReadD());
		Assert.Equal(itemObjectId, reader.ReadD());
		Assert.Equal(itemId, reader.ReadD());
		Assert.Equal(time, reader.ReadD());
		Assert.Equal(end, (int)reader.ReadC());
		Assert.Equal(unknown, (int)reader.ReadC());
		Assert.Equal(unknown1, (int)reader.ReadC());
		Assert.Equal(unknown2, (int)reader.ReadC());
		Assert.Equal(unknown3, reader.ReadD());
		Assert.Equal(0, reader.Remaining);
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}

	private sealed class TestConnectionPair : IAsyncDisposable
	{
		private readonly TcpClient _client;
		private readonly GameServerConnection _connection;

		private TestConnectionPair(TcpClient client, GameServerConnection connection)
		{
			_client = client;
			_connection = connection;
		}

		public GameServerConnection Connection => _connection;

		public static async Task<TestConnectionPair> CreateAsync(
			List<GameServerPacket> sentPackets,
			GameServerCastSpellHandlerHooks? hooks = null)
		{
			var listener = new TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			try
			{
				var endpoint = (IPEndPoint)listener.LocalEndpoint;
				var client = new TcpClient();
				var acceptTask = listener.AcceptTcpClientAsync();
				await client.ConnectAsync(endpoint.Address, endpoint.Port);
				var serverClient = await acceptTask;
				var crypt = new GameCrypt(() => 0x01020304);
				crypt.EnableKey();
				var connection = new GameServerConnection(
					NullLogger.Instance,
					serverClient,
					"cast-spell-test",
					new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
					options: new GameServerOptions(),
					sentPacketObserver: sentPackets.Add,
					castSpellHooks: hooks,
					crypt: crypt);
				return new TestConnectionPair(client, connection);
			}
			finally
			{
				listener.Stop();
			}
		}

		public async ValueTask DisposeAsync()
		{
			await _connection.DisposeAsync();
			_client.Dispose();
		}
	}
}
