using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerDeathResurrectionOptionsPlanServiceTests
{
	[Fact]
	public void CreatePlan_DeadWithoutTeleportTaskPlansSmDieAfterJavaDelay()
	{
		var player = CreatePlayer(currentHp: 0, PlayerCreatureState.Dead);

		var plan = PlayerDeathResurrectionOptionsPlanService.CreatePlan(
			player,
			hasTeleportTaskAtCallback: false);

		Assert.Equal(PlayerDeathResurrectionOptionsPlanStatus.SendSmDie, plan.Status);
		Assert.Equal(500, plan.DelayMilliseconds);
		Assert.Equal(1, plan.TeleportTaskOrdinal);
		Assert.Equal("TELEPORT", plan.TeleportTaskName);
		Assert.Equal(SmDie.PacketOpCode, plan.SmDiePacketOpcode);
		Assert.True(plan.IsDeadAtCallback);
		Assert.False(plan.HasTeleportTaskAtCallback);
		Assert.True(plan.ShouldSendPacket);
		Assert.False(plan.ScheduledLiveTask);
		Assert.False(plan.IsLive);
		AssertOrdered(
			plan.Steps,
			PlayerDeathResurrectionOptionsPlanStep.ScheduleCallback,
			PlayerDeathResurrectionOptionsPlanStep.CheckPlayerDead,
			PlayerDeathResurrectionOptionsPlanStep.CheckTeleportTask,
			PlayerDeathResurrectionOptionsPlanStep.SendSmDie);
		Assert.Contains("scheduleShowResurrectionOptions", plan.JavaSource);
	}

	[Fact]
	public void CreatePlan_AliveAtCallbackSkipsSmDieLikeJavaGuard()
	{
		var player = CreatePlayer(currentHp: 1, PlayerCreatureState.Active);

		var plan = PlayerDeathResurrectionOptionsPlanService.CreatePlan(
			player,
			hasTeleportTaskAtCallback: false);

		Assert.Equal(PlayerDeathResurrectionOptionsPlanStatus.SkipNotDead, plan.Status);
		Assert.False(plan.IsDeadAtCallback);
		Assert.False(plan.ShouldSendPacket);
		Assert.DoesNotContain(PlayerDeathResurrectionOptionsPlanStep.CheckTeleportTask, plan.Steps);
		Assert.DoesNotContain(PlayerDeathResurrectionOptionsPlanStep.SendSmDie, plan.Steps);
	}

	[Fact]
	public void CreatePlan_TeleportTaskAtCallbackSuppressesSmDie()
	{
		var player = CreatePlayer(currentHp: 0, PlayerCreatureState.Dead);

		var plan = PlayerDeathResurrectionOptionsPlanService.CreatePlan(
			player,
			hasTeleportTaskAtCallback: true);

		Assert.Equal(PlayerDeathResurrectionOptionsPlanStatus.SkipTeleportTask, plan.Status);
		Assert.True(plan.IsDeadAtCallback);
		Assert.True(plan.HasTeleportTaskAtCallback);
		Assert.False(plan.ShouldSendPacket);
		Assert.Contains(PlayerDeathResurrectionOptionsPlanStep.CheckTeleportTask, plan.Steps);
		Assert.DoesNotContain(PlayerDeathResurrectionOptionsPlanStep.SendSmDie, plan.Steps);
	}

	[Fact]
	public void CreatePlan_FloatingCorpseWithZeroHpStillPlansResurrectionOptions()
	{
		var player = CreatePlayer(currentHp: 0, PlayerCreatureState.FloatingCorpse);

		var plan = PlayerDeathResurrectionOptionsPlanService.CreatePlan(
			player,
			hasTeleportTaskAtCallback: false);

		Assert.Equal(PlayerDeathResurrectionOptionsPlanStatus.SendSmDie, plan.Status);
		Assert.Equal(PlayerCreatureState.FloatingCorpse, plan.CreatureStateAtCallback);
		Assert.True(plan.IsDeadAtCallback);
		Assert.True(plan.ShouldSendPacket);
	}

	private static Player CreatePlayer(int currentHp, PlayerCreatureState creatureState)
	{
		return new Player
		{
			ObjectId = PlayerObjectId,
			CreatureState = creatureState,
			LifeStats = new PlayerLifeStats(currentHp, CurrentMp: 0, CurrentFp: 0),
		};
	}

	private static void AssertOrdered(IReadOnlyList<PlayerDeathResurrectionOptionsPlanStep> actual, params PlayerDeathResurrectionOptionsPlanStep[] expected)
	{
		var previousIndex = -1;
		foreach (var step in expected)
		{
			var currentIndex = Array.IndexOf(actual.ToArray(), step);
			Assert.True(currentIndex > previousIndex, $"Expected {step} after index {previousIndex}, actual order: {string.Join(", ", actual)}");
			previousIndex = currentIndex;
		}
	}

	private const int PlayerObjectId = 1001;
}
