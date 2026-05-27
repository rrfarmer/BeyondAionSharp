using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerDeathWorkflowReportServiceTests
{
	private readonly PlayerDeathWorkflowPlanService _planService = new();

	[Fact]
	public void CreateReport_OrdinaryDeathFlattensComposedMetadataInJavaOrder()
	{
		var player = new Player
		{
			ObjectId = PlayerObjectId,
			CreatureState = PlayerCreatureState.Active | PlayerCreatureState.Flying,
			FlyState = PlayerFlyState.Flying,
			LifeStats = new PlayerLifeStats(CurrentHp: 0, CurrentMp: 0, CurrentFp: 0),
		};
		var plan = _planService.CreatePlan(
			player,
			new PlayerDeathWorkflowFacts(
				HasSummon: true,
				LastAttackerMasterIsNpcOrPlayerSelf: true,
				PlayerLevel: 10,
				LastAttackerObjectId: LastAttackerObjectId,
				KnownCreatureObjectIds: new[] { KnownCreatureObjectId }));

		var report = PlayerDeathWorkflowReportService.CreateReport(plan);

		Assert.Equal(PlayerDeathWorkflowStatus.PlannedFullPlayerDeath, report.Status);
		Assert.Equal(PlayerObjectId, report.PlayerObjectId);
		Assert.False(report.IsLive);
		AssertOrdered(
			report.Rows,
			"cancelCurrentSkill(null)",
			"setRebirthReviveInfo()",
			"doMode(RELEASE, summon, UNSPECIFIED)",
			nameof(PlayerDeathStateTransitionStep.CheckFlyingBeforeDeath),
			nameof(PlayerDeathStateTransitionStep.ClearFlyingAndGlidingFlyState),
			nameof(PlayerDeathCoreSideEffectPlanStep.AbortMove),
			nameof(PlayerDeathCoreSideEffectPlanStep.ClearCasting),
			nameof(PlayerDeathCoreSideEffectPlanStep.RemoveAllEffects),
			nameof(PlayerDeathStateTransitionStep.SetFloatingCorpseState),
			"notifyDeathObservers(lastAttacker)",
			"broadcastPacketAndReceive(SM_EMOTION DIE)",
			"known creatures stopHating(owner)",
			"scheduleShowResurrectionOptions()",
			"onDie(player, lastAttacker)",
			"onDie(lastAttacker, player)",
			"doReward()",
			"calculateExpLoss()",
			"onDie(QuestEnv)");
		Assert.Contains(report.Rows, row => row.Kind == PlayerDeathWorkflowReportRowKind.PacketIntent && row.Notes.Contains(LastAttackerObjectId.ToString()));
		Assert.Contains(report.Rows, row => row.Kind == PlayerDeathWorkflowReportRowKind.SchedulerIntent);
		Assert.Contains(report.Rows, row => row.Kind == PlayerDeathWorkflowReportRowKind.LiveStateBoundary);
		Assert.All(report.Rows, row => Assert.False(row.IsLive));
		Assert.Contains("PlayerController.onDie", report.JavaSource);
	}

	[Fact]
	public void CreateReport_DuelEarlyReturnStopsBeforeSummonAndSuperOnDie()
	{
		var player = new Player
		{
			ObjectId = PlayerObjectId,
			CreatureState = PlayerCreatureState.Active | PlayerCreatureState.Flying,
		};
		var plan = _planService.CreatePlan(
			player,
			new PlayerDeathWorkflowFacts(
				IsDueling: true,
				KilledByDuelOpponent: true,
				HasSummon: true,
				LastAttackerMasterIsNpcOrPlayerSelf: true,
				PlayerLevel: 10));

		var report = PlayerDeathWorkflowReportService.CreateReport(plan);

		Assert.Equal(PlayerDeathWorkflowStatus.ReturnedAfterDuelOpponentKill, report.Status);
		AssertOrdered(
			report.Rows,
			"cancelCurrentSkill(null)",
			"setRebirthReviveInfo()",
			"lastAttacker.getMaster()",
			"DuelService.isDueling(player)",
			"loseDuel(player)",
			"restore duelist HP/MP floors",
			"return after duel opponent kill");
		Assert.DoesNotContain(report.Rows, row => row.JavaOperation == "doMode(RELEASE, summon, UNSPECIFIED)");
		Assert.DoesNotContain(report.Rows, row => row.JavaOperation == nameof(PlayerDeathCoreSideEffectPlanStep.AbortMove));
		Assert.DoesNotContain(report.Rows, row => row.Kind == PlayerDeathWorkflowReportRowKind.PacketIntent);
		Assert.DoesNotContain(report.Rows, row => row.Kind == PlayerDeathWorkflowReportRowKind.SchedulerIntent);
		Assert.Equal(PlayerDeathWorkflowReportRowKind.EarlyReturn, report.Rows[^1].Kind);
	}

	private static void AssertOrdered(IReadOnlyList<PlayerDeathWorkflowReportRow> rows, params string[] expectedOperations)
	{
		var previousIndex = -1;
		foreach (var operation in expectedOperations)
		{
			var currentIndex = Array.FindIndex(rows.ToArray(), row => row.JavaOperation == operation);
			Assert.True(currentIndex > previousIndex, $"Expected {operation} after index {previousIndex}, actual order: {string.Join(", ", rows.Select(row => row.JavaOperation))}");
			previousIndex = currentIndex;
		}
	}

	private const int PlayerObjectId = 1001;
	private const int LastAttackerObjectId = 2002;
	private const int KnownCreatureObjectId = 3003;
}
