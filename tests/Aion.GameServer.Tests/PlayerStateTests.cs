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

	[Fact]
	public void Player_AbnormalStateMatchesJavaBitAndCompoundSemantics()
	{
		var player = new Player
		{
			AbnormalState = PlayerAbnormalState.Root | PlayerAbnormalState.Fear | PlayerAbnormalState.Confuse,
		};

		Assert.Equal(16, (int)PlayerAbnormalState.Root);
		Assert.Equal(512, (int)PlayerAbnormalState.Fear);
		Assert.Equal(2048, (int)PlayerAbnormalState.Confuse);
		Assert.True(player.IsAbnormalSet(PlayerAbnormalState.Root));
		Assert.True(player.IsInAnyAbnormalState(PlayerAbnormalState.CantMoveState));
		Assert.True(player.IsUnderFear());
		Assert.True(player.IsConfused());
		Assert.False(player.IsAbnormalSet(PlayerAbnormalState.CantMoveState));

		player.AbnormalState = PlayerAbnormalState.None;

		Assert.True(player.IsAbnormalSet(PlayerAbnormalState.None));
		Assert.True(player.IsInAnyAbnormalState(PlayerAbnormalState.None));
	}

	[Fact]
	public void Player_StanceStateMatchesJavaObserverPresence()
	{
		var player = new Player();

		Assert.False(player.IsUnderStance());

		player.StanceSkillId = 1234;

		Assert.True(player.IsUnderStance());

		player.StanceSkillId = 0;

		Assert.False(player.IsUnderStance());
	}

	[Fact]
	public void Player_RideSprintMatchesJavaGuardAndFpTaskIntent()
	{
		var player = new Player
		{
			LifeStats = new PlayerLifeStats(100, 100, 50),
			IsInRideMode = true,
			RideInfo = new PlayerRideInfo(NpcId: 9001, StartFp: 30, CostFp: 1, SprintSpeed: 12.0f, FlySpeed: 0, MoveSpeed: 9.0f),
		};

		Assert.True(player.RideInfo.CanSprint());
		Assert.True(player.CanStartRideSprint());

		player.StartRideSprint();

		Assert.True(player.IsInSprintMode);
		Assert.True(player.IsFpReduceActive);
		Assert.False(player.IsFpRestoreActive);
		Assert.True(player.CanEndRideSprint());

		player.EndRideSprint();

		Assert.False(player.IsInSprintMode);
		Assert.False(player.IsFpReduceActive);
		Assert.True(player.IsFpRestoreActive);

		player.LifeStats = new PlayerLifeStats(100, 100, 29);
		Assert.False(player.CanStartRideSprint());

		player.LifeStats = new PlayerLifeStats(100, 100, 50);
		player.SetCreatureState(PlayerCreatureState.Flying, enabled: true);
		Assert.False(player.CanStartRideSprint());

		player.SetCreatureState(PlayerCreatureState.Flying, enabled: false);
		player.RideInfo = player.RideInfo with { SprintSpeed = 0 };
		Assert.False(player.RideInfo.CanSprint());
		Assert.False(player.CanStartRideSprint());
	}

	[Fact]
	public void Player_CompleteFlyTeleportMatchesJavaWindstreamAndTransporterState()
	{
		var windstreamPlayer = new Player
		{
			CreatureState = PlayerCreatureState.Flying,
			FlightPathType = PlayerFlightPathType.Windstream,
		};

		windstreamPlayer.CompleteFlyTeleport();

		Assert.False(windstreamPlayer.IsInState(PlayerCreatureState.Flying));
		Assert.True(windstreamPlayer.IsInState(PlayerCreatureState.Active));
		Assert.True(windstreamPlayer.IsInState(PlayerCreatureState.Gliding));
		Assert.True(windstreamPlayer.IsFpReduceActive);
		Assert.Null(windstreamPlayer.FlightPathType);

		var transporterPlayer = new Player
		{
			CreatureState = PlayerCreatureState.Flying,
			FlightPathType = PlayerFlightPathType.FlightTransporter,
		};

		transporterPlayer.CompleteFlyTeleport();

		Assert.False(transporterPlayer.IsInState(PlayerCreatureState.Flying));
		Assert.True(transporterPlayer.IsInState(PlayerCreatureState.Active));
		Assert.False(transporterPlayer.IsInState(PlayerCreatureState.Gliding));
		Assert.False(transporterPlayer.IsFpReduceActive);
		Assert.Null(transporterPlayer.FlightPathType);
	}

	[Fact]
	public void Player_StartAndEndFlyingMatchJavaFpTaskIntent()
	{
		var player = new Player
		{
			IsInRideMode = true,
			CreatureState = PlayerCreatureState.Active,
		};

		player.StartFlying();

		Assert.True(player.IsInState(PlayerCreatureState.Flying));
		Assert.True(player.IsInState(PlayerCreatureState.FloatingCorpse));
		Assert.True(player.IsFpReduceActive);
		Assert.False(player.IsFpRestoreActive);

		player.EndFlying();

		Assert.False(player.IsInState(PlayerCreatureState.Flying));
		Assert.False(player.IsInState(PlayerCreatureState.Gliding));
		Assert.False(player.IsInState(PlayerCreatureState.FloatingCorpse));
		Assert.False(player.IsFpReduceActive);
		Assert.True(player.IsFpRestoreActive);
	}

	[Fact]
	public void Player_StopGlidingMatchesJavaFpTaskAndBroadcastDecision()
	{
		var walkingGlider = new Player
		{
			CreatureState = PlayerCreatureState.Gliding,
			IsFpReduceActive = true,
		};

		Assert.True(walkingGlider.StopGliding());
		Assert.False(walkingGlider.IsInState(PlayerCreatureState.Gliding));
		Assert.False(walkingGlider.IsFpReduceActive);
		Assert.True(walkingGlider.IsFpRestoreActive);

		var flyingGlider = new Player
		{
			CreatureState = PlayerCreatureState.Flying | PlayerCreatureState.Gliding,
			IsFpRestoreActive = true,
		};

		Assert.False(flyingGlider.StopGliding());
		Assert.True(flyingGlider.IsInState(PlayerCreatureState.Flying));
		Assert.False(flyingGlider.IsInState(PlayerCreatureState.Gliding));
		Assert.True(flyingGlider.IsFpReduceActive);
		Assert.False(flyingGlider.IsFpRestoreActive);
	}
}
