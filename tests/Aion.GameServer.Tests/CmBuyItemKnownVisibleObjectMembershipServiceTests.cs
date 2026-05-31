using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class CmBuyItemKnownVisibleObjectMembershipServiceTests
{
	[Fact]
	public void UpsertKnownObjects_StoresNpcAndPlayerMembershipAndExcludesOwner()
	{
		var membership = new CmBuyItemKnownVisibleObjectMembershipService();

		var snapshot = membership.UpsertKnownObjects(
			OwnerPlayerObjectId,
			[
				new CmBuyItemKnownVisibleObjectMembershipCandidate(OwnerPlayerObjectId, CmBuyItemKnownVisibleObjectKind.Player, IsVisibleToOwner: true),
				new CmBuyItemKnownVisibleObjectMembershipCandidate(SellerNpcObjectId, CmBuyItemKnownVisibleObjectKind.Npc, IsVisibleToOwner: true),
				new CmBuyItemKnownVisibleObjectMembershipCandidate(SellerPlayerObjectId, CmBuyItemKnownVisibleObjectKind.Player, IsVisibleToOwner: false),
			]);

		Assert.Equal([SellerPlayerObjectId, SellerNpcObjectId], snapshot.KnownObjectIds.Order());
		Assert.True(snapshot.ExcludesOwnerByNormalAddPath);
		Assert.True(snapshot.DeduplicatesByObjectId);
		Assert.False(snapshot.IsLive);
		Assert.Contains(snapshot.Entries, entry => entry.Kind == CmBuyItemKnownVisibleObjectKind.Npc && entry.IsVisibleToOwner);
		Assert.Contains(snapshot.Entries, entry => entry.Kind == CmBuyItemKnownVisibleObjectKind.Player && !entry.IsVisibleToOwner);
	}

	[Fact]
	public void CreatePlan_KnownNpcTargetReturnsTrueFromGenericMembershipSnapshot()
	{
		var membership = new CmBuyItemKnownVisibleObjectMembershipService();
		membership.UpsertKnownObjects(
			OwnerPlayerObjectId,
			[new CmBuyItemKnownVisibleObjectMembershipCandidate(SellerNpcObjectId, CmBuyItemKnownVisibleObjectKind.Npc, IsVisibleToOwner: true)]);

		var plan = CmBuyItemKnownVisibleObjectResolverAdapterService.CreatePlan(
			CreatePlayer(OwnerPlayerObjectId),
			SellerNpcObjectId,
			membership);

		Assert.Equal(CmBuyItemKnownVisibleObjectResolverAdapterStatus.KnownObjectTarget, plan.Status);
		Assert.True(plan.IsKnownByPlayer);
		Assert.Equal(CmBuyItemKnownVisibleObjectKind.Npc, plan.SnapshotObjectKind);
		Assert.True(plan.UsesKnownVisibleObjectSnapshot);
		Assert.False(plan.IsJavaKnownListParity);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePlan_UnknownNpcTargetReturnsFalseFromGenericMembershipSnapshot()
	{
		var membership = new CmBuyItemKnownVisibleObjectMembershipService();

		var plan = CmBuyItemKnownVisibleObjectResolverAdapterService.CreatePlan(
			CreatePlayer(OwnerPlayerObjectId),
			SellerNpcObjectId,
			membership);

		Assert.Equal(CmBuyItemKnownVisibleObjectResolverAdapterStatus.UnknownObjectTarget, plan.Status);
		Assert.False(plan.IsKnownByPlayer);
		Assert.Null(plan.SnapshotObjectKind);
		Assert.True(plan.UsesKnownVisibleObjectSnapshot);
	}

	[Fact]
	public void RefreshOwnerFromSuppliedFacts_UpsertsVisiblePlayersAndNpcsOnly()
	{
		var membership = new CmBuyItemKnownVisibleObjectMembershipService();
		var adapter = new CmBuyItemKnownVisibleObjectPopulationAdapterService(membership);
		var owner = CreatePlayer(OwnerPlayerObjectId, x: 0, worldId: 210010000);
		var nearPlayer = CreatePlayer(SellerPlayerObjectId, x: 94, worldId: 210010000);
		var farPlayer = CreatePlayer(2002, x: 96, worldId: 210010000);
		var otherWorldPlayer = CreatePlayer(2003, x: 1, worldId: 220010000);
		var nearNpc = CreateNpc(SellerNpcObjectId, x: 11, worldId: 210010000);
		var farNpc = CreateNpc(9002, x: 200, worldId: 210010000);
		var otherWorldNpc = CreateNpc(9003, x: 1, worldId: 220010000);

		var result = adapter.RefreshOwnerFromSuppliedFacts(
			owner,
			[owner, nearPlayer, farPlayer, otherWorldPlayer],
			[nearNpc, farNpc, otherWorldNpc]);

		Assert.True(result.UsesWorldVisibilityApproximation);
		Assert.False(result.IsJavaRegionKnownListParity);
		Assert.False(result.IsLive);
		Assert.Equal(4, result.PlayerCandidateCount);
		Assert.Equal(3, result.NpcCandidateCount);
		Assert.Equal(2, result.UpsertedVisibleObjectCount);
		Assert.Equal(0, result.RemovedStaleObjectCount);
		Assert.Equal([SellerPlayerObjectId, SellerNpcObjectId], result.Snapshot.KnownObjectIds.Order());
		Assert.Contains(result.Snapshot.Entries, entry =>
			entry.KnownObjectId == SellerPlayerObjectId
			&& entry.Kind == CmBuyItemKnownVisibleObjectKind.Player
			&& entry.UpdateReason == CmBuyItemKnownVisibleObjectMembershipUpdateReason.KnownListRefresh);
		Assert.Contains(result.Snapshot.Entries, entry =>
			entry.KnownObjectId == SellerNpcObjectId
			&& entry.Kind == CmBuyItemKnownVisibleObjectKind.Npc
			&& entry.UpdateReason == CmBuyItemKnownVisibleObjectMembershipUpdateReason.KnownListRefresh);
	}

	[Fact]
	public void RefreshOwnerFromSuppliedFacts_RemovesStaleObjectFacts()
	{
		var membership = new CmBuyItemKnownVisibleObjectMembershipService();
		var adapter = new CmBuyItemKnownVisibleObjectPopulationAdapterService(membership);
		membership.UpsertKnownObjects(
			OwnerPlayerObjectId,
			[
				new CmBuyItemKnownVisibleObjectMembershipCandidate(SellerNpcObjectId, CmBuyItemKnownVisibleObjectKind.Npc, IsVisibleToOwner: true),
				new CmBuyItemKnownVisibleObjectMembershipCandidate(SellerPlayerObjectId, CmBuyItemKnownVisibleObjectKind.Player, IsVisibleToOwner: true),
			]);
		var owner = CreatePlayer(OwnerPlayerObjectId, x: 0, worldId: 210010000);

		var result = adapter.RefreshOwnerFromSuppliedFacts(
			owner,
			[CreatePlayer(SellerPlayerObjectId, x: 94, worldId: 210010000)],
			[]);

		Assert.Equal(1, result.RemovedStaleObjectCount);
		Assert.Equal([SellerPlayerObjectId], result.Snapshot.KnownObjectIds);
		Assert.DoesNotContain(result.Snapshot.Entries, entry => entry.KnownObjectId == SellerNpcObjectId);
	}

	[Fact]
	public void PopulationResolverAdapter_RefreshesSuppliedFactsBeforeResolvingSeller()
	{
		var membership = new CmBuyItemKnownVisibleObjectMembershipService();
		var population = new CmBuyItemKnownVisibleObjectPopulationAdapterService(membership);
		var adapter = new CmBuyItemKnownVisibleObjectPopulationResolverAdapterService(membership, population);
		var owner = CreatePlayer(OwnerPlayerObjectId, x: 0, worldId: 210010000);
		var visibleNpc = CreateNpc(SellerNpcObjectId, x: 11, worldId: 210010000);

		var plan = adapter.CreatePlan(
			owner,
			SellerNpcObjectId,
			_ => [owner],
			_ => [visibleNpc]);

		Assert.True(plan.RefreshesSuppliedFactsBeforeResolve);
		Assert.False(plan.IsJavaRegionKnownListParity);
		Assert.False(plan.IsLive);
		Assert.NotNull(plan.PopulationResult);
		Assert.Equal(1, plan.PopulationResult.UpsertedVisibleObjectCount);
		Assert.Equal(CmBuyItemKnownVisibleObjectResolverAdapterStatus.KnownObjectTarget, plan.ResolverPlan.Status);
		Assert.True(plan.ResolverPlan.IsKnownByPlayer);
		Assert.Equal(CmBuyItemKnownVisibleObjectKind.Npc, plan.ResolverPlan.SnapshotObjectKind);
	}

	[Fact]
	public void PopulationResolverAdapter_RemovesStaleFactsBeforeResolvingSeller()
	{
		var membership = new CmBuyItemKnownVisibleObjectMembershipService();
		var population = new CmBuyItemKnownVisibleObjectPopulationAdapterService(membership);
		var adapter = new CmBuyItemKnownVisibleObjectPopulationResolverAdapterService(membership, population);
		var owner = CreatePlayer(OwnerPlayerObjectId, x: 0, worldId: 210010000);
		membership.UpsertKnownObjects(
			OwnerPlayerObjectId,
			[new CmBuyItemKnownVisibleObjectMembershipCandidate(SellerNpcObjectId, CmBuyItemKnownVisibleObjectKind.Npc, IsVisibleToOwner: true)]);

		var plan = adapter.CreatePlan(
			owner,
			SellerNpcObjectId,
			_ => [owner],
			_ => []);

		Assert.NotNull(plan.PopulationResult);
		Assert.Equal(1, plan.PopulationResult.RemovedStaleObjectCount);
		Assert.Equal(CmBuyItemKnownVisibleObjectResolverAdapterStatus.UnknownObjectTarget, plan.ResolverPlan.Status);
		Assert.False(plan.ResolverPlan.IsKnownByPlayer);
	}

	[Fact]
	public async Task GameServerConnection_KnownNpcFactAllowsNpcBuyPlannerSelection()
	{
		var membership = new CmBuyItemKnownVisibleObjectMembershipService();
		membership.UpsertKnownObjects(
			OwnerPlayerObjectId,
			[new CmBuyItemKnownVisibleObjectMembershipCandidate(SellerNpcObjectId, CmBuyItemKnownVisibleObjectKind.Npc, IsVisibleToOwner: true)]);
		await using var fixture = await GameServerConnectionBuyItemTests.BuyItemFixture.CreateAsync(
			CmBuyItemKnownVisibleObjectResolverAdapterService.CreateResolver(membership));
		GameServerConnectionBuyItemTests.SetActivePlayerForPacketDispatchForAdapterTests(fixture.Connection, CreatePlayer(OwnerPlayerObjectId));
		fixture.World.TryAddObject(SellerNpcObjectId, CreateNpc(SellerNpcObjectId));

		await GameServerConnectionBuyItemTests.InvokeProcessPacketAsyncForAdapterTests(
			fixture.Connection,
			GameServerConnectionBuyItemTests.CreateBuyItemPayloadForAdapterTests(SellerNpcObjectId, tradeActionId: 13, [(1001, 2)]));

		var plan = Assert.Single(fixture.BuyItemPlans);
		Assert.Equal(CmBuyItemHandlerCompositionPlanStatus.SelectedBuyFromShopPlanner, plan.Status);
		Assert.NotNull(plan.BuyFromShopPlan);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
	}

	[Fact]
	public async Task GameServerConnection_PopulationResolverAcceptsSuppliedVisibleNpcTarget()
	{
		var owner = CreatePlayer(OwnerPlayerObjectId, x: 0, worldId: 210010000);
		var sellerNpc = CreateNpc(SellerNpcObjectId, x: 11, worldId: 210010000);
		var membership = new CmBuyItemKnownVisibleObjectMembershipService();
		var population = new CmBuyItemKnownVisibleObjectPopulationAdapterService(membership);
		var adapter = new CmBuyItemKnownVisibleObjectPopulationResolverAdapterService(membership, population);
		await using var fixture = await GameServerConnectionBuyItemTests.BuyItemFixture.CreateAsync(
			adapter.CreateResolver(_ => [owner], _ => [sellerNpc]));
		GameServerConnectionBuyItemTests.SetActivePlayerForPacketDispatchForAdapterTests(fixture.Connection, owner);
		fixture.World.TryAddObject(SellerNpcObjectId, sellerNpc);

		await GameServerConnectionBuyItemTests.InvokeProcessPacketAsyncForAdapterTests(
			fixture.Connection,
			GameServerConnectionBuyItemTests.CreateBuyItemPayloadForAdapterTests(SellerNpcObjectId, tradeActionId: 13, [(1001, 2)]));

		var plan = Assert.Single(fixture.BuyItemPlans);
		Assert.Equal(CmBuyItemHandlerCompositionPlanStatus.SelectedBuyFromShopPlanner, plan.Status);
		Assert.NotNull(plan.BuyFromShopPlan);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.Contains(SellerNpcObjectId, membership.GetSnapshot(OwnerPlayerObjectId).KnownObjectIds);
	}

	[Fact]
	public async Task GameServerConnection_PopulationResolverRejectsStaleNpcTarget()
	{
		var owner = CreatePlayer(OwnerPlayerObjectId, x: 0, worldId: 210010000);
		var sellerNpc = CreateNpc(SellerNpcObjectId, x: 11, worldId: 210010000);
		var membership = new CmBuyItemKnownVisibleObjectMembershipService();
		membership.UpsertKnownObjects(
			OwnerPlayerObjectId,
			[new CmBuyItemKnownVisibleObjectMembershipCandidate(SellerNpcObjectId, CmBuyItemKnownVisibleObjectKind.Npc, IsVisibleToOwner: true)]);
		var population = new CmBuyItemKnownVisibleObjectPopulationAdapterService(membership);
		var adapter = new CmBuyItemKnownVisibleObjectPopulationResolverAdapterService(membership, population);
		await using var fixture = await GameServerConnectionBuyItemTests.BuyItemFixture.CreateAsync(
			adapter.CreateResolver(_ => [owner], _ => []));
		GameServerConnectionBuyItemTests.SetActivePlayerForPacketDispatchForAdapterTests(fixture.Connection, owner);
		fixture.World.TryAddObject(SellerNpcObjectId, sellerNpc);

		await GameServerConnectionBuyItemTests.InvokeProcessPacketAsyncForAdapterTests(
			fixture.Connection,
			GameServerConnectionBuyItemTests.CreateBuyItemPayloadForAdapterTests(SellerNpcObjectId, tradeActionId: 13, [(1001, 2)]));

		var plan = Assert.Single(fixture.BuyItemPlans);
		Assert.Equal(CmBuyItemHandlerCompositionPlanStatus.SkippedUnknownTarget, plan.Status);
		Assert.Null(plan.BuyFromShopPlan);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.DoesNotContain(SellerNpcObjectId, membership.GetSnapshot(OwnerPlayerObjectId).KnownObjectIds);
	}

	[Fact]
	public async Task GameServerConnection_UnknownNpcFactRejectsNpcBeforeBuyPlannerSelection()
	{
		var membership = new CmBuyItemKnownVisibleObjectMembershipService();
		await using var fixture = await GameServerConnectionBuyItemTests.BuyItemFixture.CreateAsync(
			CmBuyItemKnownVisibleObjectResolverAdapterService.CreateResolver(membership));
		GameServerConnectionBuyItemTests.SetActivePlayerForPacketDispatchForAdapterTests(fixture.Connection, CreatePlayer(OwnerPlayerObjectId));
		fixture.World.TryAddObject(SellerNpcObjectId, CreateNpc(SellerNpcObjectId));

		await GameServerConnectionBuyItemTests.InvokeProcessPacketAsyncForAdapterTests(
			fixture.Connection,
			GameServerConnectionBuyItemTests.CreateBuyItemPayloadForAdapterTests(SellerNpcObjectId, tradeActionId: 13, [(1001, 2)]));

		var plan = Assert.Single(fixture.BuyItemPlans);
		Assert.Equal(CmBuyItemHandlerCompositionPlanStatus.SkippedUnknownTarget, plan.Status);
		Assert.Null(plan.BuyFromShopPlan);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
	}

	private static Player CreatePlayer(int objectId, float x = 0, int worldId = 210010000) =>
		new()
		{
			ObjectId = objectId,
			Name = "Player" + objectId,
			Position = new WorldPosition(worldId, x, 0, 0, Heading: 0),
		};

	private static WorldNpc CreateNpc(int objectId, float x = 11, int worldId = 210010000)
	{
		var template = new NpcTemplateSummary(
			700001,
			"Trade Npc",
			NameId: 0,
			Level: 1,
			Rank: "NORMAL",
			Rating: "NORMAL",
			Race: "NONE",
			Tribe: "NONE",
			Type: "NPC");
		return new WorldNpc(objectId, 700001, template, new WorldPosition(worldId, x, 0, 0, 0));
	}

	private const int OwnerPlayerObjectId = 1001;
	private const int SellerNpcObjectId = 9001;
	private const int SellerPlayerObjectId = 2001;
}
