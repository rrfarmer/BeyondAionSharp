using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Tests;

public sealed class NpcDialogTargetingServiceTests
{
	private const int OpenVendorDialogAction = 33;

	[Fact]
	public void ValidateTargetingNpcWithFunction_AcceptsVisibleNpcSupportingDialogAction()
	{
		var world = CreateWorld();
		var player = CreatePlayer(targetObjectId: 5001);
		var npc = CreateNpc(5001, FunctionDialogIds: [OpenVendorDialogAction]);
		world.TryAddObject(npc.ObjectId, npc);

		var result = NpcDialogTargetingService.ValidateTargetingNpcWithFunction(
			player,
			npc.ObjectId,
			OpenVendorDialogAction,
			world);

		Assert.Equal(NpcDialogTargetingResult.Valid, result);
	}

	[Fact]
	public void ValidateTargetingNpcWithFunction_RejectsUntargetedOrUnknownNpc()
	{
		var world = CreateWorld();
		var player = CreatePlayer(targetObjectId: 5002);
		var npc = CreateNpc(5001, FunctionDialogIds: [OpenVendorDialogAction]);
		world.TryAddObject(npc.ObjectId, npc);

		var untargeted = NpcDialogTargetingService.ValidateTargetingNpcWithFunction(
			player,
			npc.ObjectId,
			OpenVendorDialogAction,
			world);
		player.TargetObjectId = 6001;
		var unknown = NpcDialogTargetingService.ValidateTargetingNpcWithFunction(
			player,
			6001,
			OpenVendorDialogAction,
			world);

		Assert.Equal(NpcDialogTargetingResult.NotTargeted, untargeted);
		Assert.Equal(NpcDialogTargetingResult.UnknownTarget, unknown);
	}

	[Fact]
	public void ValidateTargetingNpcWithFunction_AcceptsTargetedNpcRegardlessOfDistance()
	{
		var world = CreateWorld();
		var player = CreatePlayer(targetObjectId: 5001);
		var npc = CreateNpc(5001, new WorldPosition(210010000, 200, 0, 0, 0), FunctionDialogIds: [OpenVendorDialogAction]);
		world.TryAddObject(npc.ObjectId, npc);

		var result = NpcDialogTargetingService.ValidateTargetingNpcWithFunction(
			player,
			npc.ObjectId,
			OpenVendorDialogAction,
			world);

		Assert.Equal(NpcDialogTargetingResult.Valid, result);
	}

	[Fact]
	public void ValidateTargetingNpcWithFunction_RejectsUnsupportedDialogAction()
	{
		var world = CreateWorld();
		var player = CreatePlayer(targetObjectId: 5001);
		var npc = CreateNpc(5001, FunctionDialogIds: [2, 3]);
		world.TryAddObject(npc.ObjectId, npc);

		var result = NpcDialogTargetingService.ValidateTargetingNpcWithFunction(
			player,
			npc.ObjectId,
			OpenVendorDialogAction,
			world);

		Assert.Equal(NpcDialogTargetingResult.UnsupportedAction, result);
	}

	private static GameWorld CreateWorld()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		world.Initialize();
		return world;
	}

	private static Player CreatePlayer(int targetObjectId)
	{
		return new Player
		{
			ObjectId = 1001,
			TargetObjectId = targetObjectId,
			Position = new WorldPosition(210010000, 0, 0, 0, 0),
		};
	}

	private static WorldNpc CreateNpc(
		int objectId,
		WorldPosition? position = null,
		IReadOnlyList<int>? FunctionDialogIds = null)
	{
		var template = new NpcTemplateSummary(
			TemplateId: 799211,
			Name: "broker",
			NameId: 0,
			Level: 10,
			Rank: "NORMAL",
			Rating: "NORMAL",
			Race: "NONE",
			Tribe: "GENERAL",
			Type: "NPC",
			FunctionDialogIds: FunctionDialogIds);
		return new WorldNpc(objectId, template.TemplateId, template, position ?? new WorldPosition(210010000, 1, 1, 0, 0));
	}
}
