using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class QuestBonusHandlerOutcomePlanServiceTests
{
	[Fact]
	public void CreatePlan_MovieRewardStateAddsHatBoxIntentAndRandomMovieIntent()
	{
		var service = new QuestBonusHandlerOutcomePlanService();

		var plan = service.CreatePlan(new QuestBonusHandlerOutcomeInput(
			"MOVIE",
			new Dictionary<int, QuestBonusHandlerQuestState>
			{
				[80016] = new("REWARD", CompleteCount: 9),
			}));

		Assert.Equal(QuestBonusHandlerResult.Success, plan.Result);
		Assert.Equal(QuestBonusHandlerOutcomeStatus.HandlerSucceeded, plan.Status);
		Assert.Equal(80016, plan.HandlerQuestId);
		Assert.Equal(QuestBonusHandlerKind.Movie, plan.HandlerKind);
		var item = Assert.Single(plan.DirectRewardItems);
		Assert.Equal(188051106, item.ItemId);
		Assert.Equal(1, item.Count);
		var movie = Assert.Single(plan.SideEffects);
		Assert.Equal(QuestBonusHandlerSideEffectKind.RandomMovie, movie.Kind);
		Assert.Equal([103, 104], movie.CandidateIds);
	}

	[Fact]
	public void CreatePlan_MovieRewardStateWithoutCompleteCountNineOnlyPlansMovieIntent()
	{
		var service = new QuestBonusHandlerOutcomePlanService();

		var plan = service.CreatePlan(new QuestBonusHandlerOutcomeInput(
			"MOVIE",
			new Dictionary<int, QuestBonusHandlerQuestState>
			{
				[80016] = new("REWARD", CompleteCount: 8),
			}));

		Assert.Equal(QuestBonusHandlerResult.Success, plan.Result);
		Assert.Empty(plan.DirectRewardItems);
		Assert.Equal([103, 104], Assert.Single(plan.SideEffects).CandidateIds);
	}

	[Theory]
	[InlineData("START", 0, QuestBonusHandlerResult.Success)]
	[InlineData("COMPLETE", 0, QuestBonusHandlerResult.Success)]
	[InlineData("REWARD", 0, QuestBonusHandlerResult.Failed)]
	[InlineData("START", 1, QuestBonusHandlerResult.Failed)]
	public void CreatePlan_LunarGateMatchesJavaStatusAndVarRule(string status, int var0, QuestBonusHandlerResult expected)
	{
		var service = new QuestBonusHandlerOutcomePlanService();

		var plan = service.CreatePlan(new QuestBonusHandlerOutcomeInput(
			"LUNAR",
			new Dictionary<int, QuestBonusHandlerQuestState>
			{
				[80034] = new(status, Var0: var0),
			}));

		Assert.Equal(expected, plan.Result);
		Assert.Equal(80034, plan.HandlerQuestId);
		Assert.Equal(QuestBonusHandlerKind.LunarGate, plan.HandlerKind);
		Assert.Empty(plan.DirectRewardItems);
		Assert.Empty(plan.SideEffects);
	}

	[Theory]
	[InlineData("START", 0, QuestBonusHandlerResult.Success)]
	[InlineData("COMPLETE", 0, QuestBonusHandlerResult.Success)]
	[InlineData("START", 2, QuestBonusHandlerResult.Failed)]
	[InlineData("REWARD", 0, QuestBonusHandlerResult.Failed)]
	public void CreatePlan_RiftGateMatchesJavaStatusAndVarRule(string status, int var0, QuestBonusHandlerResult expected)
	{
		var service = new QuestBonusHandlerOutcomePlanService();

		var plan = service.CreatePlan(new QuestBonusHandlerOutcomeInput(
			"RIFT",
			new Dictionary<int, QuestBonusHandlerQuestState>
			{
				[80137] = new(status, Var0: var0),
			}));

		Assert.Equal(expected, plan.Result);
		Assert.Equal(80137, plan.HandlerQuestId);
		Assert.Equal(QuestBonusHandlerKind.RiftGate, plan.HandlerKind);
		Assert.Empty(plan.DirectRewardItems);
		Assert.Empty(plan.SideEffects);
	}

	[Fact]
	public void CreatePlan_FirstLoadedRegisteredHandlerWinsLikeJavaQuestEngine()
	{
		var service = new QuestBonusHandlerOutcomePlanService();
		var states = new Dictionary<int, QuestBonusHandlerQuestState>
		{
			[80034] = new("REWARD"),
			[80035] = new("START", Var0: 0),
		};

		var firstLoadedPlan = service.CreatePlan(new QuestBonusHandlerOutcomeInput("LUNAR", states));
		var secondLoadedPlan = service.CreatePlan(new QuestBonusHandlerOutcomeInput("LUNAR", states, new HashSet<int> { 80035 }));

		Assert.Equal(80034, firstLoadedPlan.HandlerQuestId);
		Assert.Equal(QuestBonusHandlerResult.Failed, firstLoadedPlan.Result);
		Assert.Equal(80035, secondLoadedPlan.HandlerQuestId);
		Assert.Equal(QuestBonusHandlerResult.Success, secondLoadedPlan.Result);
	}

	[Fact]
	public void CreatePlan_FirstLoadedUnknownHandlerStopsLaterHandlersLikeJavaQuestEngine()
	{
		var service = new QuestBonusHandlerOutcomePlanService();
		var registrations = new[]
		{
			new QuestBonusHandlerRegistration(90001, "MOVIE", (QuestBonusHandlerKind)999),
			new QuestBonusHandlerRegistration(80016, "MOVIE", QuestBonusHandlerKind.Movie, [103, 104]),
		};
		var states = new Dictionary<int, QuestBonusHandlerQuestState>
		{
			[80016] = new("REWARD", CompleteCount: 9),
		};

		var plan = service.CreatePlan(new QuestBonusHandlerOutcomeInput("MOVIE", states), registrations);

		Assert.Equal(QuestBonusHandlerResult.Unknown, plan.Result);
		Assert.Equal(QuestBonusHandlerOutcomeStatus.HandlerReturnedUnknown, plan.Status);
		Assert.Equal(90001, plan.HandlerQuestId);
		Assert.Equal((QuestBonusHandlerKind)999, plan.HandlerKind);
		Assert.Empty(plan.DirectRewardItems);
		Assert.Empty(plan.SideEffects);
	}

	[Fact]
	public void CreateHandlerExceptionPlan_RepresentsJavaCatchAsFailedOutcome()
	{
		var service = new QuestBonusHandlerOutcomePlanService();
		var input = new QuestBonusHandlerOutcomeInput(
			"MOVIE",
			new Dictionary<int, QuestBonusHandlerQuestState>
			{
				[80016] = new("REWARD"),
			});
		var registration = new QuestBonusHandlerRegistration(80016, "MOVIE", QuestBonusHandlerKind.Movie, [103, 104]);

		var plan = service.CreateHandlerExceptionPlan(input, registration);

		Assert.Equal(QuestBonusHandlerResult.Failed, plan.Result);
		Assert.Equal(QuestBonusHandlerOutcomeStatus.HandlerException, plan.Status);
		Assert.Equal(80016, plan.HandlerQuestId);
		Assert.Equal(QuestBonusHandlerKind.Movie, plan.HandlerKind);
		Assert.Empty(plan.DirectRewardItems);
		Assert.Empty(plan.SideEffects);
	}

	[Fact]
	public void CreatePlan_UnknownBonusTypeAllowsLaterBonusServiceLikeJavaUnknownResult()
	{
		var service = new QuestBonusHandlerOutcomePlanService();

		var plan = service.CreatePlan(new QuestBonusHandlerOutcomeInput("MANASTONE", new Dictionary<int, QuestBonusHandlerQuestState>()));

		Assert.Equal(QuestBonusHandlerResult.Unknown, plan.Result);
		Assert.Equal(QuestBonusHandlerOutcomeStatus.NoRegisteredHandler, plan.Status);
		Assert.Null(plan.HandlerQuestId);
		Assert.Null(plan.HandlerKind);
	}

	[Fact]
	public void CreatePlan_NoLoadedRegisteredHandlerReturnsUnknownLikeJavaMissingHandler()
	{
		var service = new QuestBonusHandlerOutcomePlanService();

		var plan = service.CreatePlan(new QuestBonusHandlerOutcomeInput(
			"MOVIE",
			new Dictionary<int, QuestBonusHandlerQuestState>(),
			new HashSet<int> { 99999 }));

		Assert.Equal(QuestBonusHandlerResult.Unknown, plan.Result);
		Assert.Equal(QuestBonusHandlerOutcomeStatus.NoLoadedHandler, plan.Status);
		Assert.Null(plan.HandlerQuestId);
	}
}
