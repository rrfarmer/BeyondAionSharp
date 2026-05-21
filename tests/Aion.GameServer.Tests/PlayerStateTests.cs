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

	[Fact]
	public void Player_CreatureStateMatchesJavaBitAndExactMultibitSemantics()
	{
		var player = new Player();

		player.SetCreatureState(PlayerCreatureState.WalkMode, enabled: true);
		player.SetCreatureState(PlayerCreatureState.Powershard, enabled: true);

		Assert.True(player.IsInState(PlayerCreatureState.WalkMode));
		Assert.True(player.IsInState(PlayerCreatureState.Powershard));
		Assert.Equal(2, (int)PlayerCreatureState.Flying);
		Assert.Equal(8, (int)PlayerCreatureState.FloatingCorpse);
		Assert.Equal(64, (int)PlayerCreatureState.WalkMode);
		Assert.Equal(128, (int)PlayerCreatureState.Powershard);
		Assert.Equal(512, (int)PlayerCreatureState.Gliding);

		player.ReplaceCreatureState(PlayerCreatureState.Chair);

		Assert.True(player.IsInState(PlayerCreatureState.Chair));
		Assert.False(player.IsInState(PlayerCreatureState.PrivateShop));

		player.ReplaceCreatureState(PlayerCreatureState.PrivateShop);

		Assert.True(player.IsInState(PlayerCreatureState.PrivateShop));
		Assert.False(player.IsInState(PlayerCreatureState.Chair));
	}
}
