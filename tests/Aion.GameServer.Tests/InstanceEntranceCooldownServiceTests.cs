using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
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
}
