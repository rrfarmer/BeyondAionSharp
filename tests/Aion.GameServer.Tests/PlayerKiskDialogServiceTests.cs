using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Tests;

public sealed class PlayerKiskDialogServiceTests
{
	[Fact]
	public void RequestDialogStartsJavaBindstoneQuestionForAllowedKisk()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var registry = new PlayerKiskRegistry();
		var player = new Player
		{
			ObjectId = 1002,
			Race = "ELYOS",
			Position = new WorldPosition(210010000, 1, 1, 1, 0),
		};
		var kisk = RegisterKisk(world, registry, useMask: 1, ownerRace: "ELYOS");

		var result = PlayerKiskDialogService.RequestDialog(player, kisk.ObjectId, world, registry);

		Assert.True(result.Handled);
		Assert.Equal(PlayerKiskDialogStatus.QuestionRequested, result.Status);
		Assert.IsType<SmQuestionWindow>(result.ResponsePacket);
		Assert.Equal(new PendingKiskBindRequest(kisk.ObjectId, SmQuestionWindow.RegisterBindstone), player.PendingKiskBindRequest);
		Assert.Equal(1, player.ResponseRequester.Count);
	}

	[Fact]
	public void RequestDialogRejectsDuplicateBindstoneQuestionThroughResponseRequester()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var registry = new PlayerKiskRegistry();
		var player = new Player
		{
			ObjectId = 1002,
			Race = "ELYOS",
			Position = new WorldPosition(210010000, 1, 1, 1, 0),
		};
		var kisk = RegisterKisk(world, registry, useMask: 1, ownerRace: "ELYOS");
		var first = PlayerKiskDialogService.RequestDialog(player, kisk.ObjectId, world, registry);

		var duplicate = PlayerKiskDialogService.RequestDialog(player, kisk.ObjectId, world, registry);

		Assert.Equal(PlayerKiskDialogStatus.QuestionRequested, first.Status);
		Assert.Equal(PlayerKiskDialogStatus.PendingRequest, duplicate.Status);
		Assert.Null(duplicate.ResponsePacket);
		Assert.Equal(new PendingKiskBindRequest(kisk.ObjectId, SmQuestionWindow.RegisterBindstone), player.PendingKiskBindRequest);
		Assert.Equal(1, player.ResponseRequester.Count);
	}

	[Fact]
	public void RequestDialogMatchesJavaKiskAIDuplicateAndFullBranches()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var registry = new PlayerKiskRegistry();
		var duplicatePlayer = new Player
		{
			ObjectId = 1002,
			Race = "ELYOS",
			BoundKiskObjectId = 9001,
			Position = new WorldPosition(210010000, 1, 1, 1, 0),
		};
		var otherRacePlayer = new Player
		{
			ObjectId = 1003,
			Race = "ASMODIANS",
			Position = new WorldPosition(210010000, 1, 1, 1, 0),
		};
		var fullPlayer = new Player
		{
			ObjectId = 1004,
			Race = "ELYOS",
			Position = new WorldPosition(210010000, 1, 1, 1, 0),
		};
		var kisk = RegisterKisk(world, registry, useMask: 1, ownerRace: "ELYOS", maxMembers: 2);
		Assert.True(kisk.AddMember(2001));
		Assert.True(kisk.AddMember(2002));

		var duplicate = PlayerKiskDialogService.RequestDialog(duplicatePlayer, kisk.ObjectId, world, registry);
		var full = PlayerKiskDialogService.RequestDialog(fullPlayer, kisk.ObjectId, world, registry);
		var otherRaceWhenFull = PlayerKiskDialogService.RequestDialog(otherRacePlayer, kisk.ObjectId, world, registry);

		Assert.Equal(PlayerKiskDialogStatus.AlreadyRegistered, duplicate.Status);
		Assert.IsType<SmSystemMessage>(duplicate.ResponsePacket);
		Assert.Equal(PlayerKiskDialogStatus.Full, full.Status);
		Assert.IsType<SmSystemMessage>(full.ResponsePacket);
		Assert.Equal(PlayerKiskDialogStatus.Full, otherRaceWhenFull.Status);
		Assert.IsType<SmSystemMessage>(otherRaceWhenFull.ResponsePacket);
	}

	[Fact]
	public void RequestDialogRejectsUnauthorizedBeforeQuestionWhenKiskIsNotFull()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var registry = new PlayerKiskRegistry();
		var player = new Player
		{
			ObjectId = 1003,
			Race = "ASMODIANS",
			Position = new WorldPosition(210010000, 1, 1, 1, 0),
		};
		var kisk = RegisterKisk(world, registry, useMask: 1, ownerRace: "ELYOS");

		var result = PlayerKiskDialogService.RequestDialog(player, kisk.ObjectId, world, registry);

		Assert.Equal(PlayerKiskDialogStatus.NoAuthority, result.Status);
		Assert.IsType<SmSystemMessage>(result.ResponsePacket);
		Assert.Null(player.PendingKiskBindRequest);
	}

	[Fact]
	public void RequestDialogUsesNpcControllerRangeGateForKisk()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var registry = new PlayerKiskRegistry();
		var player = new Player
		{
			ObjectId = 1002,
			Race = "ELYOS",
			Position = new WorldPosition(210010000, 100, 100, 1, 0),
		};
		var kisk = RegisterKisk(world, registry, useMask: 1, ownerRace: "ELYOS");

		var result = PlayerKiskDialogService.RequestDialog(player, kisk.ObjectId, world, registry);

		Assert.Equal(PlayerKiskDialogStatus.TooFar, result.Status);
		Assert.IsType<SmSystemMessage>(result.ResponsePacket);
		Assert.Null(player.PendingKiskBindRequest);
	}

	private static PlayerKiskRuntimeState RegisterKisk(
		GameWorld world,
		PlayerKiskRegistry registry,
		int useMask,
		string ownerRace,
		int maxMembers = 6)
	{
		var template = new NpcTemplateSummary(
			700273,
			"test_kisk",
			NameId: 350991,
			Level: 20,
			Rank: "DISCIPLINED",
			Rating: "NORMAL",
			Race: ownerRace,
			Tribe: "GENERAL",
			Type: "GENERAL",
			BoundRadius: 0.175f,
			TalkDistance: 5,
			AiName: "kisk",
			CanTalkInvisible: false,
			HasTalkInfo: true,
			KiskStats: new KiskStatsSummary(useMask, maxMembers, 18));
		var npc = new WorldNpc(
			9001,
			template.TemplateId,
			template,
			new WorldPosition(210010000, 1, 1, 1, 0),
			AiName: "kisk");
		var kisk = new PlayerKiskRuntimeState(
			npc.ObjectId,
			ownerObjectId: 1001,
			npc.TemplateId,
			useMask,
			maxMembers,
			maxResurrects: 18,
			ownerRace: ownerRace);
		Assert.True(world.TryAddObject(npc.ObjectId, npc));
		registry.RegisterKisk(kisk);
		return kisk;
	}
}
