using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests;

public sealed class PlayerStateTests
{
	[Fact]
	public void Player_AddsAndRemovesJavaItemCooldown()
	{
		var now = DateTimeOffset.FromUnixTimeMilliseconds(100_000);
		var player = new Player { ObjectId = 1, Name = "CooldownTester" };

		player.AddItemCooldown(delayId: 21, useDelayMillis: 5000, now);

		var cooldown = Assert.Single(player.ItemCooldowns);
		Assert.Equal(21, cooldown.Key);
		Assert.Equal(105_000, cooldown.Value.ReuseTimeMillis);
		Assert.Equal(5, cooldown.Value.UseDelaySeconds);

		player.RemoveItemCooldown(21);

		Assert.Empty(player.ItemCooldowns);
	}
}
