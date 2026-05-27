using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerProtectionActiveTaskSightedRecipientTraceServiceTests
{
	[Fact]
	public void CreateTrace_ProjectsSourceFirstThenRecipientsThatSeeSource()
	{
		var fanoutPlan = CreateStartFanoutPlan();
		var membership = CreateSourceKnownList(
			new PlayerKnownListMembershipCandidate(SightedPlayerObjectId, IsVisibleToOwner: true),
			new PlayerKnownListMembershipCandidate(InvisibleToSourcePlayerObjectId, IsVisibleToOwner: false),
			new PlayerKnownListMembershipCandidate(DoesNotSeeSourcePlayerObjectId, IsVisibleToOwner: true));

		var trace = PlayerProtectionActiveTaskSightedRecipientTraceService.CreateTrace(
			fanoutPlan,
			membership,
			[
				new PlayerProtectionActiveTaskRecipientVisibilityFact(SightedPlayerObjectId, RecipientSeesSource: true),
				new PlayerProtectionActiveTaskRecipientVisibilityFact(InvisibleToSourcePlayerObjectId, RecipientSeesSource: true),
				new PlayerProtectionActiveTaskRecipientVisibilityFact(DoesNotSeeSourcePlayerObjectId, RecipientSeesSource: false),
			]);

		Assert.Equal(PlayerProtectionActiveTaskSightedRecipientTraceStatus.Projected, trace.Status);
		Assert.Same(fanoutPlan, trace.FanoutPlan);
		Assert.True(trace.SendsSourceFirst);
		Assert.True(trace.UsesSourceKnownListTraversal);
		Assert.True(trace.UsesRecipientKnownListSeesFilter);
		Assert.False(trace.IsLive);
		Assert.Equal(
			[SourcePlayerObjectId, SightedPlayerObjectId, InvisibleToSourcePlayerObjectId],
			trace.Recipients.Select(recipient => recipient.PlayerObjectId));
		Assert.Equal(PlayerProtectionActiveTaskSightedRecipientKind.SourceSelf, trace.Recipients[0].Kind);
		Assert.All(trace.Recipients.Skip(1), recipient =>
			Assert.Equal(PlayerProtectionActiveTaskSightedRecipientKind.KnownListSightedPlayer, recipient.Kind));
		Assert.Contains("other.getKnownList().sees(source)", trace.JavaSource);
	}

	[Fact]
	public void CreateTrace_DeduplicatesSourceKnownListCandidates()
	{
		var fanoutPlan = CreateStartFanoutPlan();
		var membership = CreateSourceKnownList(
			new PlayerKnownListMembershipCandidate(SightedPlayerObjectId, IsVisibleToOwner: true),
			new PlayerKnownListMembershipCandidate(SightedPlayerObjectId, IsVisibleToOwner: true));

		var trace = PlayerProtectionActiveTaskSightedRecipientTraceService.CreateTrace(
			fanoutPlan,
			membership,
			[new PlayerProtectionActiveTaskRecipientVisibilityFact(SightedPlayerObjectId, RecipientSeesSource: true)]);

		Assert.True(trace.DuplicateKnownObjectIdsCollapsed);
		Assert.Equal([SourcePlayerObjectId, SightedPlayerObjectId], trace.Recipients.Select(recipient => recipient.PlayerObjectId));
		Assert.Equal(PlayerProtectionActiveTaskKnownListOrdering.ConcurrentHashMapUnspecified, trace.KnownListOrdering);
	}

	[Fact]
	public void CreateTrace_MissingRecipientVisibilityFactSkipsKnownPlayer()
	{
		var fanoutPlan = CreateStartFanoutPlan();
		var membership = CreateSourceKnownList(new PlayerKnownListMembershipCandidate(SightedPlayerObjectId, IsVisibleToOwner: true));

		var trace = PlayerProtectionActiveTaskSightedRecipientTraceService.CreateTrace(
			fanoutPlan,
			membership,
			recipientVisibilityFacts: null);

		Assert.Equal([SourcePlayerObjectId], trace.Recipients.Select(recipient => recipient.PlayerObjectId));
	}

	[Fact]
	public void CreateTrace_SkippedFanoutPlanHasNoRecipients()
	{
		var player = new Player { ObjectId = SourcePlayerObjectId };
		player.SetVisualState(PlayerVisualStates.Blinking);
		var sourcePlan = PlayerProtectionActiveTaskPlanService.CreateStartPlan(player);
		var fanoutPlan = PlayerProtectionActiveTaskFanoutPlanService.Create(
			sourcePlan,
			PlayerProtectionActiveTaskFanoutAction.Start);
		var membership = CreateSourceKnownList(new PlayerKnownListMembershipCandidate(SightedPlayerObjectId, IsVisibleToOwner: true));

		var trace = PlayerProtectionActiveTaskSightedRecipientTraceService.CreateTrace(
			fanoutPlan,
			membership,
			[new PlayerProtectionActiveTaskRecipientVisibilityFact(SightedPlayerObjectId, RecipientSeesSource: true)]);

		Assert.Equal(PlayerProtectionActiveTaskSightedRecipientTraceStatus.NoBroadcast, trace.Status);
		Assert.False(trace.SendsSourceFirst);
		Assert.False(trace.UsesSourceKnownListTraversal);
		Assert.False(trace.UsesRecipientKnownListSeesFilter);
		Assert.Empty(trace.Recipients);
	}

	private static PlayerProtectionActiveTaskFanoutPlan CreateStartFanoutPlan()
	{
		var player = new Player { ObjectId = SourcePlayerObjectId };
		var sourcePlan = PlayerProtectionActiveTaskPlanService.CreateStartPlan(player);
		return PlayerProtectionActiveTaskFanoutPlanService.Create(
			sourcePlan,
			PlayerProtectionActiveTaskFanoutAction.Start);
	}

	private static PlayerKnownListMembershipSnapshot CreateSourceKnownList(
		params PlayerKnownListMembershipCandidate[] candidates)
	{
		var membershipService = new PlayerKnownListMembershipService();
		return membershipService.UpsertKnownPlayers(SourcePlayerObjectId, candidates);
	}

	private const int SourcePlayerObjectId = 1001;
	private const int SightedPlayerObjectId = 1002;
	private const int InvisibleToSourcePlayerObjectId = 1003;
	private const int DoesNotSeeSourcePlayerObjectId = 1004;
}
