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

	private static Player CreatePlayer(int objectId) =>
		new()
		{
			ObjectId = objectId,
			Name = "Player" + objectId,
			Position = new WorldPosition(210010000, 0, 0, 0, Heading: 0),
		};

	private static WorldNpc CreateNpc(int objectId)
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
		return new WorldNpc(objectId, 700001, template, new WorldPosition(210010000, 11, 0, 0, 0));
	}

	private const int OwnerPlayerObjectId = 1001;
	private const int SellerNpcObjectId = 9001;
	private const int SellerPlayerObjectId = 2001;
}
