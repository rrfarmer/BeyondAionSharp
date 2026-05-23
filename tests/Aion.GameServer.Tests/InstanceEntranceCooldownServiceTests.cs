using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class InstanceEntranceCooldownServiceTests
{
	[Fact]
	public void ApplyEntranceCooldown_ComposesJavaPortalTransferCooldownPath()
	{
		var now = DateTimeOffset.FromUnixTimeMilliseconds(100_000);
		var player = new Player { AccountMembership = 10 };
		var cooltimes = new InstanceCooltimeTable(
		[
			new InstanceCooltimeSummary(8, 300030000, "PC_ALL", MaxCount: 5, CoolTimeType: "RELATIVE", EntCoolTime: 30),
		]);
		var options = new GameServerOptions
		{
			Membership = new GameServerMembershipOptions { InstancesCooldown = 10 },
			Instance = new GameServerInstanceOptions { CooldownRate = 2 },
		};

		var result = InstanceEntranceCooldownService.ApplyEntranceCooldown(
			player,
			300030000,
			reenter: false,
			cooltimes,
			options,
			now);

		Assert.True(result.Added);
		Assert.Equal(2, result.InstanceCooldownRate);
		Assert.Equal(now.AddMinutes(15).ToUnixTimeMilliseconds(), result.ReuseTimeMillis);
		var cooldown = Assert.Single(player.PortalCooldowns);
		Assert.Equal(300030000, cooldown.Key);
		Assert.Equal(result.ReuseTimeMillis, cooldown.Value.ReuseTimeMillis);
		Assert.Equal(1, cooldown.Value.EntryCount);
	}

	[Fact]
	public void ApplyEntranceCooldown_SkipsAddForReentryLikeJavaPortalTransfer()
	{
		var now = DateTimeOffset.FromUnixTimeMilliseconds(100_000);
		var player = new Player { AccountMembership = 10 };
		var cooltimes = new InstanceCooltimeTable(
		[
			new InstanceCooltimeSummary(8, 300030000, "PC_ALL", MaxCount: 5, CoolTimeType: "RELATIVE", EntCoolTime: 30),
		]);
		var options = new GameServerOptions
		{
			Membership = new GameServerMembershipOptions { InstancesCooldown = 10 },
			Instance = new GameServerInstanceOptions { CooldownRate = 2 },
		};

		var result = InstanceEntranceCooldownService.ApplyEntranceCooldown(
			player,
			300030000,
			reenter: true,
			cooltimes,
			options,
			now);

		Assert.False(result.Added);
		Assert.Equal(now.AddMinutes(15).ToUnixTimeMilliseconds(), result.ReuseTimeMillis);
		Assert.Empty(player.PortalCooldowns);
	}

	[Fact]
	public void ApplyEntranceCooldown_SkipsAddWhenJavaCalculationReturnsZero()
	{
		var now = DateTimeOffset.FromUnixTimeMilliseconds(100_000);
		var player = new Player { AccountMembership = 10 };
		var cooltimes = new InstanceCooltimeTable(
		[
			new InstanceCooltimeSummary(8, 300030000, "PC_ALL", MaxCount: 0, CoolTimeType: "RELATIVE", EntCoolTime: 30),
		]);

		var result = InstanceEntranceCooldownService.ApplyEntranceCooldown(
			player,
			300030000,
			reenter: false,
			cooltimes,
			new GameServerOptions(),
			now);

		Assert.False(result.Added);
		Assert.Equal(0, result.ReuseTimeMillis);
		Assert.Empty(player.PortalCooldowns);
	}

	[Fact]
	public void CreateEntryInfoPacket_WritesJavaSingleWorldInstanceUpdate()
	{
		var now = DateTimeOffset.FromUnixTimeMilliseconds(100_000);
		var player = new Player
		{
			ObjectId = 1001,
			Name = "Character",
			Race = "ELYOS",
			AccountMembership = 10,
		};
		var cooltimes = new InstanceCooltimeTable(
		[
			new InstanceCooltimeSummary(8, 300030000, "PC_ALL", MaxCount: 5, CoolTimeType: "RELATIVE", EntCoolTime: 30),
			new InstanceCooltimeSummary(9, 300040000, "ASMODIANS", MaxCount: 1, CoolTimeType: "RELATIVE", EntCoolTime: 30),
		]);
		var options = new GameServerOptions
		{
			Membership = new GameServerMembershipOptions { InstancesCooldown = 10 },
			Instance = new GameServerInstanceOptions { CooldownRate = 1 },
		};

		var result = InstanceEntranceCooldownService.ApplyEntranceCooldown(
			player,
			300030000,
			reenter: false,
			cooltimes,
			options,
			now);
		var packet = Assert.IsType<Aion.GameServer.Network.Aion.ServerPackets.SmInstanceInfo>(
			InstanceEntranceCooldownService.CreateEntryInfoPacket(result, player, cooltimes, () => now));

		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);
		Assert.Equal(2, (int)reader.ReadC());
		Assert.Equal(8, reader.ReadD());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(1, reader.ReadH());
		Assert.Equal(1001, reader.ReadD());
		Assert.Equal(1, reader.ReadH());
		Assert.Equal(8, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(1800, reader.ReadD());
		Assert.Equal(5, reader.ReadD());
		Assert.Equal(-1, reader.ReadD());
		Assert.Equal(1, (int)reader.ReadC());
		Assert.Equal("Character", reader.ReadS());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void CreateEntryInfoPacket_SkipsPacketWhenCooldownWasNotAdded()
	{
		var result = new InstanceEntranceCooldownResult(
			WorldId: 300030000,
			ReuseTimeMillis: 100_000,
			InstanceCooldownRate: 1,
			Added: false);

		var packet = InstanceEntranceCooldownService.CreateEntryInfoPacket(
			result,
			new Player(),
			new InstanceCooltimeTable([]));

		Assert.Null(packet);
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}
}
