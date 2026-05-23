using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class PortalEntryValidationServiceTests
{
	[Fact]
	public void ValidateCooldown_AllowsWhenJavaCooldownCountIsBelowMax()
	{
		var now = DateTimeOffset.FromUnixTimeMilliseconds(100_000);
		var player = CreatePlayerWithCooldown(reuseTimeMillis: 200_000, entryCount: 1);
		var cooltimes = CreateCooltimes(maxCount: 2);

		var result = PortalEntryValidationService.ValidateCooldown(player, WorldId, cooltimes, now);

		Assert.True(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.Allowed, result.Status);
		Assert.Null(result.FailurePacket);
		Assert.Single(player.PortalCooldowns);
	}

	[Fact]
	public void ValidateCooldown_RejectsWithJavaSystemMessageWhenCountMeetsMax()
	{
		var now = DateTimeOffset.FromUnixTimeMilliseconds(100_000);
		var player = CreatePlayerWithCooldown(reuseTimeMillis: 200_000, entryCount: 2);
		var cooltimes = CreateCooltimes(maxCount: 2);

		var result = PortalEntryValidationService.ValidateCooldown(player, WorldId, cooltimes, now);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.CooldownLocked, result.Status);
		var packet = Assert.IsType<SmSystemMessage>(result.FailurePacket);
		Assert.Equal(1400043, packet.MessageId);
	}

	[Fact]
	public void ValidateCooldown_RemovesExpiredJavaCooldownAndAllowsEntry()
	{
		var now = DateTimeOffset.FromUnixTimeMilliseconds(100_000);
		var player = CreatePlayerWithCooldown(reuseTimeMillis: 99_999, entryCount: 2);
		var cooltimes = CreateCooltimes(maxCount: 2);

		var result = PortalEntryValidationService.ValidateCooldown(player, WorldId, cooltimes, now);

		Assert.True(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.Allowed, result.Status);
		Assert.Null(result.FailurePacket);
		Assert.Empty(player.PortalCooldowns);
	}

	[Fact]
	public void ValidateCooldownForRegisteredInstance_SkipsCooldownLockoutForSameSoloInstance()
	{
		var now = DateTimeOffset.FromUnixTimeMilliseconds(100_000);
		var player = CreatePlayerWithCooldown(reuseTimeMillis: 200_000, entryCount: 2);
		player.Position = new WorldPosition(WorldId, 10, 20, 30, 40, InstanceId: 2);
		var worldMaps = CreateWorldMapsWithRegisteredSoloInstance(player.ObjectId, out var instance);
		var cooltimes = CreateCooltimes(maxCount: 2);

		var result = PortalEntryValidationService.ValidateCooldownForRegisteredInstance(
			player,
			WorldId,
			maxPlayers: 1,
			worldMaps,
			cooltimes,
			now);

		Assert.True(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.Allowed, result.Status);
		Assert.Same(instance, result.RegisteredInstance);
		Assert.False(result.Reenter);
		Assert.Null(result.FailurePacket);
	}

	[Fact]
	public void ValidateCooldownForRegisteredInstance_MarksReenterWhenRegisteredElsewhere()
	{
		var now = DateTimeOffset.FromUnixTimeMilliseconds(100_000);
		var player = CreatePlayerWithCooldown(reuseTimeMillis: 200_000, entryCount: 2);
		player.Position = new WorldPosition(210010000, 10, 20, 30, 40, InstanceId: 1);
		var worldMaps = CreateWorldMapsWithRegisteredSoloInstance(player.ObjectId, out var instance);
		var cooltimes = CreateCooltimes(maxCount: 2);

		var result = PortalEntryValidationService.ValidateCooldownForRegisteredInstance(
			player,
			WorldId,
			maxPlayers: 1,
			worldMaps,
			cooltimes,
			now);

		Assert.True(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.Allowed, result.Status);
		Assert.Same(instance, result.RegisteredInstance);
		Assert.True(result.Reenter);
		Assert.Null(result.FailurePacket);
	}

	[Fact]
	public void ValidateCooldownForRegisteredInstance_RejectsUnregisteredSoloEntryWhenCooldownLocked()
	{
		var now = DateTimeOffset.FromUnixTimeMilliseconds(100_000);
		var player = CreatePlayerWithCooldown(reuseTimeMillis: 200_000, entryCount: 2);
		player.Position = new WorldPosition(210010000, 10, 20, 30, 40, InstanceId: 1);
		var worldMaps = new WorldMapRuntimeStateTable([new WorldMapSummary(WorldId, IsInstance: true, TwinCount: 1)]);
		var cooltimes = CreateCooltimes(maxCount: 2);

		var result = PortalEntryValidationService.ValidateCooldownForRegisteredInstance(
			player,
			WorldId,
			maxPlayers: 1,
			worldMaps,
			cooltimes,
			now);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.CooldownLocked, result.Status);
		Assert.Null(result.RegisteredInstance);
		Assert.False(result.Reenter);
		var packet = Assert.IsType<SmSystemMessage>(result.FailurePacket);
		Assert.Equal(1400043, packet.MessageId);
	}

	[Fact]
	public void ValidateEnterLevel_UsesJavaInstanceCooltimeLevelsWhenPortalPathMinIsMissing()
	{
		var player = new Player { Race = "ELYOS", Level = 25 };
		var cooltimes = CreateCooltimesWithLevels(
			elyosMin: 25,
			elyosMax: 50,
			asmodianMin: 30,
			asmodianMax: 55);

		var result = PortalEntryValidationService.ValidateEnterLevel(player, WorldId, cooltimes);

		Assert.True(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.Allowed, result.Status);
		Assert.Null(result.FailurePacket);
	}

	[Fact]
	public void ValidateEnterLevel_RejectsBelowJavaMinimumWithSystemMessage()
	{
		var player = new Player { Race = "ELYOS", Level = 24 };
		var cooltimes = CreateCooltimesWithLevels(
			elyosMin: 25,
			elyosMax: 50,
			asmodianMin: 30,
			asmodianMax: 55);

		var result = PortalEntryValidationService.ValidateEnterLevel(player, WorldId, cooltimes);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.LevelRestricted, result.Status);
		var packet = Assert.IsType<SmSystemMessage>(result.FailurePacket);
		Assert.Equal(1400179, packet.MessageId);
	}

	[Fact]
	public void ValidateEnterLevel_RejectsAboveJavaMaximum()
	{
		var player = new Player { Race = "ASMODIANS", Level = 56 };
		var cooltimes = CreateCooltimesWithLevels(
			elyosMin: 25,
			elyosMax: 50,
			asmodianMin: 30,
			asmodianMax: 55);

		var result = PortalEntryValidationService.ValidateEnterLevel(player, WorldId, cooltimes);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.LevelRestricted, result.Status);
		var packet = Assert.IsType<SmSystemMessage>(result.FailurePacket);
		Assert.Equal(1400179, packet.MessageId);
	}

	[Fact]
	public void ValidateEnterLevel_UsesPortalPathMinimumBeforeInstanceCooltimeMinimum()
	{
		var player = new Player { Race = "ELYOS", Level = 18 };
		var cooltimes = CreateCooltimesWithLevels(
			elyosMin: 25,
			elyosMax: 50,
			asmodianMin: 30,
			asmodianMax: 55);

		var result = PortalEntryValidationService.ValidateEnterLevel(
			player,
			WorldId,
			cooltimes,
			portalPathMinLevel: 18);

		Assert.True(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.Allowed, result.Status);
	}

	[Fact]
	public void ValidateEnterLevel_ReturnsJavaErrLevelDialogWhenPortalPathProvidesOne()
	{
		var player = new Player { Race = "ELYOS", Level = 24 };
		var cooltimes = CreateCooltimesWithLevels(
			elyosMin: 25,
			elyosMax: 50,
			asmodianMin: 30,
			asmodianMax: 55);

		var result = PortalEntryValidationService.ValidateEnterLevel(
			player,
			WorldId,
			cooltimes,
			portalPathErrLevel: 1011,
			npcObjectId: 4001);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.LevelRestricted, result.Status);
		var packet = Assert.IsType<SmDialogWindow>(result.FailurePacket);
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);
		Assert.Equal(4001, reader.ReadD());
		Assert.Equal(1011, reader.ReadH());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void ValidateEnterLevel_UsesLoadedPortalPathSummaryFields()
	{
		var player = new Player { Race = "ELYOS", Level = 24 };
		var cooltimes = CreateCooltimesWithLevels(
			elyosMin: 25,
			elyosMax: 50,
			asmodianMin: 30,
			asmodianMax: 55);
		var portalPath = CreatePortalPath(minLevel: 25, errLevel: 1011);

		var result = PortalEntryValidationService.ValidateEnterLevel(
			player,
			WorldId,
			cooltimes,
			portalPath,
			npcObjectId: 4001);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.LevelRestricted, result.Status);
		var packet = Assert.IsType<SmDialogWindow>(result.FailurePacket);
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);
		Assert.Equal(4001, reader.ReadD());
		Assert.Equal(1011, reader.ReadH());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void ValidateEnterLevel_AllowsMembershipBypassLikeJavaPermission()
	{
		var player = new Player { Race = "ELYOS", Level = 1 };
		var cooltimes = CreateCooltimesWithLevels(
			elyosMin: 25,
			elyosMax: 50,
			asmodianMin: 30,
			asmodianMax: 55);

		var result = PortalEntryValidationService.ValidateEnterLevel(
			player,
			WorldId,
			cooltimes,
			bypassLevelRequirement: true);

		Assert.True(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.Allowed, result.Status);
		Assert.Null(result.FailurePacket);
	}

	[Fact]
	public void ValidateMentor_AllowsNonMentorWhenInstanceDisallowsMentors()
	{
		var player = new Player { IsMentor = false };
		var cooltimes = CreateCooltimesWithMentor(canEnterMentor: false);

		var result = PortalEntryValidationService.ValidateMentor(player, WorldId, cooltimes);

		Assert.True(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.Allowed, result.Status);
		Assert.Null(result.FailurePacket);
	}

	[Fact]
	public void ValidateMentor_RejectsMentorWhenJavaTemplateDisallowsMentors()
	{
		var player = new Player { IsMentor = true };
		var cooltimes = CreateCooltimesWithMentor(canEnterMentor: false);

		var result = PortalEntryValidationService.ValidateMentor(player, WorldId, cooltimes);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.MentorRestricted, result.Status);
		var packet = Assert.IsType<SmSystemMessage>(result.FailurePacket);
		Assert.Equal(1400766, packet.MessageId);
	}

	[Fact]
	public void ValidateMentor_AllowsMentorWhenJavaTemplateAllowsMentors()
	{
		var player = new Player { IsMentor = true };
		var cooltimes = CreateCooltimesWithMentor(canEnterMentor: true);

		var result = PortalEntryValidationService.ValidateMentor(player, WorldId, cooltimes);

		Assert.True(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.Allowed, result.Status);
		Assert.Null(result.FailurePacket);
	}

	[Fact]
	public void ValidateRace_AllowsPcAllRaceLikeJavaPortalPathDefault()
	{
		var player = new Player { Race = "ELYOS" };

		var result = PortalEntryValidationService.ValidateRace(player, "PC_ALL");

		Assert.True(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.Allowed, result.Status);
		Assert.Null(result.FailurePacket);
	}

	[Fact]
	public void ValidateRace_AllowsMatchingPortalRace()
	{
		var player = new Player { Race = "ASMODIANS" };

		var result = PortalEntryValidationService.ValidateRace(player, "ASMODIANS");

		Assert.True(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.Allowed, result.Status);
		Assert.Null(result.FailurePacket);
	}

	[Fact]
	public void ValidateRace_ReturnsNoRightDialogForDialogNpcMismatch()
	{
		var player = new Player { Race = "ELYOS" };

		var result = PortalEntryValidationService.ValidateRace(
			player,
			"ASMODIANS",
			npcIsDialogNpc: true,
			npcObjectId: 4001);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.RaceRestricted, result.Status);
		var packet = Assert.IsType<SmDialogWindow>(result.FailurePacket);
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);
		Assert.Equal(4001, reader.ReadD());
		Assert.Equal(SmDialogWindow.NoRightPageId, reader.ReadH());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void ValidateRace_UsesLoadedPortalPathSummaryRace()
	{
		var player = new Player { Race = "ELYOS" };
		var portalPath = CreatePortalPath(race: "ASMODIANS");

		var result = PortalEntryValidationService.ValidateRace(
			player,
			portalPath,
			npcIsDialogNpc: false,
			npcObjectId: 4001);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.RaceRestricted, result.Status);
		var packet = Assert.IsType<SmSystemMessage>(result.FailurePacket);
		Assert.Equal(901354, packet.MessageId);
	}

	[Fact]
	public void ValidateRace_ReturnsInvalidRaceSystemMessageForNonDialogNpcMismatch()
	{
		var player = new Player { Race = "ELYOS" };

		var result = PortalEntryValidationService.ValidateRace(
			player,
			"ASMODIANS",
			npcIsDialogNpc: false,
			npcObjectId: 4001);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.RaceRestricted, result.Status);
		var packet = Assert.IsType<SmSystemMessage>(result.FailurePacket);
		Assert.Equal(901354, packet.MessageId);
	}

	[Fact]
	public void ValidateRace_RejectsWhenSuppliedSiegeOwnershipCheckFails()
	{
		var player = new Player { Race = "ELYOS" };

		var result = PortalEntryValidationService.ValidateRace(
			player,
			"PC_ALL",
			siegeOwnerMatchesPlayerRace: false,
			npcIsDialogNpc: false);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.RaceRestricted, result.Status);
		var packet = Assert.IsType<SmSystemMessage>(result.FailurePacket);
		Assert.Equal(901354, packet.MessageId);
	}

	[Fact]
	public void ValidateRace_AllowsMembershipBypassLikeJavaPermission()
	{
		var player = new Player { Race = "ELYOS" };

		var result = PortalEntryValidationService.ValidateRace(
			player,
			"ASMODIANS",
			siegeOwnerMatchesPlayerRace: false,
			bypassRaceRequirement: true);

		Assert.True(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.Allowed, result.Status);
		Assert.Null(result.FailurePacket);
	}

	[Fact]
	public void ValidateRank_AllowsWhenJavaAbyssRankMeetsPortalMinimum()
	{
		var player = new Player { AbyssRank = PlayerAbyssRank.Default() with { Rank = 5 } };

		var result = PortalEntryValidationService.ValidateRank(player, portalPathMinRank: 5, npcObjectId: 4001);

		Assert.True(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.Allowed, result.Status);
		Assert.Null(result.FailurePacket);
	}

	[Fact]
	public void ValidateRank_AllowsWhenJavaPortalMinimumIsZero()
	{
		var player = new Player { AbyssRank = PlayerAbyssRank.Default() };

		var result = PortalEntryValidationService.ValidateRank(player, portalPathMinRank: 0, npcObjectId: 4001);

		Assert.True(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.Allowed, result.Status);
		Assert.Null(result.FailurePacket);
	}

	[Fact]
	public void ValidateRank_ReturnsNoRightDialogWhenRankIsBelowPortalMinimum()
	{
		var player = new Player { AbyssRank = PlayerAbyssRank.Default() with { Rank = 4 } };

		var result = PortalEntryValidationService.ValidateRank(player, portalPathMinRank: 5, npcObjectId: 4001);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.RankRestricted, result.Status);
		var packet = Assert.IsType<SmDialogWindow>(result.FailurePacket);
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);
		Assert.Equal(4001, reader.ReadD());
		Assert.Equal(SmDialogWindow.NoRightPageId, reader.ReadH());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void ValidateRank_UsesLoadedPortalPathSummaryMinimumRank()
	{
		var player = new Player { AbyssRank = PlayerAbyssRank.Default() with { Rank = 4 } };
		var portalPath = CreatePortalPath(minRank: 5);

		var result = PortalEntryValidationService.ValidateRank(player, portalPath, npcObjectId: 4001);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.RankRestricted, result.Status);
		var packet = Assert.IsType<SmDialogWindow>(result.FailurePacket);
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);
		Assert.Equal(4001, reader.ReadD());
		Assert.Equal(SmDialogWindow.NoRightPageId, reader.ReadH());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void ValidateTitle_AllowsWhenJavaPortalTitleRequirementIsMissing()
	{
		var player = new Player { TitleId = 7 };

		var result = PortalEntryValidationService.ValidateTitle(player, portalPathTitleId: 0, npcObjectId: 4001);

		Assert.True(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.Allowed, result.Status);
		Assert.Null(result.FailurePacket);
	}

	[Fact]
	public void ValidateTitle_AllowsWhenActiveJavaCommonDataTitleMatches()
	{
		var player = new Player { TitleId = 7 };

		var result = PortalEntryValidationService.ValidateTitle(player, portalPathTitleId: 7, npcObjectId: 4001);

		Assert.True(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.Allowed, result.Status);
		Assert.Null(result.FailurePacket);
	}

	[Fact]
	public void ValidateTitle_ReturnsNoRightDialogWhenActiveTitleDiffers()
	{
		var player = new Player
		{
			TitleId = 6,
			Titles = [new PlayerTitle(7, ExpireTimeSeconds: 0)],
		};

		var result = PortalEntryValidationService.ValidateTitle(player, portalPathTitleId: 7, npcObjectId: 4001);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.TitleRestricted, result.Status);
		var packet = Assert.IsType<SmDialogWindow>(result.FailurePacket);
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);
		Assert.Equal(4001, reader.ReadD());
		Assert.Equal(SmDialogWindow.NoRightPageId, reader.ReadH());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void ValidateTitle_UsesLoadedPortalPathSummaryTitleId()
	{
		var player = new Player { TitleId = 6 };
		var portalPath = CreatePortalPath(titleId: 7);

		var result = PortalEntryValidationService.ValidateTitle(player, portalPath, npcObjectId: 4001);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.TitleRestricted, result.Status);
		var packet = Assert.IsType<SmDialogWindow>(result.FailurePacket);
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);
		Assert.Equal(4001, reader.ReadD());
		Assert.Equal(SmDialogWindow.NoRightPageId, reader.ReadH());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void ValidateTitle_AllowsMembershipBypassLikeJavaPermission()
	{
		var player = new Player { TitleId = 6 };

		var result = PortalEntryValidationService.ValidateTitle(
			player,
			portalPathTitleId: 7,
			npcObjectId: 4001,
			bypassTitleRequirement: true);

		Assert.True(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.Allowed, result.Status);
		Assert.Null(result.FailurePacket);
	}

	[Fact]
	public void ValidateQuestRequirements_AllowsWhenJavaPortalHasNoQuestRequirements()
	{
		var player = new Player();

		var result = PortalEntryValidationService.ValidateQuestRequirements(player, CreatePortalPath());

		Assert.True(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.Allowed, result.Status);
		Assert.Null(result.FailurePacket);
	}

	[Fact]
	public void ValidateQuestRequirements_AllowsMembershipBypassLikeJavaPermission()
	{
		var player = new Player();
		var portalPath = CreatePortalPath(
			questRequirements: [new PortalQuestRequirementSummary(1044, QuestStep: 3)]);

		var result = PortalEntryValidationService.ValidateQuestRequirements(
			player,
			portalPath,
			bypassQuestRequirement: true);

		Assert.True(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.Allowed, result.Status);
		Assert.Null(result.FailurePacket);
	}

	[Fact]
	public void ValidateQuestRequirements_AllowsWhenAnyJavaQuestIsComplete()
	{
		var player = new Player
		{
			Quests = [new PlayerQuestState(1044, "COMPLETE", QuestVars: 0, Flags: 0, CompleteCount: 1)],
		};
		var portalPath = CreatePortalPath(
			questRequirements: [new PortalQuestRequirementSummary(1044, QuestStep: 0)]);

		var result = PortalEntryValidationService.ValidateQuestRequirements(player, portalPath);

		Assert.True(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.Allowed, result.Status);
		Assert.Null(result.FailurePacket);
	}

	[Fact]
	public void ValidateQuestRequirements_AllowsWhenAnyJavaQuestVarMeetsStep()
	{
		var player = new Player
		{
			Quests = [new PlayerQuestState(1044, "START", QuestVars(var0: 3), Flags: 0, CompleteCount: 0)],
		};
		var portalPath = CreatePortalPath(
			questRequirements:
			[
				new PortalQuestRequirementSummary(1044, QuestStep: 4),
				new PortalQuestRequirementSummary(1044, QuestStep: 3),
			]);

		var result = PortalEntryValidationService.ValidateQuestRequirements(player, portalPath);

		Assert.True(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.Allowed, result.Status);
		Assert.Null(result.FailurePacket);
	}

	[Fact]
	public void ValidateQuestRequirements_ReturnsNoRightDialogForDialogNpcWhenNoQuestMatches()
	{
		var player = new Player
		{
			Quests = [new PlayerQuestState(1044, "START", QuestVars(var0: 2), Flags: 0, CompleteCount: 0)],
		};
		var portalPath = CreatePortalPath(
			questRequirements: [new PortalQuestRequirementSummary(1044, QuestStep: 3)]);

		var result = PortalEntryValidationService.ValidateQuestRequirements(
			player,
			portalPath,
			npcIsDialogNpc: true,
			npcObjectId: 4001);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.QuestRestricted, result.Status);
		var packet = Assert.IsType<SmDialogWindow>(result.FailurePacket);
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);
		Assert.Equal(4001, reader.ReadD());
		Assert.Equal(SmDialogWindow.NoRightPageId, reader.ReadH());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void ValidateQuestRequirements_ReturnsGroupgateSystemMessageForNonDialogNpcWhenNoQuestMatches()
	{
		var player = new Player();
		var portalPath = CreatePortalPath(
			questRequirements: [new PortalQuestRequirementSummary(1044, QuestStep: 3)]);

		var result = PortalEntryValidationService.ValidateQuestRequirements(
			player,
			portalPath,
			npcIsDialogNpc: false,
			npcObjectId: 4001);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.QuestRestricted, result.Status);
		var packet = Assert.IsType<SmSystemMessage>(result.FailurePacket);
		Assert.Equal(1300150, packet.MessageId);
	}

	[Fact]
	public void ValidateRequiredItemsAndKinah_AllowsWhenJavaRequirementsAreMet()
	{
		var player = new Player
		{
			InventoryItems =
			[
				new InventoryItem { ItemId = KinahItemId, Count = 1_000 },
				new InventoryItem { ItemId = 185000077, Count = 1 },
			],
		};
		var portalPath = CreatePortalPath(
			kinah: 500,
			itemRequirements: [new PortalItemRequirementSummary(185000077, ItemCount: 1)]);

		var result = PortalEntryValidationService.ValidateRequiredItemsAndKinah(player, portalPath);

		Assert.True(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.Allowed, result.Status);
		Assert.Null(result.FailurePacket);
	}

	[Fact]
	public void ValidateRequiredItemsAndKinah_ReturnsNoRightDialogWhenDialogNpcKinahIsMissing()
	{
		var player = new Player
		{
			InventoryItems = [new InventoryItem { ItemId = KinahItemId, Count = 499 }],
		};
		var portalPath = CreatePortalPath(kinah: 500);

		var result = PortalEntryValidationService.ValidateRequiredItemsAndKinah(
			player,
			portalPath,
			npcIsDialogNpc: true,
			npcObjectId: 4001);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.KinahRestricted, result.Status);
		var packet = Assert.IsType<SmDialogWindow>(result.FailurePacket);
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);
		Assert.Equal(4001, reader.ReadD());
		Assert.Equal(SmDialogWindow.NoRightPageId, reader.ReadH());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void ValidateRequiredItemsAndKinah_ReturnsNotEnoughKinahForNonDialogNpc()
	{
		var player = new Player();
		var portalPath = CreatePortalPath(kinah: 500);

		var result = PortalEntryValidationService.ValidateRequiredItemsAndKinah(
			player,
			portalPath,
			npcIsDialogNpc: false,
			npcObjectId: 4001);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.KinahRestricted, result.Status);
		var packet = Assert.IsType<SmSystemMessage>(result.FailurePacket);
		Assert.Equal(901285, packet.MessageId);
	}

	[Fact]
	public void ValidateRequiredItemsAndKinah_ReturnsNoRightDialogWhenDialogNpcItemIsMissing()
	{
		var player = new Player
		{
			InventoryItems = [new InventoryItem { ItemId = KinahItemId, Count = 1_000 }],
		};
		var portalPath = CreatePortalPath(
			kinah: 500,
			itemRequirements: [new PortalItemRequirementSummary(185000077, ItemCount: 1)]);

		var result = PortalEntryValidationService.ValidateRequiredItemsAndKinah(
			player,
			portalPath,
			npcIsDialogNpc: true,
			npcObjectId: 4001);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.ItemRestricted, result.Status);
		Assert.IsType<SmDialogWindow>(result.FailurePacket);
	}

	[Fact]
	public void ValidateRequiredItemsAndKinah_ReturnsMissingItemSystemMessageForNonDialogNpc()
	{
		var player = new Player();
		var portalPath = CreatePortalPath(
			itemRequirements: [new PortalItemRequirementSummary(185000077, ItemCount: 1)]);

		var result = PortalEntryValidationService.ValidateRequiredItemsAndKinah(
			player,
			portalPath,
			npcIsDialogNpc: false,
			npcObjectId: 4001);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.ItemRestricted, result.Status);
		var packet = Assert.IsType<SmSystemMessage>(result.FailurePacket);
		Assert.Equal(1400219, packet.MessageId);
	}

	[Fact]
	public void CreateRequiredItemsAndKinahConsumptionPlan_PlansJavaItemThenKinahConsumptionAcrossStacks()
	{
		var itemStackA = new InventoryItem { ObjectId = 10, ItemId = 185000077, Count = 1 };
		var itemStackB = new InventoryItem { ObjectId = 11, ItemId = 185000077, Count = 3 };
		var kinahStack = new InventoryItem { ObjectId = 12, ItemId = KinahItemId, Count = 1_000 };
		var player = new Player
		{
			InventoryItems = [itemStackA, itemStackB, kinahStack],
		};
		var portalPath = CreatePortalPath(
			kinah: 500,
			itemRequirements: [new PortalItemRequirementSummary(185000077, ItemCount: 3)]);

		var plan = PortalEntryValidationService.CreateRequiredItemsAndKinahConsumptionPlan(player, portalPath);

		Assert.True(plan.Succeeded);
		Assert.Null(plan.MissingItemId);
		Assert.Equal(3, plan.ConsumptionSteps.Count);
		Assert.Equal(new[] { 10 }, plan.DeletedObjectIds);
		Assert.Contains(plan.UpdatedItems, item => item.ObjectId == 11 && item.Count == 1);
		Assert.Contains(plan.UpdatedItems, item => item.ObjectId == 12 && item.Count == 500);
		Assert.Collection(
			plan.ConsumptionSteps,
			step =>
			{
				Assert.Equal(185000077, step.ItemId);
				Assert.Equal(10, step.ObjectId);
				Assert.Equal(1, step.ConsumedCount);
				Assert.Equal(0, step.RemainingItemCount);
				Assert.False(step.IsKinah);
			},
			step =>
			{
				Assert.Equal(185000077, step.ItemId);
				Assert.Equal(11, step.ObjectId);
				Assert.Equal(2, step.ConsumedCount);
				Assert.Equal(1, step.RemainingItemCount);
				Assert.False(step.IsKinah);
			},
			step =>
			{
				Assert.Equal(KinahItemId, step.ItemId);
				Assert.Equal(12, step.ObjectId);
				Assert.Equal(500, step.ConsumedCount);
				Assert.Equal(500, step.RemainingItemCount);
				Assert.True(step.IsKinah);
			});
		Assert.Equal(1, itemStackA.Count);
		Assert.Equal(3, itemStackB.Count);
		Assert.Equal(1_000, kinahStack.Count);
	}

	[Fact]
	public void CreateRequiredItemsAndKinahConsumptionPlan_KeepsKinahRowAtZeroLikeJavaStorage()
	{
		var player = new Player
		{
			InventoryItems = [new InventoryItem { ObjectId = 12, ItemId = KinahItemId, Count = 500 }],
		};
		var portalPath = CreatePortalPath(kinah: 500);

		var plan = PortalEntryValidationService.CreateRequiredItemsAndKinahConsumptionPlan(player, portalPath);

		Assert.True(plan.Succeeded);
		Assert.Empty(plan.DeletedObjectIds);
		var kinahUpdate = Assert.Single(plan.UpdatedItems);
		Assert.Equal(12, kinahUpdate.ObjectId);
		Assert.Equal(0, kinahUpdate.Count);
		var step = Assert.Single(plan.ConsumptionSteps);
		Assert.Equal(KinahItemId, step.ItemId);
		Assert.True(step.IsKinah);
		Assert.Equal(0, step.RemainingItemCount);
	}

	[Fact]
	public void CreateRequiredItemsAndKinahConsumptionPlan_FailsBeforePlanningWhenJavaKinahCheckFails()
	{
		var player = new Player
		{
			InventoryItems =
			[
				new InventoryItem { ObjectId = 10, ItemId = 185000077, Count = 1 },
				new InventoryItem { ObjectId = 12, ItemId = KinahItemId, Count = 499 },
			],
		};
		var portalPath = CreatePortalPath(
			kinah: 500,
			itemRequirements: [new PortalItemRequirementSummary(185000077, ItemCount: 1)]);

		var plan = PortalEntryValidationService.CreateRequiredItemsAndKinahConsumptionPlan(player, portalPath);

		Assert.False(plan.Succeeded);
		Assert.Equal(KinahItemId, plan.MissingItemId);
		Assert.Equal(1, plan.MissingCount);
		Assert.Empty(plan.UpdatedItems);
		Assert.Empty(plan.DeletedObjectIds);
		Assert.Empty(plan.ConsumptionSteps);
	}

	[Fact]
	public void CreateRequiredItemsAndKinahConsumptionPlan_FailsBeforePlanningWhenAnyJavaItemCheckFails()
	{
		var player = new Player
		{
			InventoryItems =
			[
				new InventoryItem { ObjectId = 10, ItemId = 185000077, Count = 1 },
				new InventoryItem { ObjectId = 12, ItemId = KinahItemId, Count = 1_000 },
			],
		};
		var portalPath = CreatePortalPath(
			kinah: 500,
			itemRequirements: [new PortalItemRequirementSummary(185000077, ItemCount: 2)]);

		var plan = PortalEntryValidationService.CreateRequiredItemsAndKinahConsumptionPlan(player, portalPath);

		Assert.False(plan.Succeeded);
		Assert.Equal(185000077, plan.MissingItemId);
		Assert.Equal(1, plan.MissingCount);
		Assert.Empty(plan.UpdatedItems);
		Assert.Empty(plan.DeletedObjectIds);
		Assert.Empty(plan.ConsumptionSteps);
	}

	[Fact]
	public void CreateRequiredItemsAndKinahApplication_AppliesJavaDeleteUpdateAndKinahPackets()
	{
		var itemStackA = new InventoryItem { ObjectId = 10, ItemId = 185000077, Count = 1, Location = 0 };
		var itemStackB = new InventoryItem { ObjectId = 11, ItemId = 185000077, Count = 3, Location = 0 };
		var kinahStack = new InventoryItem { ObjectId = 12, ItemId = KinahItemId, Count = 1_000, Location = 0 };
		var player = new Player
		{
			NpcExpands = 2,
			QuestExpands = 1,
			ItemExpands = 3,
			InventoryItems = [itemStackA, itemStackB, kinahStack],
		};
		var portalPath = CreatePortalPath(
			kinah: 500,
			itemRequirements: [new PortalItemRequirementSummary(185000077, ItemCount: 3)]);
		var consumptionPlan = PortalEntryValidationService.CreateRequiredItemsAndKinahConsumptionPlan(player, portalPath);

		var application = PortalEntryValidationService.CreateRequiredItemsAndKinahApplication(
			player,
			consumptionPlan,
			CreateItemTemplates(185000077, KinahItemId));

		Assert.True(application.Applied);
		Assert.Empty(application.MissingTemplateIds);
		Assert.Equal([11, 12], application.InventoryItems.Select(item => item.ObjectId).ToArray());
		Assert.Contains(application.InventoryItems, item => item.ObjectId == 11 && item.Count == 1);
		Assert.Contains(application.InventoryItems, item => item.ObjectId == 12 && item.Count == 500);
		Assert.Equal(4, application.Packets.Count);

		var deletePacket = Assert.IsType<SmDeleteItem>(application.Packets[0]);
		var deletePayload = SerializeUnencryptedPayload(deletePacket);
		using var deleteReader = new PacketBuffer(deletePayload);
		Assert.Equal(10, deleteReader.ReadD());
		Assert.Equal(SmDeleteItem.UseDeleteType, (int)deleteReader.ReadC());
		Assert.Equal(0, deleteReader.Remaining);

		var cubePayload = SerializeUnencryptedPayload(application.Packets[1]);
		using var cubeReader = new PacketBuffer(cubePayload);
		Assert.Equal(0, (int)cubeReader.ReadC());
		Assert.Equal(0, (int)cubeReader.ReadC());
		Assert.Equal(1, cubeReader.ReadD());
		Assert.Equal(2, (int)cubeReader.ReadC());
		Assert.Equal(1, (int)cubeReader.ReadC());
		Assert.Equal(3, (int)cubeReader.ReadC());
		Assert.Equal(0, cubeReader.Remaining);

		var itemUpdatePayload = SerializeUnencryptedPayload(application.Packets[2]);
		using var itemUpdateReader = new PacketBuffer(itemUpdatePayload);
		Assert.Equal(11, itemUpdateReader.ReadD());
		Assert.Equal(CreateItemTemplate(185000077).GetClientName()?.TrimEnd('\0'), itemUpdateReader.ReadS());
		AssertPacketEndsWithUpdateType(itemUpdatePayload, SmInventoryUpdateItem.DecreaseItemUse);

		var kinahUpdatePayload = SerializeUnencryptedPayload(application.Packets[3]);
		using var kinahUpdateReader = new PacketBuffer(kinahUpdatePayload);
		Assert.Equal(12, kinahUpdateReader.ReadD());
		Assert.Equal(CreateItemTemplate(KinahItemId).GetClientName()?.TrimEnd('\0'), kinahUpdateReader.ReadS());
		AssertPacketEndsWithUpdateType(kinahUpdatePayload, SmInventoryUpdateItem.DecreaseKinahBuy);

		Assert.Equal(1, itemStackA.Count);
		Assert.Equal(3, itemStackB.Count);
		Assert.Equal(1_000, kinahStack.Count);
		Assert.Equal([10, 11, 12], player.InventoryItems.Select(item => item.ObjectId).ToArray());
	}

	[Fact]
	public void CreateRequiredItemsAndKinahApplication_ReturnsNoPacketsForFailedConsumptionPlan()
	{
		var player = new Player
		{
			InventoryItems = [new InventoryItem { ObjectId = 10, ItemId = 185000077, Count = 1 }],
		};
		var portalPath = CreatePortalPath(
			itemRequirements: [new PortalItemRequirementSummary(185000077, ItemCount: 2)]);
		var consumptionPlan = PortalEntryValidationService.CreateRequiredItemsAndKinahConsumptionPlan(player, portalPath);

		var application = PortalEntryValidationService.CreateRequiredItemsAndKinahApplication(
			player,
			consumptionPlan,
			CreateItemTemplates(185000077));

		Assert.False(application.Applied);
		Assert.Empty(application.Packets);
		Assert.Empty(application.MissingTemplateIds);
		Assert.Same(player.InventoryItems, application.InventoryItems);
	}

	[Fact]
	public void CreateRequiredItemsAndKinahApplication_ReturnsNoPacketsWhenUpdateTemplateIsMissing()
	{
		var player = new Player
		{
			InventoryItems = [new InventoryItem { ObjectId = 11, ItemId = 185000077, Count = 3 }],
		};
		var portalPath = CreatePortalPath(
			itemRequirements: [new PortalItemRequirementSummary(185000077, ItemCount: 1)]);
		var consumptionPlan = PortalEntryValidationService.CreateRequiredItemsAndKinahConsumptionPlan(player, portalPath);

		var application = PortalEntryValidationService.CreateRequiredItemsAndKinahApplication(
			player,
			consumptionPlan,
			new ItemTemplateTable([]));

		Assert.False(application.Applied);
		Assert.Empty(application.Packets);
		Assert.Equal([185000077], application.MissingTemplateIds);
		Assert.Same(player.InventoryItems, application.InventoryItems);
	}

	[Fact]
	public void ValidatePortalEntryPlan_ReturnsMissingLocationBeforeAnyGuardLikeJava()
	{
		var player = CreatePlayerWithCooldown(reuseTimeMillis: 200_000, entryCount: 2);
		player.IsMentor = true;
		var result = PortalEntryValidationService.ValidatePortalEntryPlan(
			player,
			CreatePortalPath(),
			new PortalLocTable([]),
			CreatePortalCooltimes(maxPlayers: 1, maxCount: 1, canEnterMentor: false),
			CreateWorldMaps(),
			DateTimeOffset.FromUnixTimeMilliseconds(100_000),
			npcObjectId: 4001);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.MissingPortalLocation, result.Status);
		Assert.Null(result.PortalLoc);
		Assert.Null(result.FailurePacket);
	}

	[Fact]
	public void ValidatePortalEntryPlan_MentorFailureHappensBeforeCooldown()
	{
		var player = CreatePlayerWithCooldown(reuseTimeMillis: 200_000, entryCount: 2);
		player.IsMentor = true;

		var result = PortalEntryValidationService.ValidatePortalEntryPlan(
			player,
			CreatePortalPath(),
			CreatePortalLocs(),
			CreatePortalCooltimes(maxPlayers: 1, maxCount: 1, canEnterMentor: false),
			CreateWorldMaps(),
			DateTimeOffset.FromUnixTimeMilliseconds(100_000),
			npcObjectId: 4001);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.MentorRestricted, result.Status);
		var packet = Assert.IsType<SmSystemMessage>(result.FailurePacket);
		Assert.Equal(1400766, packet.MessageId);
	}

	[Fact]
	public void ValidatePortalEntryPlan_RaceFailureHappensBeforeCooldown()
	{
		var player = CreatePlayerWithCooldown(reuseTimeMillis: 200_000, entryCount: 2, race: "ELYOS");

		var result = PortalEntryValidationService.ValidatePortalEntryPlan(
			player,
			CreatePortalPath(race: "ASMODIANS"),
			CreatePortalLocs(),
			CreatePortalCooltimes(maxPlayers: 1, maxCount: 1),
			CreateWorldMaps(),
			DateTimeOffset.FromUnixTimeMilliseconds(100_000),
			npcObjectId: 4001,
			npcIsDialogNpc: false);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.RaceRestricted, result.Status);
		var packet = Assert.IsType<SmSystemMessage>(result.FailurePacket);
		Assert.Equal(901354, packet.MessageId);
	}

	[Fact]
	public void ValidatePortalEntryPlan_RankFailureHappensBeforeCooldown()
	{
		var player = CreatePlayerWithCooldown(reuseTimeMillis: 200_000, entryCount: 2, race: "ELYOS");
		player.AbyssRank = PlayerAbyssRank.Default() with { Rank = 4 };

		var result = PortalEntryValidationService.ValidatePortalEntryPlan(
			player,
			CreatePortalPath(minRank: 5),
			CreatePortalLocs(),
			CreatePortalCooltimes(maxPlayers: 1, maxCount: 1),
			CreateWorldMaps(),
			DateTimeOffset.FromUnixTimeMilliseconds(100_000),
			npcObjectId: 4001);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.RankRestricted, result.Status);
		Assert.IsType<SmDialogWindow>(result.FailurePacket);
	}

	[Fact]
	public void ValidatePortalEntryPlan_TitleFailureHappensBeforeCooldown()
	{
		var player = CreatePlayerWithCooldown(reuseTimeMillis: 200_000, entryCount: 2, race: "ELYOS");
		player.TitleId = 6;

		var result = PortalEntryValidationService.ValidatePortalEntryPlan(
			player,
			CreatePortalPath(titleId: 7),
			CreatePortalLocs(),
			CreatePortalCooltimes(maxPlayers: 1, maxCount: 1),
			CreateWorldMaps(),
			DateTimeOffset.FromUnixTimeMilliseconds(100_000),
			npcObjectId: 4001);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.TitleRestricted, result.Status);
		Assert.IsType<SmDialogWindow>(result.FailurePacket);
	}

	[Fact]
	public void ValidatePortalEntryPlan_CooldownFailureHappensBeforeLevelCheck()
	{
		var player = CreatePlayerWithCooldown(reuseTimeMillis: 200_000, entryCount: 1, race: "ELYOS");
		player.Level = 1;

		var result = PortalEntryValidationService.ValidatePortalEntryPlan(
			player,
			CreatePortalPath(minLevel: 25, errLevel: 1011),
			CreatePortalLocs(),
			CreatePortalCooltimes(maxPlayers: 1, maxCount: 1),
			CreateWorldMaps(),
			DateTimeOffset.FromUnixTimeMilliseconds(100_000),
			npcObjectId: 4001);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.CooldownLocked, result.Status);
		var packet = Assert.IsType<SmSystemMessage>(result.FailurePacket);
		Assert.Equal(1400043, packet.MessageId);
	}

	[Fact]
	public void ValidatePortalEntryPlan_QuestFailureHappensBeforeCooldown()
	{
		var player = CreatePlayerWithCooldown(reuseTimeMillis: 200_000, entryCount: 2, race: "ELYOS");

		var result = PortalEntryValidationService.ValidatePortalEntryPlan(
			player,
			CreatePortalPath(questRequirements: [new PortalQuestRequirementSummary(1044, QuestStep: 3)]),
			CreatePortalLocs(),
			CreatePortalCooltimes(maxPlayers: 1, maxCount: 1),
			CreateWorldMaps(),
			DateTimeOffset.FromUnixTimeMilliseconds(100_000),
			npcObjectId: 4001);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.QuestRestricted, result.Status);
		Assert.IsType<SmDialogWindow>(result.FailurePacket);
	}

	[Fact]
	public void ValidatePortalEntryPlan_ItemFailureHappensAfterLevelBeforeSameInstanceTeleport()
	{
		var player = new Player
		{
			Race = "ELYOS",
			Level = 25,
			Position = new WorldPosition(WorldId, 10, 20, 30, 40, InstanceId: 7),
		};

		var result = PortalEntryValidationService.ValidatePortalEntryPlan(
			player,
			CreatePortalPath(itemRequirements: [new PortalItemRequirementSummary(185000077, ItemCount: 1)]),
			CreatePortalLocs(),
			CreatePortalCooltimes(maxPlayers: 0, maxCount: 0),
			CreateWorldMaps(),
			DateTimeOffset.FromUnixTimeMilliseconds(100_000),
			npcObjectId: 4001);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.ItemRestricted, result.Status);
		Assert.Equal(PortalEntryPlanAction.None, result.Action);
		Assert.IsType<SmDialogWindow>(result.FailurePacket);
	}

	[Fact]
	public void ValidatePortalEntryPlan_ReenterSkipsCooldownAndLevelLikeJava()
	{
		var player = CreatePlayerWithCooldown(reuseTimeMillis: 200_000, entryCount: 1, race: "ELYOS");
		player.Level = 1;
		player.Position = new WorldPosition(210010000, 1, 2, 3, 4, InstanceId: 1);
		var worldMaps = CreateWorldMapsWithRegisteredSoloInstance(player.ObjectId, out var instance);

		var result = PortalEntryValidationService.ValidatePortalEntryPlan(
			player,
			CreatePortalPath(minLevel: 25, errLevel: 1011),
			CreatePortalLocs(),
			CreatePortalCooltimes(maxPlayers: 1, maxCount: 1),
			worldMaps,
			DateTimeOffset.FromUnixTimeMilliseconds(100_000),
			npcObjectId: 4001);

		Assert.True(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.Allowed, result.Status);
		Assert.Same(instance, result.RegisteredInstance);
		Assert.True(result.Reenter);
		Assert.NotNull(result.PortalLoc);
		Assert.Null(result.FailurePacket);
	}

	[Fact]
	public void ValidatePortalEntryPlan_PlansSameInstanceTeleportAfterLevelCheck()
	{
		var player = new Player
		{
			Race = "ELYOS",
			Level = 25,
			Position = new WorldPosition(WorldId, 10, 20, 30, 40, InstanceId: 7),
		};

		var result = PortalEntryValidationService.ValidatePortalEntryPlan(
			player,
			CreatePortalPath(minLevel: 25),
			CreatePortalLocs(),
			CreatePortalCooltimes(maxPlayers: 0, maxCount: 0),
			CreateWorldMaps(),
			DateTimeOffset.FromUnixTimeMilliseconds(100_000),
			npcObjectId: 4001);

		Assert.True(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.Allowed, result.Status);
		Assert.Equal(PortalEntryPlanAction.SameInstanceTeleport, result.Action);
		Assert.Equal(WorldId, result.PortalLoc?.WorldId);
		Assert.Equal(1, result.PortalLoc?.X);
		Assert.Equal(2, result.PortalLoc?.Y);
		Assert.Equal(3, result.PortalLoc?.Z);
		Assert.Equal((byte)4, result.PortalLoc?.Heading);
		Assert.False(result.Reenter);
		Assert.Null(result.FailurePacket);
	}

	[Fact]
	public void ValidatePortalEntryPlan_AllowsOpenWorldPlanWithResolvedLocation()
	{
		var player = new Player { Race = "ELYOS", Level = 25 };

		var result = PortalEntryValidationService.ValidatePortalEntryPlan(
			player,
			CreatePortalPath(minLevel: 25),
			CreatePortalLocs(),
			CreatePortalCooltimes(maxPlayers: 0, maxCount: 0),
			CreateWorldMaps(),
			DateTimeOffset.FromUnixTimeMilliseconds(100_000),
			npcObjectId: 4001);

		Assert.True(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.Allowed, result.Status);
		Assert.Equal(PortalEntryPlanAction.Continue, result.Action);
		Assert.Equal(WorldId, result.PortalLoc?.WorldId);
		Assert.Null(result.RegisteredInstance);
		Assert.False(result.Reenter);
		Assert.Null(result.FailurePacket);
	}

	[Fact]
	public void ValidatePortalEntryPlan_GroupPortalWithoutGroupReturnsErrGroupDialogLikeJava()
	{
		var player = new Player { Race = "ELYOS", Level = 25 };

		var result = PortalEntryValidationService.ValidatePortalEntryPlan(
			player,
			CreatePortalPath(minLevel: 25, errGroup: 9001),
			CreatePortalLocs(),
			CreatePortalCooltimes(maxPlayers: 6, maxCount: 1),
			CreateWorldMaps(),
			DateTimeOffset.FromUnixTimeMilliseconds(100_000),
			npcObjectId: 4001);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.GroupRequired, result.Status);
		Assert.NotNull(result.PortalLoc);
		Assert.IsType<SmDialogWindow>(result.FailurePacket);
	}

	[Fact]
	public void ValidatePortalEntryPlan_GroupPortalWithoutErrGroupReturnsPartySystemMessageLikeJava()
	{
		var player = new Player { Race = "ELYOS", Level = 25 };

		var result = PortalEntryValidationService.ValidatePortalEntryPlan(
			player,
			CreatePortalPath(minLevel: 25),
			CreatePortalLocs(),
			CreatePortalCooltimes(maxPlayers: 3, maxCount: 1),
			CreateWorldMaps(),
			DateTimeOffset.FromUnixTimeMilliseconds(100_000),
			npcObjectId: 4001);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.GroupRequired, result.Status);
		var packet = Assert.IsType<SmSystemMessage>(result.FailurePacket);
		Assert.Equal(1390256, packet.MessageId);
	}

	[Fact]
	public void ValidatePortalEntryPlan_AlliancePortalWithoutAllianceReturnsForceSystemMessageLikeJava()
	{
		var player = new Player { Race = "ELYOS", Level = 25 };

		var result = PortalEntryValidationService.ValidatePortalEntryPlan(
			player,
			CreatePortalPath(minLevel: 25),
			CreatePortalLocs(),
			CreatePortalCooltimes(maxPlayers: 12, maxCount: 1),
			CreateWorldMaps(),
			DateTimeOffset.FromUnixTimeMilliseconds(100_000),
			npcObjectId: 4001);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.AllianceRequired, result.Status);
		var packet = Assert.IsType<SmSystemMessage>(result.FailurePacket);
		Assert.Equal(1400544, packet.MessageId);
	}

	[Fact]
	public void ValidatePortalEntryPlan_LeaguePortalReturnsUnionSystemMessageUntilLeagueModelExists()
	{
		var player = new Player { Race = "ELYOS", Level = 25 };

		var result = PortalEntryValidationService.ValidatePortalEntryPlan(
			player,
			CreatePortalPath(minLevel: 25),
			CreatePortalLocs(),
			CreatePortalCooltimes(maxPlayers: 48, maxCount: 1),
			CreateWorldMaps(),
			DateTimeOffset.FromUnixTimeMilliseconds(100_000),
			npcObjectId: 4001);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.LeagueRequired, result.Status);
		var packet = Assert.IsType<SmSystemMessage>(result.FailurePacket);
		Assert.Equal(1401251, packet.MessageId);
	}

	[Fact]
	public void ValidatePortalEntryPlan_GroupMemberStopsWithBlockedTeamPlanBeforeFanout()
	{
		var player = new Player
		{
			ObjectId = 1001,
			Race = "ELYOS",
			Level = 25,
			TeamMembership = PlayerTeamMembership.Group,
			CurrentTeamId = 88001,
			CurrentTeamMemberObjectIds = [1001, 1002],
		};

		var result = PortalEntryValidationService.ValidatePortalEntryPlan(
			player,
			CreatePortalPath(minLevel: 25),
			CreatePortalLocs(),
			CreatePortalCooltimes(maxPlayers: 6, maxCount: 1),
			CreateWorldMaps(),
			DateTimeOffset.FromUnixTimeMilliseconds(100_000),
			npcObjectId: 4001);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.UnsupportedTeamPortal, result.Status);
		Assert.NotNull(result.PortalLoc);
		Assert.Null(result.FailurePacket);
		Assert.NotNull(result.TeamPlan);
		Assert.Equal(PortalTeamEntryKind.Group, result.TeamPlan.Kind);
		Assert.Equal(88001, result.TeamPlan.TeamId);
		Assert.Equal([1001, 1002], result.TeamPlan.MemberObjectIds);
		Assert.Equal(6, result.TeamPlan.MaxPlayers);
		Assert.Equal(PortalTeamEntryDisposition.FreshInstanceAllocationNeeded, result.TeamPlan.Disposition);
		Assert.Null(result.TeamPlan.RegisteredInstance);
		Assert.False(result.TeamPlan.Reenter);
		Assert.False(result.TeamPlan.FanoutSupported);
	}

	[Fact]
	public void ValidatePortalEntryPlan_GroupMemberFindsRegisteredTeamInstanceBeforeBlockedFanout()
	{
		var player = new Player
		{
			ObjectId = 1001,
			Race = "ELYOS",
			Level = 25,
			Position = new WorldPosition(210010000, 10, 20, 30, 0),
			TeamMembership = PlayerTeamMembership.Group,
			CurrentTeamId = 88001,
			CurrentTeamMemberObjectIds = [1001, 1002],
		};
		var worldMaps = CreateWorldMaps();
		var registered = worldMaps.AddWorldMapInstance(WorldId, instanceId: 7, maxPlayers: 6);
		Assert.NotNull(registered);
		registered.Register(88001);

		var result = PortalEntryValidationService.ValidatePortalEntryPlan(
			player,
			CreatePortalPath(minLevel: 25),
			CreatePortalLocs(),
			CreatePortalCooltimes(maxPlayers: 6, maxCount: 1),
			worldMaps,
			DateTimeOffset.FromUnixTimeMilliseconds(100_000),
			npcObjectId: 4001);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.UnsupportedTeamPortal, result.Status);
		Assert.NotNull(result.TeamPlan);
		Assert.Equal(PortalTeamEntryKind.Group, result.TeamPlan.Kind);
		Assert.Equal(PortalTeamEntryDisposition.RegisteredInstanceTransfer, result.TeamPlan.Disposition);
		Assert.Same(registered, result.TeamPlan.RegisteredInstance);
		Assert.False(result.TeamPlan.Reenter);
		Assert.False(result.TeamPlan.FanoutSupported);
	}

	[Fact]
	public void ValidatePortalEntryPlan_GroupMemberMarksReenterOnlyWhenPlayerObjectIsRegisteredLikeJava()
	{
		var player = new Player
		{
			ObjectId = 1001,
			Race = "ELYOS",
			Level = 25,
			Position = new WorldPosition(210010000, 10, 20, 30, 0),
			TeamMembership = PlayerTeamMembership.Group,
			CurrentTeamId = 88001,
			CurrentTeamMemberObjectIds = [1001, 1002],
		};
		var worldMaps = CreateWorldMaps();
		var registered = worldMaps.AddWorldMapInstance(WorldId, instanceId: 7, maxPlayers: 6);
		Assert.NotNull(registered);
		registered.Register(88001);
		registered.Register(1001);

		var result = PortalEntryValidationService.ValidatePortalEntryPlan(
			player,
			CreatePortalPath(minLevel: 25),
			CreatePortalLocs(),
			CreatePortalCooltimes(maxPlayers: 6, maxCount: 1),
			worldMaps,
			DateTimeOffset.FromUnixTimeMilliseconds(100_000),
			npcObjectId: 4001);

		Assert.False(result.CanEnter);
		Assert.Equal(PortalEntryValidationStatus.UnsupportedTeamPortal, result.Status);
		Assert.NotNull(result.TeamPlan);
		Assert.Same(registered, result.TeamPlan.RegisteredInstance);
		Assert.True(result.TeamPlan.Reenter);
		Assert.False(result.TeamPlan.FanoutSupported);
	}

	private const int WorldId = 300030000;
	private const int KinahItemId = 182400001;

	private static PortalPathSummary CreatePortalPath(
		string race = "PC_ALL",
		int minLevel = 0,
		int minRank = 0,
		int titleId = 0,
		int kinah = 0,
		int errLevel = 0,
		int errGroup = 0,
		IReadOnlyList<PortalQuestRequirementSummary>? questRequirements = null,
		IReadOnlyList<PortalItemRequirementSummary>? itemRequirements = null)
	{
		return new PortalPathSummary(
			PortalPathSource.Dialog,
			NpcId: 730000,
			ScrollName: string.Empty,
			Dialog: 10000,
			LocId: WorldId / 100,
			SiegeId: 0,
			Race: race,
			MinLevel: minLevel,
			MinRank: minRank,
			Kinah: kinah,
			TitleId: titleId,
			ErrGroup: errGroup,
			ErrLevel: errLevel)
		{
			QuestRequirements = questRequirements ?? Array.Empty<PortalQuestRequirementSummary>(),
			ItemRequirements = itemRequirements ?? Array.Empty<PortalItemRequirementSummary>(),
		};
	}

	private static int QuestVars(int var0 = 0)
	{
		return var0 & 0x3F;
	}

	private static ItemTemplateTable CreateItemTemplates(params int[] itemIds)
	{
		return new ItemTemplateTable(itemIds.Select(CreateItemTemplate).ToArray());
	}

	private static ItemTemplateSummary CreateItemTemplate(int itemId)
	{
		return new ItemTemplateSummary(
			itemId,
			itemId == KinahItemId ? "Kinah" : "Portal Item",
			DescriptionId: itemId == KinahItemId ? 12350 : 20001,
			Mask: 0,
			Level: 1,
			ItemGroup: itemId == KinahItemId ? "NONE" : "NORMAL",
			ItemType: "NORMAL",
			Quality: "COMMON",
			Race: "PC_ALL",
			MaxStackCount: 1000,
			Price: 0,
			ValidEquipmentSlots: 0);
	}

	private static Player CreatePlayerWithCooldown(long reuseTimeMillis, int entryCount, string race = "")
	{
		return new Player
		{
			ObjectId = 1001,
			Race = race,
			PortalCooldowns = new Dictionary<int, PlayerPortalCooldown>
			{
				[WorldId] = new(WorldId, reuseTimeMillis, entryCount),
			},
		};
	}

	private static InstanceCooltimeTable CreateCooltimes(int maxCount)
	{
		return new InstanceCooltimeTable(
		[
			new InstanceCooltimeSummary(8, WorldId, "PC_ALL", maxCount),
		]);
	}

	private static InstanceCooltimeTable CreateCooltimesWithLevels(
		int elyosMin,
		int elyosMax,
		int asmodianMin,
		int asmodianMax)
	{
		return new InstanceCooltimeTable(
		[
			new InstanceCooltimeSummary(
				8,
				WorldId,
				"PC_ALL",
				MaxCount: 2,
				EnterMinLevelLight: elyosMin,
				EnterMaxLevelLight: elyosMax,
				EnterMinLevelDark: asmodianMin,
				EnterMaxLevelDark: asmodianMax),
		]);
	}

	private static InstanceCooltimeTable CreateCooltimesWithMentor(bool canEnterMentor)
	{
		return new InstanceCooltimeTable(
		[
			new InstanceCooltimeSummary(
				8,
				WorldId,
				"PC_ALL",
				MaxCount: 2,
				CanEnterMentor: canEnterMentor),
		]);
	}

	private static InstanceCooltimeTable CreatePortalCooltimes(
		int maxPlayers,
		int maxCount,
		bool canEnterMentor = true)
	{
		return new InstanceCooltimeTable(
		[
			new InstanceCooltimeSummary(
				8,
				WorldId,
				"PC_ALL",
				MaxCount: maxCount,
				MaxMemberLight: maxPlayers,
				MaxMemberDark: maxPlayers,
				EnterMinLevelLight: 25,
				EnterMaxLevelLight: 0,
				EnterMinLevelDark: 25,
				EnterMaxLevelDark: 0,
				CanEnterMentor: canEnterMentor),
		]);
	}

	private static PortalLocTable CreatePortalLocs()
	{
		return new PortalLocTable(
		[
			new PortalLocSummary(WorldId, WorldId / 100, X: 1, Y: 2, Z: 3, Heading: 4),
		]);
	}

	private static WorldMapRuntimeStateTable CreateWorldMaps()
	{
		return new WorldMapRuntimeStateTable([new WorldMapSummary(WorldId, IsInstance: true, TwinCount: 1)]);
	}

	private static WorldMapRuntimeStateTable CreateWorldMapsWithRegisteredSoloInstance(
		int playerObjectId,
		out WorldMapInstanceRuntimeState instance)
	{
		var worldMaps = new WorldMapRuntimeStateTable([new WorldMapSummary(WorldId, IsInstance: true, TwinCount: 1)]);
		instance = worldMaps.AddWorldMapInstance(WorldId, instanceId: 2, ownerId: 0, maxPlayers: 1)!;
		instance.Register(playerObjectId);
		return worldMaps;
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}

	private static void AssertPacketEndsWithUpdateType(byte[] payload, int updateType)
	{
		var actual = payload[^2] | (payload[^1] << 8);
		Assert.Equal(updateType, actual);
	}
}
