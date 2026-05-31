using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class CmBuyItemKnownListMembershipResolverAdapterServiceTests
{
	[Fact]
	public void CreatePlan_KnownPlayerTargetReturnsTrueFromMembershipSnapshot()
	{
		var membership = new PlayerKnownListMembershipService();
		membership.UpsertKnownPlayers(
			OwnerPlayerObjectId,
			[new PlayerKnownListMembershipCandidate(SellerPlayerObjectId, IsVisibleToOwner: true)]);

		var plan = CmBuyItemKnownListMembershipResolverAdapterService.CreatePlan(
			CreatePlayer(OwnerPlayerObjectId),
			SellerPlayerObjectId,
			CreatePlayer(SellerPlayerObjectId),
			membership);

		Assert.Equal(CmBuyItemKnownListMembershipResolverAdapterStatus.KnownPlayerTarget, plan.Status);
		Assert.True(plan.IsKnownByPlayer);
		Assert.Equal(1, plan.SnapshotEntryCount);
		Assert.True(plan.UsesPlayerKnownListMembershipSnapshot);
		Assert.False(plan.IsJavaKnownListParity);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePlan_UnknownPlayerTargetReturnsFalseFromMembershipSnapshot()
	{
		var membership = new PlayerKnownListMembershipService();

		var plan = CmBuyItemKnownListMembershipResolverAdapterService.CreatePlan(
			CreatePlayer(OwnerPlayerObjectId),
			SellerPlayerObjectId,
			CreatePlayer(SellerPlayerObjectId),
			membership);

		Assert.Equal(CmBuyItemKnownListMembershipResolverAdapterStatus.UnknownPlayerTarget, plan.Status);
		Assert.False(plan.IsKnownByPlayer);
		Assert.Equal(0, plan.SnapshotEntryCount);
		Assert.True(plan.UsesPlayerKnownListMembershipSnapshot);
	}

	[Fact]
	public void CreatePlan_NpcTargetReturnsUnknownFactBecausePlayerSnapshotCannotRepresentNpc()
	{
		var membership = new PlayerKnownListMembershipService();

		var plan = CmBuyItemKnownListMembershipResolverAdapterService.CreatePlan(
			CreatePlayer(OwnerPlayerObjectId),
			SellerNpcObjectId,
			CreateNpc(SellerNpcObjectId),
			membership);

		Assert.Equal(CmBuyItemKnownListMembershipResolverAdapterStatus.UnsupportedTargetKind, plan.Status);
		Assert.Null(plan.IsKnownByPlayer);
		Assert.False(plan.UsesPlayerKnownListMembershipSnapshot);
		Assert.False(plan.IsJavaKnownListParity);
	}

	[Fact]
	public void CreateResolver_ProjectsPlanMembershipFact()
	{
		var membership = new PlayerKnownListMembershipService();
		membership.UpsertKnownPlayers(
			OwnerPlayerObjectId,
			[new PlayerKnownListMembershipCandidate(SellerPlayerObjectId, IsVisibleToOwner: true)]);
		var resolver = CmBuyItemKnownListMembershipResolverAdapterService.CreateResolver(membership);

		var isKnown = resolver(
			CreatePlayer(OwnerPlayerObjectId),
			SellerPlayerObjectId,
			CreatePlayer(SellerPlayerObjectId));

		Assert.True(isKnown);
	}

	[Fact]
	public void CreatePlan_MissingMembershipServiceReturnsUnknownFact()
	{
		var plan = CmBuyItemKnownListMembershipResolverAdapterService.CreatePlan(
			CreatePlayer(OwnerPlayerObjectId),
			SellerPlayerObjectId,
			CreatePlayer(SellerPlayerObjectId),
			membershipService: null);

		Assert.Equal(CmBuyItemKnownListMembershipResolverAdapterStatus.MissingMembershipService, plan.Status);
		Assert.Null(plan.IsKnownByPlayer);
		Assert.False(plan.UsesPlayerKnownListMembershipSnapshot);
	}

	private static Player CreatePlayer(int objectId) =>
		new()
		{
			ObjectId = objectId,
			Name = "Player" + objectId,
			Position = new WorldPosition(210010000, objectId, 0, 0, Heading: 0),
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
		return new WorldNpc(objectId, 700001, template, new WorldPosition(210010000, 0, 0, 0, 0));
	}

	private const int OwnerPlayerObjectId = 1001;
	private const int SellerPlayerObjectId = 2001;
	private const int SellerNpcObjectId = 9001;
}
