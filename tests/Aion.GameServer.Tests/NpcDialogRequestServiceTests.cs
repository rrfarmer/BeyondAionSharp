using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Tests;

public sealed class NpcDialogRequestServiceTests
{
	[Fact]
	public void RequestDialog_RejectsUnknownOrNotInteractableNpc()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var player = CreatePlayer();
		var npc = CreateNpc(5001, hasTalkInfo: false);
		world.TryAddObject(npc.ObjectId, npc);

		var unknown = NpcDialogRequestService.RequestDialog(player, 6001, world);
		var notKnown = NpcDialogRequestService.RequestDialog(player, npc.ObjectId, world, isKnownNpc: (_, _) => false);
		var notInteractable = NpcDialogRequestService.RequestDialog(player, npc.ObjectId, world);

		Assert.False(unknown.Handled);
		Assert.Equal(NpcDialogRequestStatus.UnknownTarget, unknown.Status);
		Assert.False(notKnown.Handled);
		Assert.Equal(NpcDialogRequestStatus.UnknownTarget, notKnown.Status);
		Assert.False(notInteractable.Handled);
		Assert.Equal(NpcDialogRequestStatus.NotInteractable, notInteractable.Status);
	}

	[Fact]
	public void RequestDialog_SendsDialogTooFarForDialogNpcOutsideTalkRange()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var player = CreatePlayer();
		var npc = CreateNpc(5001, hasTalkInfo: true, isDialogNpc: true, position: new WorldPosition(210010000, 20, 0, 0, 0));
		world.TryAddObject(npc.ObjectId, npc);

		var result = NpcDialogRequestService.RequestDialog(player, npc.ObjectId, world);

		Assert.True(result.Handled);
		Assert.Equal(NpcDialogRequestStatus.TooFar, result.Status);
		var packet = Assert.IsType<SmSystemMessage>(result.ResponsePacket);
		Assert.Equal(1300346, packet.MessageId);
	}

	[Fact]
	public void RequestDialog_SendsWarehouseTooFarForFunctionNpcOutsideTalkRange()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var player = CreatePlayer();
		var npc = CreateNpc(5001, hasTalkInfo: true, isDialogNpc: false, position: new WorldPosition(210010000, 20, 0, 0, 0));
		world.TryAddObject(npc.ObjectId, npc);

		var result = NpcDialogRequestService.RequestDialog(player, npc.ObjectId, world);

		Assert.True(result.Handled);
		Assert.Equal(NpcDialogRequestStatus.TooFar, result.Status);
		var packet = Assert.IsType<SmSystemMessage>(result.ResponsePacket);
		Assert.Equal(1300419, packet.MessageId);
	}

	[Fact]
	public void RequestDialog_StartsDialogForNpcInsideTalkRange()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var player = CreatePlayer();
		var npc = CreateNpc(5001, hasTalkInfo: true, isDialogNpc: true, position: new WorldPosition(210010000, 2, 0, 0, 0));
		world.TryAddObject(npc.ObjectId, npc);

		var result = NpcDialogRequestService.RequestDialog(player, npc.ObjectId, world);

		Assert.True(result.Handled);
		Assert.Equal(NpcDialogRequestStatus.DialogStarted, result.Status);
		Assert.Null(result.ResponsePacket);
	}

	private static Player CreatePlayer()
	{
		return new Player
		{
			ObjectId = 1001,
			Position = new WorldPosition(210010000, 0, 0, 0, 0),
		};
	}

	private static WorldNpc CreateNpc(
		int objectId,
		bool hasTalkInfo,
		bool isDialogNpc = false,
		WorldPosition? position = null)
	{
		var template = new NpcTemplateSummary(
			TemplateId: 203000 + objectId,
			Name: "dialog-npc",
			NameId: 1,
			Level: 1,
			Rank: "NORMAL",
			Rating: "NORMAL",
			Race: "ELYOS",
			Tribe: "GENERAL",
			Type: "GENERAL",
			TalkDistance: 2,
			BoundRadius: 0.5f,
			HasTalkInfo: hasTalkInfo,
			IsDialogNpc: isDialogNpc);
		return new WorldNpc(objectId, template.TemplateId, template, position ?? new WorldPosition(210010000, 1, 0, 0, 0));
	}
}
