using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class BindPointTeleportRuntimeStateOwnerTests
{
	[Fact]
	public async Task ScheduleSkillUseTask_ReplacesExistingTaskAndCancelsOldTask()
	{
		await using var threadPoolManager = CreateThreadPoolManager();
		var owner = new BindPointTeleportRuntimeStateOwner(threadPoolManager);
		var oldCallbackCount = 0;
		var newCallbackCount = 0;

		var first = owner.ScheduleSkillUseTask(
			playerObjectId: 8001,
			locId: 6101,
			_ =>
			{
				Interlocked.Increment(ref oldCallbackCount);
				return ValueTask.CompletedTask;
			},
			delay: TimeSpan.FromSeconds(30));
		var second = owner.ScheduleSkillUseTask(
			playerObjectId: 8001,
			locId: 6102,
			_ =>
			{
				Interlocked.Increment(ref newCallbackCount);
				return ValueTask.CompletedTask;
			},
			delay: TimeSpan.FromSeconds(30));

		Assert.Equal(BindPointTeleportRuntimeTaskOwnerStatus.ScheduledNewTask, first.Status);
		Assert.Equal(BindPointTeleportRuntimeTaskOwnerStatus.ReplacedExistingTask, second.Status);
		Assert.True(second.HadExistingTask);
		Assert.True(second.CancelledExistingTask);
		Assert.True(owner.HasSkillUseTask(8001));
		Assert.Equal(1, owner.PendingSkillUseTaskCount);

		await Task.Delay(50);
		Assert.Equal(0, oldCallbackCount);
		Assert.Equal(0, newCallbackCount);
	}

	[Fact]
	public async Task CancelSkillUseTask_RemovesThenCancelsExistingTask()
	{
		await using var threadPoolManager = CreateThreadPoolManager();
		var owner = new BindPointTeleportRuntimeStateOwner(threadPoolManager);
		var callbackCount = 0;
		owner.ScheduleSkillUseTask(
			playerObjectId: 8002,
			locId: 6102,
			_ =>
			{
				Interlocked.Increment(ref callbackCount);
				return ValueTask.CompletedTask;
			},
			delay: TimeSpan.FromSeconds(30));

		var cancel = owner.CancelSkillUseTask(playerObjectId: 8002, locId: 6102);

		Assert.Equal(BindPointTeleportRuntimeTaskOwnerStatus.CancelledExistingTask, cancel.Status);
		Assert.True(cancel.HadExistingTask);
		Assert.True(cancel.CancelledExistingTask);
		Assert.True(cancel.RemovedTask);
		Assert.False(owner.HasSkillUseTask(8002));
		Assert.Equal(0, owner.PendingSkillUseTaskCount);

		await Task.Delay(50);
		Assert.Equal(0, callbackCount);
	}

	[Fact]
	public async Task CancelSkillUseTask_NoopsWhenTaskMissing()
	{
		await using var threadPoolManager = CreateThreadPoolManager();
		var owner = new BindPointTeleportRuntimeStateOwner(threadPoolManager);

		var cancel = owner.CancelSkillUseTask(playerObjectId: 8003, locId: 6103);

		Assert.Equal(BindPointTeleportRuntimeTaskOwnerStatus.NoTaskToCancel, cancel.Status);
		Assert.False(cancel.HadExistingTask);
		Assert.False(cancel.CancelledExistingTask);
		Assert.False(cancel.RemovedTask);
		Assert.Equal(BindPointTeleportSkillUseTaskPlanStatus.NoTaskToCancel, cancel.Plan.Status);
	}

	[Fact]
	public async Task CompletedSkillUseTask_KeepsSlotUntilCancelOrReplaceLikeJavaHasTask()
	{
		await using var threadPoolManager = CreateThreadPoolManager();
		var owner = new BindPointTeleportRuntimeStateOwner(threadPoolManager);
		var callbackCount = 0;

		owner.ScheduleSkillUseTask(
			playerObjectId: 8004,
			locId: 6104,
			_ =>
			{
				Interlocked.Increment(ref callbackCount);
				return ValueTask.CompletedTask;
			},
			delay: TimeSpan.Zero);

		await WaitUntilAsync(() => Volatile.Read(ref callbackCount) == 1);

		Assert.True(owner.HasSkillUseTask(8004));
		Assert.Equal(1, owner.PendingSkillUseTaskCount);

		var cancel = owner.CancelSkillUseTask(playerObjectId: 8004, locId: 6104);

		Assert.Equal(BindPointTeleportRuntimeTaskOwnerStatus.CancelledExistingTask, cancel.Status);
		Assert.True(cancel.HadExistingTask);
		Assert.False(cancel.CancelledExistingTask);
		Assert.True(cancel.RemovedTask);
		Assert.False(owner.HasSkillUseTask(8004));
	}

	[Fact]
	public async Task AddCooldown_StoresJavaTenMinuteEndMillisByPlayer()
	{
		await using var threadPoolManager = CreateThreadPoolManager();
		var owner = new BindPointTeleportRuntimeStateOwner(threadPoolManager);

		var fact = owner.AddCooldown(playerObjectId: 8005, locId: 6105, currentTimeMillis: 1_000);

		Assert.Equal(8005, fact.PlayerObjectId);
		Assert.Equal(6105, fact.LocId);
		Assert.Equal(601_000, fact.CooldownEndMillis);
		Assert.Equal(fact, owner.GetCooldown(8005));
		Assert.Null(owner.GetCooldown(8006));
		Assert.Equal(1, owner.CooldownCount);
	}

	[Fact]
	public async Task LookupCooldown_UsesJavaWholeSecondTruncationAndKeepsExpiredFact()
	{
		await using var threadPoolManager = CreateThreadPoolManager();
		var owner = new BindPointTeleportRuntimeStateOwner(threadPoolManager);
		owner.AddCooldown(playerObjectId: 8006, locId: 6106, currentTimeMillis: 1_000);

		var active = owner.CreateLookupCooldownPlan(playerObjectId: 8006, currentTimeMillis: 2_499);
		var expired = owner.CreateLookupCooldownPlan(playerObjectId: 8006, currentTimeMillis: 601_000);

		Assert.Equal(BindPointTeleportCooldownPlanStatus.ActiveCooldown, active.Status);
		Assert.Equal(598, active.TimeLeftSeconds);
		Assert.Equal(BindPointTeleportCooldownPlanStatus.ExpiredCooldown, expired.Status);
		Assert.Equal(0, expired.TimeLeftSeconds);
		Assert.NotNull(owner.GetCooldown(8006));
		Assert.Equal(1, owner.CooldownCount);
	}

	[Fact]
	public async Task ClearPlayer_CancelsPendingTaskWithoutRemovingOtherPlayers()
	{
		await using var threadPoolManager = CreateThreadPoolManager();
		var owner = new BindPointTeleportRuntimeStateOwner(threadPoolManager);
		var clearedCallbackCount = 0;
		var otherCallbackCount = 0;
		owner.ScheduleSkillUseTask(
			playerObjectId: 8007,
			locId: 6107,
			_ =>
			{
				Interlocked.Increment(ref clearedCallbackCount);
				return ValueTask.CompletedTask;
			},
			delay: TimeSpan.FromSeconds(30));
		owner.ScheduleSkillUseTask(
			playerObjectId: 8008,
			locId: 6108,
			_ =>
			{
				Interlocked.Increment(ref otherCallbackCount);
				return ValueTask.CompletedTask;
			},
			delay: TimeSpan.FromSeconds(30));

		var cleared = owner.ClearPlayer(8007);

		Assert.Equal(BindPointTeleportRuntimeTaskOwnerStatus.ClearedPlayer, cleared.Status);
		Assert.True(cleared.CancelledExistingTask);
		Assert.False(owner.HasSkillUseTask(8007));
		Assert.True(owner.HasSkillUseTask(8008));
		Assert.Equal(1, owner.PendingSkillUseTaskCount);

		await Task.Delay(50);
		Assert.Equal(0, clearedCallbackCount);
		Assert.Equal(0, otherCallbackCount);
	}

	private static ThreadPoolManager CreateThreadPoolManager()
	{
		return new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
	}

	private static async Task WaitUntilAsync(Func<bool> predicate)
	{
		var deadline = DateTime.UtcNow.AddSeconds(2);
		while (DateTime.UtcNow < deadline)
		{
			if (predicate())
				return;

			await Task.Delay(10);
		}

		Assert.True(predicate());
	}
}
