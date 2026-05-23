using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PortalEntryValidationServiceTests
{
	[Fact]
	public void ValidateCooldown_AllowsWhenJavaCooldownCountIsBelowMax()
	{
		var now = DateTimeOffset.FromUnixTimeMilliseconds(100_000);
		var player = CreatePlayerWithCooldown(reuseTimeMillis: 200_000, entryCount: 1);
		var cooltimes = CreateCooltimes(maxCount: 2);

		var result = PortalEntryValidationService.ValidateCooldown(player, WorldId, cooltimes, now);

		Assert.True(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.Allowed, result.Status);
		Assert.Null(result.FailurePacket);
		Assert.Single(player.PortalCooldowns);
	}

	[Fact]
	public void ValidateCooldown_RejectsWithJavaSystemMessageWhenCountMeetsMax()
	{
		var now = DateTimeOffset.FromUnixTimeMilliseconds(100_000);
		var player = CreatePlayerWithCooldown(reuseTimeMillis: 200_000, entryCount: 2);
		var cooltimes = CreateCooltimes(maxCount: 2);

		var result = PortalEntryValidationService.ValidateCooldown(player, WorldId, cooltimes, now);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.CooldownLocked, result.Status);
		var packet = Assert.IsType<SmSystemMessage>(result.FailurePacket);
		Assert.Equal(1400043, packet.MessageId);
	}

	[Fact]
	public void ValidateCooldown_RemovesExpiredJavaCooldownAndAllowsEntry()
	{
		var now = DateTimeOffset.FromUnixTimeMilliseconds(100_000);
		var player = CreatePlayerWithCooldown(reuseTimeMillis: 99_999, entryCount: 2);
		var cooltimes = CreateCooltimes(maxCount: 2);

		var result = PortalEntryValidationService.ValidateCooldown(player, WorldId, cooltimes, now);

		Assert.True(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.Allowed, result.Status);
		Assert.Null(result.FailurePacket);
		Assert.Empty(player.PortalCooldowns);
	}

	private const int WorldId = 300030000;

	private static Player CreatePlayerWithCooldown(long reuseTimeMillis, int entryCount)
	{
		return new Player
		{
			PortalCooldowns = new Dictionary<int, PlayerPortalCooldown>
			{
				[WorldId] = new(WorldId, reuseTimeMillis, entryCount),
			},
		};
	}

	private static InstanceCooltimeTable CreateCooltimes(int maxCount)
	{
		return new InstanceCooltimeTable(
		[
			new InstanceCooltimeSummary(8, WorldId, "PC_ALL", maxCount),
		]);
	}
}
