using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class CmBuyItemKnownListTargetFactAdapterServiceTests
{
	[Fact]
	public void CreatePlan_KnownNpcFactResolvesNpcWithoutWorldOnlyApproximation()
	{
		var plan = CmBuyItemKnownListTargetFactAdapterService.CreatePlan(
			new Player { ObjectId = 1001 },
			sellerObjectId: 9001,
			CreateNpc(9001),
			isKnownByPlayer: true);

		Assert.Equal(CmBuyItemKnownListTargetFactAdapterStatus.ResolvedFromKnownListFact, plan.Status);
		Assert.Equal(CmBuyItemRunTargetKind.Npc, plan.TargetKind);
		Assert.True(plan.IsKnownByPlayer);
		Assert.False(plan.UsesWorldObjectOnlyApproximation);
		Assert.True(plan.IsJavaKnownListParity);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePlan_NotKnownFactReturnsUnknownEvenWhenWorldObjectExists()
	{
		var plan = CmBuyItemKnownListTargetFactAdapterService.CreatePlan(
			new Player { ObjectId = 1001 },
			sellerObjectId: 9001,
			CreateNpc(9001),
			isKnownByPlayer: false);

		Assert.Equal(CmBuyItemKnownListTargetFactAdapterStatus.NotKnownByPlayer, plan.Status);
		Assert.Equal(CmBuyItemRunTargetKind.Unknown, plan.TargetKind);
		Assert.False(plan.IsKnownByPlayer);
		Assert.False(plan.UsesWorldObjectOnlyApproximation);
		Assert.True(plan.IsJavaKnownListParity);
	}

	[Fact]
	public void CreatePlan_WorldObjectWithoutKnownFactIsExplicitApproximation()
	{
		var plan = CmBuyItemKnownListTargetFactAdapterService.CreatePlan(
			new Player { ObjectId = 1001 },
			sellerObjectId: 9001,
			CreateNpc(9001),
			isKnownByPlayer: null);

		Assert.Equal(CmBuyItemKnownListTargetFactAdapterStatus.WorldObjectOnlyApproximation, plan.Status);
		Assert.Equal(CmBuyItemRunTargetKind.Npc, plan.TargetKind);
		Assert.Null(plan.IsKnownByPlayer);
		Assert.True(plan.UsesWorldObjectOnlyApproximation);
		Assert.False(plan.IsJavaKnownListParity);
	}

	[Fact]
	public void CreatePlan_MissingWorldObjectReturnsUnknown()
	{
		var plan = CmBuyItemKnownListTargetFactAdapterService.CreatePlan(
			new Player { ObjectId = 1001 },
			sellerObjectId: 9001,
			worldObject: null,
			isKnownByPlayer: null);

		Assert.Equal(CmBuyItemKnownListTargetFactAdapterStatus.UnknownWorldObject, plan.Status);
		Assert.Equal(CmBuyItemRunTargetKind.Unknown, plan.TargetKind);
		Assert.False(plan.UsesWorldObjectOnlyApproximation);
	}

	[Fact]
	public void CreatePlan_PlayerTargetClassifiesPlayerWhenKnown()
	{
		var plan = CmBuyItemKnownListTargetFactAdapterService.CreatePlan(
			new Player { ObjectId = 1001 },
			sellerObjectId: 2001,
			new Player { ObjectId = 2001 },
			isKnownByPlayer: true);

		Assert.Equal(CmBuyItemRunTargetKind.Player, plan.TargetKind);
		Assert.Equal(CmBuyItemKnownListTargetFactAdapterStatus.ResolvedFromKnownListFact, plan.Status);
	}

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
}
